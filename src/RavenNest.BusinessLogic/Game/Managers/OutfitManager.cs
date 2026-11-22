using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;
using Character = RavenNest.DataModels.Character;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Outfits per skill on the server (v0.9.7, docs/outfits-design.md in the game repo): gear that follows what a
    ///     character trains. The rules are RavenNest.Models.OutfitRules, shared with the game; this keeps the outfits,
    ///     puts them on and tells the game.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Kept as one short text per character (RavenNest.Models.CharacterOutfits) in a user property,
    ///     "outfits.{character id}" (ModelMapper.OutfitsKey): no table and nothing to migrate. A slot names its piece
    ///     by inventory item and by kind of item; a piece that is gone (sold, traded, stashed, destroyed) is simply a
    ///     slot to fill when the outfit is next worn, so nothing has to be cleaned up when an item leaves.
    ///     </para>
    ///     <para>
    ///     The server never changes an outfit on its own: the game asks when training changes or a fight begins and
    ///     ends (<see cref="Follow"/>), and a game before 0.9.7 never asks, so nothing changes behind its back. The one
    ///     exception is <see cref="ComeBack"/>, when a character joins or leaves a game: a fight's outfit that was
    ///     left on goes back to the training outfit, which the joining game gets with the character.
    ///     </para>
    ///     <para>
    ///     Three kinds of callers: the game (a session token, the character must be in that session), the server
    ///     itself (<see cref="ComeBack"/>, <see cref="EquippedBest"/>), and the website (by character id; the service
    ///     in front checks whose character it is).
    ///     </para>
    /// </remarks>
    public sealed class OutfitManager
    {
        /// <summary>The first game that knows outfits: an older one is told of gear changes the old way.</summary>
        public static readonly Version FirstGameVersion = new Version(0, 9, 7, 0);

        // the sessions whose game has asked anything about outfits: such a game knows them, whatever version it
        // says it is (a build made before the version was raised)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, bool> askingSessions = new System.Collections.Concurrent.ConcurrentDictionary<Guid, bool>();

        // one change to a character's outfits at a time; shared by every instance (PlayerManager has its own)
        private static readonly object[] locks = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;

        public OutfitManager(GameData gameData, PlayerInventoryProvider inventories)
        {
            this.gameData = gameData;
            this.inventories = inventories;
        }

        private static object LockOf(Guid characterId) => locks[(characterId.GetHashCode() & 0x7fffffff) % locks.Length];

        // ------------------------------------------------------------------------------------------------ the game

        /// <summary>The character's outfits as they are.</summary>
        public OutfitResult Get(SessionToken token, Guid characterId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            var state = Load(character);
            return new OutfitResult { Outfits = state.ToString(), Skill = (int)state.Worn };
        }

        /// <summary>
        ///     The character trains something else, or went into or came out of a raid, dungeon or tower floor: the
        ///     outfit that belongs to that goes on (OutfitRules.Desired), unless "change outfit when I switch
        ///     training" is off. What is worn is kept as the outfit that was on first.
        /// </summary>
        /// <param name="trained">The skill trained now (a Skill's number).</param>
        /// <param name="fighting">In a raid, a dungeon or on a tower floor.</param>
        /// <param name="from">The skill whose outfit the game takes to be on, for a character whose outfits the
        /// server has not kept anything of yet (the skill trained until now).</param>
        public OutfitResult Follow(SessionToken token, Guid characterId, int trained, bool fighting, int from)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult();
                var skill = OutfitRules.Normalize((Skill)trained);
                var before = OutfitRules.Normalize((Skill)from);
                if (skill != Skill.None)
                {
                    if (state.Worn == Skill.None) state.Worn = before;
                    if (state.Fight == Skill.None && OutfitRules.IsCombat(before)) state.Fight = before;
                    if (!fighting && OutfitRules.IsCombat(skill)) state.Fight = skill;
                    if (state.Follow)
                    {
                        var desired = OutfitRules.Desired(skill, fighting, state.Fight);
                        if (desired != state.Worn)
                        {
                            var wasOn = state.Worn;
                            Switch(character, state, desired, result);
                            // a fight's outfit: the one to go back to is kept, should the game never say the fight is over
                            if (fighting && state.Back == Skill.None) state.Back = wasOn != Skill.None ? wasOn : skill;
                        }
                        if (!fighting) state.Back = Skill.None;
                    }
                }
                return Done(character, state, result);
            }
        }

        /// <summary>"!outfit attack": that skill's outfit goes on now, whatever is trained.</summary>
        public OutfitResult Wear(SessionToken token, Guid characterId, int skill)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            return WearNow(character, OutfitRules.Normalize((Skill)skill));
        }

        /// <summary>
        ///     "!outfit save", "!outfit pin", "!outfit unpin" for the skill the character trains.
        ///     <paramref name="mode"/> 0 keeps what is worn as the skill's outfit (pinned slots stay as they are),
        ///     1 pins every slot to what is worn, 2 makes every slot "best" again.
        /// </summary>
        public OutfitResult Save(SessionToken token, Guid characterId, int skill, int mode)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            var target = OutfitRules.Normalize((Skill)skill);
            if (target == Skill.None) return OutfitResult.Fail(OutfitStatus.UnknownSkill);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)target };
                if (mode == 2)
                {
                    OutfitRules.Unpin(state.Find(target));
                    return Done(character, state, result);
                }

                var inventory = inventories.Get(character.Id);
                var pieces = Pieces(character, inventory);
                var pet = BattlePetManager.ActiveOf(gameData, character.Id);
                var outfit = state.GetOrAdd(target);
                if (mode == 1) OutfitRules.Pin(outfit, pieces, pet);
                else OutfitRules.Remember(outfit, pieces, pet);
                // what is worn is this skill's outfit from now on
                state.Worn = target;
                state.Back = Skill.None;
                return Done(character, state, result);
            }
        }

        /// <summary>"!outfit auto on|off" and "!outfit upgrade on|off": -1 leaves a switch as it is, 0 turns it off, 1 on.</summary>
        public OutfitResult Switches(SessionToken token, Guid characterId, int follow, int upgrade)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            return SetSwitches(character, follow < 0 ? (bool?)null : follow > 0, upgrade < 0 ? (bool?)null : upgrade > 0, false);
        }

        /// <summary>
        ///     A piece of gear arrived (found, bought, a reward) and the game judged it for what is worn: here it is
        ///     judged for every other outfit, and goes into each whose slot is "best" and whose piece it beats
        ///     (OutfitRules.Upgrades). Nothing with "upgrade automatically" off.
        /// </summary>
        /// <param name="inventoryItemId">The stack it is in, when the game knows it (empty otherwise).</param>
        /// <param name="itemId">The kind of item.</param>
        public OutfitResult Offer(SessionToken token, Guid characterId, Guid inventoryItemId, Guid itemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)state.Worn };
                if (!state.Upgrade || state.Outfits.Count == 0) return Done(character, state, result);

                var inventory = inventories.Get(character.Id);
                var pieces = Pieces(character, inventory);
                var index = pieces.FindIndex(x => x.Id == inventoryItemId && inventoryItemId != Guid.Empty);
                if (index < 0) index = pieces.FindIndex(x => x.ItemId == itemId && !x.Worn);
                OutfitPiece candidate;
                if (index >= 0) candidate = pieces[index];
                else
                {
                    // on its way to the bag (the game adds what it found in its own time): judged as the plain item
                    var item = gameData.GetItem(itemId);
                    if (item == null) return Done(character, state, result);
                    candidate = PieceOf(Guid.Empty, item, PlayerInventory.GetItemValue(item), false,
                        PlayerInventory.CanEquipItem(item, gameData.GetCharacterSkills(character.SkillsId)), true);
                }

                foreach (var outfit in state.Outfits)
                {
                    if (outfit.Skill == state.Worn) continue;
                    if (!OutfitRules.Upgrades(outfit, candidate, pieces)) continue;
                    outfit.Set(candidate.Slot, candidate.Id, candidate.ItemId, false);
                    result.Changed = true;
                }
                return Done(character, state, result);
            }
        }

        // ------------------------------------------------------------------------------------------ the server itself

        /// <summary>
        ///     After "equip best" for a character in a game ("!equip all", "!equip tank"): the role or line that was
        ///     asked for is kept with the outfit that is on (it comes back with it, and is forgotten with "!equip
        ///     all"), and the outfit's pinned pieces go back on, since "equip best" takes everything off.
        /// </summary>
        public void EquippedBest(SessionToken token, Guid characterId, string word)
        {
            var character = GetCharacter(token, characterId, asksAboutOutfits: false);
            if (character == null) return;
            // a game before 0.9.7 wears what its own "equip best" put on: nothing is changed behind it
            SessionOf(character, out var knowsOutfits);
            if (!knowsOutfits) return;
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var asked = GearOutfit.Parse(word);
                // nothing kept and nothing asked for: nothing to do, and nothing to start keeping
                if (state.IsEmpty && asked == null) return;
                EnsureWorn(character, state);
                if (state.Worn == Skill.None) return;
                var outfit = asked != null ? state.GetOrAdd(state.Worn) : state.Find(state.Worn);
                if (outfit == null) return;
                outfit.Word = asked?.Word;
                if (outfit.PinnedCount > 0)
                {
                    var inventory = inventories.Get(character.Id);
                    var pieces = Pieces(character, inventory);
                    var plan = OutfitRules.Wear(outfit, pieces, pinnedOnly: true);
                    Equip(inventory, pieces, plan, null);
                }
                Store(character, state);
            }
        }

        /// <summary>
        ///     A character joins or leaves a game: an outfit that was put on for a fight and never taken off (the
        ///     player left in the middle of a raid, the stream ended, the game crashed) goes back to the outfit worn
        ///     before it. Done before the character is sent to the joining game, so that game gets it as it is.
        /// </summary>
        public void ComeBack(Character character)
        {
            if (character == null) return;
            // most characters have no outfits: no lock and no work for them
            if (string.IsNullOrEmpty(gameData.GetUserProperty(character.UserId, ModelMapper.OutfitsKey(character.Id)))) return;
            lock (LockOf(character.Id))
            {
                var state = Load(character);
                if (state.Back == Skill.None) return;
                if (state.Back != state.Worn) Switch(character, state, state.Back, new OutfitResult());
                state.Back = Skill.None;
                Store(character, state);
            }
        }

        // ------------------------------------------------------------------------------------------------ the website
        // By character id, with no check of whose character it is: RavenNest.Blazor.Services.OutfitService is in
        // front of these and checks the signed-in user. Each tells a game the character plays in (TellGame).

        /// <summary>A character's outfits and switches, read only; never null.</summary>
        public CharacterOutfits Read(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return character == null ? new CharacterOutfits() : Load(character);
        }

        /// <summary>
        ///     Puts one of the character's items into a slot of a skill's outfit, pinned or "best". The outfit is made
        ///     if the skill has none. When that outfit is the one that is on, the piece is put on at once.
        /// </summary>
        public OutfitResult SetSlot(Guid characterId, Skill skill, RavenNest.Models.EquipmentSlot slot, Guid inventoryItemId, bool pinned)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            skill = OutfitRules.Normalize(skill);
            if (skill == Skill.None) return OutfitResult.Fail(OutfitStatus.UnknownSkill);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)skill };
                var inventory = inventories.Get(character.Id);
                var pieces = Pieces(character, inventory);
                var index = pieces.FindIndex(x => x.Id == inventoryItemId);
                if (index < 0 || pieces[index].Slot != slot || !OutfitRules.IsOutfitSlot(slot) || !pieces[index].Wearable)
                {
                    result.Status = OutfitStatus.BadItem;
                    result.Outfits = state.ToString();
                    return result;
                }

                EnsureWorn(character, state);
                var outfit = state.GetOrAdd(skill);
                outfit.Set(slot, pieces[index].Id, pieces[index].ItemId, pinned);
                if (state.Worn == skill && !pieces[index].Worn)
                {
                    var plan = new OutfitPlan();
                    plan.Equip.Add(index);
                    Equip(inventory, pieces, plan, result);
                    TellOldGame(character, pieces, plan);
                }
                return Done(character, state, result, tellGame: true);
            }
        }

        /// <summary>Takes a slot out of a skill's outfit: nothing is kept for it, and what is worn there stays when the outfit goes on.</summary>
        public OutfitResult ClearSlot(Guid characterId, Skill skill, RavenNest.Models.EquipmentSlot slot)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            skill = OutfitRules.Normalize(skill);
            if (skill == Skill.None) return OutfitResult.Fail(OutfitStatus.UnknownSkill);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)skill };
                var outfit = state.Find(skill);
                if (outfit == null || !outfit.Clear(slot))
                {
                    result.Outfits = state.ToString();
                    return result;
                }
                return Done(character, state, result, tellGame: true);
            }
        }

        /// <summary>
        ///     Sets the pet that goes out with a skill's outfit; an empty id for none (the pet then stays as it is).
        ///     When that outfit is the one that is on, the pet goes out at once.
        /// </summary>
        public OutfitResult SetPet(Guid characterId, Skill skill, Guid petId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            skill = OutfitRules.Normalize(skill);
            if (skill == Skill.None) return OutfitResult.Fail(OutfitStatus.UnknownSkill);
            lock (LockOf(characterId))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)skill };
                if (petId != Guid.Empty)
                {
                    var pet = gameData.GetPet(petId);
                    if (pet == null || pet.CharacterId != characterId || string.IsNullOrEmpty(pet.Species) || RavenNest.Combat.PetEggs.IsUnhatched(pet.Level))
                    {
                        result.Status = OutfitStatus.BadPet;
                        result.Outfits = state.ToString();
                        return result;
                    }
                }

                EnsureWorn(character, state);
                var outfit = state.GetOrAdd(skill);
                outfit.Pet = petId;
                if (state.Worn == skill) SendOutPet(character, outfit, result);
                return Done(character, state, result, tellGame: true);
            }
        }

        /// <summary>Puts a skill's outfit on now, whatever is trained, as "!outfit attack" does.</summary>
        public OutfitResult WearNow(Guid characterId, Skill skill)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            return WearNow(character, OutfitRules.Normalize(skill), tellGame: true);
        }

        /// <summary>The two switches; null leaves one as it is.</summary>
        public OutfitResult SetSwitches(Guid characterId, bool? follow, bool? upgrade)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return OutfitResult.Fail(OutfitStatus.Error);
            return SetSwitches(character, follow, upgrade, true);
        }

        // ------------------------------------------------------------------------------------------------- the work

        private OutfitResult WearNow(Character character, Skill skill, bool tellGame = false)
        {
            if (skill == Skill.None) return OutfitResult.Fail(OutfitStatus.UnknownSkill);
            lock (LockOf(character.Id))
            {
                var state = Load(character);
                var result = new OutfitResult { Skill = (int)skill };
                EnsureWorn(character, state);
                if (state.Find(skill) == null && state.Worn != skill)
                {
                    // nothing was ever worn for it: nothing to put on, and what is worn stays the other skill's
                    result.Status = OutfitStatus.NoOutfit;
                    result.Outfits = state.ToString();
                    return result;
                }
                var plan = Switch(character, state, skill, result);
                state.Back = Skill.None;
                if (tellGame && plan != null) TellOldGame(character, plan.Item1, plan.Item2);
                return Done(character, state, result, tellGame);
            }
        }

        private OutfitResult SetSwitches(Character character, bool? follow, bool? upgrade, bool tellGame)
        {
            lock (LockOf(character.Id))
            {
                var state = Load(character);
                if (follow != null && follow.Value && !state.Follow)
                {
                    // on again: what is worn is the outfit of what is trained now (nothing changed while it was off)
                    var task = gameData.GetCharacterState(character.StateId);
                    var trained = OutfitRules.SkillOf(task?.Task, task?.TaskArgument);
                    if (trained != Skill.None) state.Worn = trained;
                    state.Back = Skill.None;
                }
                if (follow != null) state.Follow = follow.Value;
                if (upgrade != null) state.Upgrade = upgrade.Value;
                return Done(character, state, new OutfitResult { Skill = (int)state.Worn }, tellGame);
            }
        }

        /// <summary>
        ///     Changes the outfit that is on: what is worn is kept as the outfit that was on (OutfitRules.Remember),
        ///     then the target's pieces and pet go on (OutfitRules.Wear). A skill with no outfit keeps what is worn.
        ///     When the target is on already only its pinned pieces are put back.
        /// </summary>
        /// <returns>The pieces and the plan, when an outfit was put on; null otherwise.</returns>
        private Tuple<List<OutfitPiece>, OutfitPlan> Switch(Character character, CharacterOutfits state, Skill target, OutfitResult result)
        {
            var inventory = inventories.Get(character.Id);
            var pieces = Pieces(character, inventory);
            var same = state.Worn == target;
            if (!same && state.Worn != Skill.None)
            {
                OutfitRules.Remember(state.GetOrAdd(state.Worn), pieces, BattlePetManager.ActiveOf(gameData, character.Id));
            }

            Tuple<List<OutfitPiece>, OutfitPlan> done = null;
            var outfit = state.Find(target);
            if (outfit != null)
            {
                var plan = OutfitRules.Wear(outfit, pieces, pinnedOnly: same);
                Equip(inventory, pieces, plan, result);
                if (plan.Gone.Count > 0)
                {
                    result.Gone = plan.Gone.ToArray();
                    result.Filled = plan.Filled.ToArray();
                }
                if (!same) SendOutPet(character, outfit, result);
                done = Tuple.Create(pieces, plan);
            }

            state.Worn = target;
            result.Skill = (int)target;
            return done;
        }

        private static void Equip(PlayerInventory inventory, List<OutfitPiece> pieces, OutfitPlan plan, OutfitResult result)
        {
            foreach (var index in plan.Equip)
            {
                var item = inventory.Get(pieces[index].Id);
                if (item.IsNull()) continue;
                if (inventory.EquipItem(item) && result != null) result.Changed = true;
            }
        }

        // the outfit's pet goes out the way "!pets use" sends one; a pet that is gone leaves the pet as it is
        private void SendOutPet(Character character, Outfit outfit, OutfitResult result)
        {
            if (outfit.Pet == Guid.Empty) return;
            if (BattlePetManager.ActiveOf(gameData, character.Id) == outfit.Pet) return;
            var status = BattlePetManager.SendOut(gameData, inventories, character.Id, outfit.Pet);
            if (status == PetResultStatus.Ok) result.Pets = BattlePetManager.StableOf(gameData, character.Id);
            else outfit.Pet = Guid.Empty;
        }

        // an outfit is on from the first moment; before the game said which, it is the one of the task the server has
        private void EnsureWorn(Character character, CharacterOutfits state)
        {
            if (state.Worn != Skill.None) return;
            var task = gameData.GetCharacterState(character.StateId);
            state.Worn = OutfitRules.SkillOf(task?.Task, task?.TaskArgument);
        }

        private List<OutfitPiece> Pieces(Character character, PlayerInventory inventory)
        {
            var pieces = new List<OutfitPiece>();
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            foreach (var stack in inventory.GetAllItems())
            {
                if (stack.IsNull() || stack.Item == null || stack.Amount <= 0) continue;
                if (!OutfitRules.IsOutfitSlot(OutfitRules.SlotOf((ItemType)stack.Item.Type))) continue;
                pieces.Add(PieceOf(stack.Id, stack.Item, inventory.GetItemValue(stack), stack.Equipped,
                    skills != null && PlayerInventory.CanEquipItem(stack.Item, skills), PlayerInventory.CanBeStacked(stack)));
            }
            return pieces;
        }

        private static OutfitPiece PieceOf(Guid id, DataModels.Item item, int classic, bool worn, bool wearable, bool plain)
        {
            return new OutfitPiece
            {
                Id = id,
                ItemId = item.Id,
                Type = (ItemType)item.Type,
                Category = (ItemCategory)item.Category,
                Material = (ItemMaterial)item.Material,
                Classic = classic,
                Worn = worn,
                Wearable = wearable,
                Plain = plain,
                Name = item.Name,
            };
        }

        private CharacterOutfits Load(Character character)
        {
            return CharacterOutfits.Parse(gameData.GetUserProperty(character.UserId, ModelMapper.OutfitsKey(character.Id)));
        }

        private void Store(Character character, CharacterOutfits state)
        {
            var key = ModelMapper.OutfitsKey(character.Id);
            var text = state.ToString();
            var kept = gameData.GetUserProperty(character.UserId, key) ?? string.Empty;
            if (text != kept) gameData.SetUserProperty(character.UserId, key, text);
        }

        private OutfitResult Done(Character character, CharacterOutfits state, OutfitResult result, bool tellGame = false)
        {
            Store(character, state);
            result.Outfits = state.ToString();
            if (tellGame) TellGame(character, result);
            return result;
        }

        // A change made on the website reaches the game the character plays in. Only a game that knows outfits is
        // told (the session's client version): an older one has no use for it, and gets gear changes the old way.
        private void TellGame(Character character, OutfitResult result)
        {
            var session = SessionOf(character, out var knowsOutfits);
            if (session == null || !knowsOutfits) return;
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.OutfitsChanged, session, new OutfitsChanged
            {
                CharacterId = character.Id,
                Outfits = result.Outfits,
                Pets = result.Pets,
            }));
        }

        // A game before 0.9.7 hears of gear put on from the website the way it always did: item by item.
        private void TellOldGame(Character character, List<OutfitPiece> pieces, OutfitPlan plan)
        {
            if (plan == null || plan.Equip.Count == 0) return;
            var session = SessionOf(character, out var knowsOutfits);
            if (session == null || knowsOutfits) return;
            foreach (var index in plan.Equip)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemEquip, session, new ItemEquip
                {
                    PlayerId = character.Id,
                    InventoryItemId = pieces[index].Id,
                    IsEquipped = true,
                }));
            }
        }

        private DataModels.GameSession SessionOf(Character character, out bool knowsOutfits)
        {
            knowsOutfits = false;
            if (character.UserIdLock == null) return null;
            var session = gameData.GetSessionByUserId(character.UserIdLock.Value);
            if (session == null) return null;
            var version = gameData.GetSessionState(session.Id)?.ClientVersion;
            knowsOutfits = askingSessions.ContainsKey(session.Id) || (GameVersion.TryParse(version, out var parsed) && parsed >= FirstGameVersion);
            return session;
        }

        private Character GetCharacter(SessionToken token, Guid characterId, bool asksAboutOutfits = true)
        {
            if (token == null) return null;
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            if (asksAboutOutfits) askingSessions[session.Id] = true;
            return gameData.GetActiveSessionCharacters(session)?.FirstOrDefault(x => x.Id == characterId);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;
using Character = RavenNest.DataModels.Character;
using ItemCategory = RavenNest.DataModels.ItemCategory;
using Item = RavenNest.DataModels.Item;
using ClanSkill = RavenNest.DataModels.ClanSkill;
using RavenNest.BusinessLogic.Extensions;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Battle pets on the server (docs/battle-pets-design.md in the game repo): the stable kept in the Pet table,
    ///     taming (the game rolls the chance on each kill of the creature being tamed and asks for the pet here), pet items
    ///     woken to fight, which pet is out, names, skill slots, evolving and experience. The species and rules are
    ///     RavenNest.Combat.BattlePets, shared with the game and the website.
    /// </summary>
    /// <summary>A gatherer pet's help while its owner gathers (BattlePetManager.GatherBonusOf).</summary>
    public struct PetGatherBonus
    {
        public double ExtraResource;
        public double RareDrop;
        public double BonusItem;
    }

    public class BattlePetManager
    {
        /// <summary>A character tames at most one creature in this many seconds: a check on the game's rolls.</summary>
        public const double SecondsBetweenTames = 20;

        /// <summary>The most experience one report may carry: a pet's share of a few minutes of play.</summary>
        public const double MaxExperiencePerReport = 50_000_000;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly ILogger<BattlePetManager> logger;
        private readonly ConcurrentDictionary<Guid, DateTime> lastTame = new ConcurrentDictionary<Guid, DateTime>();
        private readonly object mutex = new object();

        public BattlePetManager(GameData gameData, PlayerInventoryProvider inventories, ILogger<BattlePetManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.logger = logger;
        }

        public PetResult List(SessionToken token, Guid characterId)
        {
            var character = GetCharacter(token, characterId);
            return character == null ? PetResult.Fail(PetResultStatus.Error) : Ok(character, null);
        }

        /// <summary>The stable of any character, for the website.</summary>
        public BattlePet[] Stable(Guid characterId) => gameData.GetPets(characterId).Where(IsBattlePet).OrderByDescending(p => p.Active).ThenByDescending(p => p.Level).Select(Map).ToArray();

        public PetResult Tame(SessionToken token, Guid characterId, string speciesId, Guid treatItemId = default)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            var species = BattlePets.GetSpecies(speciesId);
            if (species == null || species.Source != PetSource.Tamed) return PetResult.Fail(PetResultStatus.NoSuchSpecies);

            var taming = TamingLevel(character);
            if (taming < species.TameLevel) return PetResult.Fail(PetResultStatus.TamingTooLow, species.TameLevel.ToString());

            lock (mutex)
            {
                if (lastTame.TryGetValue(characterId, out var last) && (DateTime.UtcNow - last).TotalSeconds < SecondsBetweenTames)
                    return PetResult.Fail(PetResultStatus.TooSoon);
                if (Count(characterId) >= StableSize(character)) return PetResult.Fail(PetResultStatus.StableFull, StableSize(character).ToString());

                lastTame[characterId] = DateTime.UtcNow;
                if (treatItemId != Guid.Empty) EatTreat(character, species, treatItemId);
                var pet = Create(character, species, PetSource.Tamed, null);
                return Ok(character, pet);
            }
        }

        /// <summary>A starter pet (fox, bear or fawn) for a character whose stable is empty.</summary>
        public PetResult Starter(SessionToken token, Guid characterId, string speciesId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            var species = BattlePets.FindStarter(speciesId);
            if (species == null) return PetResult.Fail(PetResultStatus.NoSuchSpecies);
            lock (mutex)
            {
                if (Count(characterId) > 0) return PetResult.Fail(PetResultStatus.StableFull, "starter");
                var pet = Create(character, species, PetSource.Egg, null);
                return Ok(character, pet);
            }
        }

        /// <summary>A pet item the character has, woken to fight: one battle pet per kind of item.</summary>
        public PetResult Wake(SessionToken token, Guid characterId, Guid itemId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            var item = gameData.GetItem(itemId);
            if (item == null) return PetResult.Fail(PetResultStatus.NoSuchItem);
            if (item.Category != (int)ItemCategory.Pet) return PetResult.Fail(PetResultStatus.NotAPetItem);
            var species = BattlePets.SpeciesForItem(item.GenericPrefab) ?? BattlePets.SpeciesForItem(item.Name);
            if (species == null) return PetResult.Fail(PetResultStatus.NotAPetItem);
            if (!Has(character, itemId)) return PetResult.Fail(PetResultStatus.NoSuchItem);

            lock (mutex)
            {
                var awake = gameData.GetPets(characterId).FirstOrDefault(p => IsBattlePet(p) && p.ItemId == itemId);
                if (awake != null) return new PetResult { Status = PetResultStatus.AlreadyAwake, Pet = Map(awake) };
                if (Count(characterId) >= StableSize(character)) return PetResult.Fail(PetResultStatus.StableFull, StableSize(character).ToString());
                var pet = Create(character, species, PetSource.Companion, item);
                return Ok(character, pet);
            }
        }

        /// <summary>Sends a pet out (the one out before goes back), or puts the pet away (an empty id).</summary>
        public PetResult Use(SessionToken token, Guid characterId, Guid petId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            lock (mutex)
            {
                Pet chosen = null;
                if (petId != Guid.Empty)
                {
                    chosen = Find(characterId, petId);
                    if (chosen == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
                    if (chosen.ItemId != null && !Has(character, chosen.ItemId.Value)) return new PetResult { Status = PetResultStatus.ItemMissing, Pet = Map(chosen) };
                }

                foreach (var p in gameData.GetPets(characterId))
                {
                    if (IsBattlePet(p)) p.Active = p == chosen;
                }
                return Ok(character, chosen);
            }
        }

        public PetResult Rename(SessionToken token, Guid characterId, Guid petId, string name)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            var clean = CleanName(name);
            if (clean == null) return PetResult.Fail(PetResultStatus.BadName);
            var pet = Find(characterId, petId);
            if (pet == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
            pet.Name = clean;
            return Ok(character, pet);
        }

        /// <summary>Puts a learned skill in a slot (0 based), or clears it; a skill already in another slot moves.</summary>
        public PetResult Slot(SessionToken token, Guid characterId, Guid petId, int slot, string skillId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            lock (mutex)
            {
                var pet = Find(characterId, petId);
                if (pet == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
                var species = BattlePets.GetSpecies(pet.Species);
                if (species == null) return PetResult.Fail(PetResultStatus.NoSuchSpecies);
                var count = BattlePets.SkillSlots(TamingLevel(character), pet.Stage);
                if (slot < 0 || slot >= count) return PetResult.Fail(PetResultStatus.NoSuchSlot, count.ToString());

                var slots = Slots(pet, count);
                if (!string.IsNullOrWhiteSpace(skillId))
                {
                    var skill = BattlePets.FindSkill(skillId);
                    if (skill == null || !species.Skills.Contains(skill.Id, StringComparer.OrdinalIgnoreCase)) return PetResult.Fail(PetResultStatus.NoSuchSkill);
                    if (!species.SkillsAt(pet.Level, pet.Stage).Any(s => s.Id == skill.Id)) return PetResult.Fail(PetResultStatus.NotLearned, skill.Name + ":" + skill.LearnLevel);
                    for (var i = 0; i < slots.Length; ++i) if (slots[i] == skill.Id) slots[i] = "";
                    slots[slot] = skill.Id;
                }
                else
                {
                    slots[slot] = "";
                }
                pet.Slots = string.Join(",", slots);
                return Ok(character, pet);
            }
        }

        public PetResult Evolve(SessionToken token, Guid characterId, Guid petId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            lock (mutex)
            {
                var pet = Find(characterId, petId);
                if (pet == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
                if (!BattlePets.CanEvolve(pet.Stage, pet.Level, TamingLevel(character), out var missing))
                    return new PetResult { Status = PetResultStatus.CannotEvolve, Pet = Map(pet), Missing = missing };

                var species = BattlePets.GetSpecies(pet.Species);
                var oldName = species?.NameAt(pet.Stage);
                pet.Stage++;
                // a pet still called by its kind's name takes the new one
                if (species != null && string.Equals(pet.Name, oldName, StringComparison.OrdinalIgnoreCase)) pet.Name = species.NameAt(pet.Stage);
                FillSlots(pet, species, TamingLevel(character));
                return Ok(character, pet);
            }
        }

        public PetResult Release(SessionToken token, Guid characterId, Guid petId)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            lock (mutex)
            {
                var pet = Find(characterId, petId);
                if (pet == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
                var mapped = Map(pet);
                gameData.Remove(pet);
                var result = Ok(character, null);
                result.Pet = mapped;
                return result;
            }
        }

        /// <summary>Experience the pet earned while out: levels up to the owner's Taming level, new skills fill empty slots.</summary>
        public PetResult AddExperience(SessionToken token, Guid characterId, Guid petId, double experience)
        {
            var character = GetCharacter(token, characterId) ?? JustLeft(token, characterId);
            if (character == null) return PetResult.Fail(PetResultStatus.Error);
            if (double.IsNaN(experience) || experience <= 0) return PetResult.Fail(PetResultStatus.Error);
            lock (mutex)
            {
                var pet = Find(characterId, petId);
                if (pet == null) return PetResult.Fail(PetResultStatus.NoSuchPet);
                var cap = BattlePets.LevelCap(TamingLevel(character));
                var gained = 0;
                experience = Math.Min(experience, MaxExperiencePerReport);
                // the clan's Menagerie: more for the pet, and a share for the clan
                var menagerie = MenagerieOf(character);
                if (menagerie != null)
                {
                    experience *= 1 + Menagerie.ExperienceBonus(menagerie.Level);
                    GrowMenagerie(menagerie, experience * Menagerie.ClanShare);
                }
                var exp = pet.Experience + (long)experience;
                while (pet.Level < cap && pet.Level < GameMath.MaxLevel)
                {
                    var need = (long)GameMath.ExperienceForLevel(pet.Level + 1);
                    if (exp < need) break;
                    exp -= need;
                    pet.Level++;
                    gained++;
                }
                // at the cap, the experience is kept and spent once Taming is higher: the game saves Taming's level a
                // little after it reports the pet's experience, so a pet at the cap would otherwise lose some every time
                pet.Experience = Math.Max(0, exp);
                if (gained > 0) FillSlots(pet, BattlePets.GetSpecies(pet.Species), TamingLevel(character));
                return new PetResult { Status = PetResultStatus.Ok, Pet = Map(pet), LevelsGained = gained, StableSize = StableSize(character) };
            }
        }

        // ---------------------------------------------------------------- Menagerie, the clan skill

        /// <summary>The character's clan's Menagerie, or null (no clan, or the clan is below its level).</summary>
        private ClanSkill MenagerieOf(Character character)
        {
            var membership = gameData.GetClanMembership(character.Id);
            if (membership == null) return null;
            var skill = gameData.GetSkills().FirstOrDefault(x => x.Name == Menagerie.Name);
            if (skill == null) return null;
            return gameData.GetClanSkills(membership.ClanId)?.FirstOrDefault(x => x.SkillId == skill.Id);
        }

        private static void GrowMenagerie(ClanSkill skill, double experience)
        {
            if (experience <= 0 || skill.Level >= GameMath.MaxLevel) return;
            skill.Experience = Math.Floor(skill.Experience + experience);
            var next = GameMath.ExperienceForLevel(skill.Level + 1);
            while (skill.Experience >= next && skill.Level < GameMath.MaxLevel)
            {
                skill.Experience -= next;
                ++skill.Level;
                next = GameMath.ExperienceForLevel(skill.Level + 1);
            }
        }

        /// <summary>The stable's places: Taming's, and the clan's Menagerie's.</summary>
        public int StableSize(Character character) =>
            BattlePets.StableSize(TamingLevel(character)) + Menagerie.ExtraStablePlaces(MenagerieOf(character)?.Level ?? 0);

        /// <summary>The treat that helped is eaten: one of it, if it is one the species loves and the character has it.</summary>
        private void EatTreat(Character character, PetSpecies species, Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            if (item == null || !species.Treats.Any(t => string.Equals(t, item.Name, StringComparison.OrdinalIgnoreCase))) return;
            var inventory = inventories.Get(character.Id);
            var stack = inventory.GetAllItems().FirstOrDefault(x => x.ItemId == itemId && !x.Equipped && x.Amount > 0);
            if (stack.IsNull()) return;
            inventory.RemoveItem(stack, 1);
        }

        // ---------------------------------------------------------------- gatherer pets

        /// <summary>
        ///     What the pet out with a character adds to its gathering (the gather skills in its slots, added up): a
        ///     chance of one more of a drop, better drop odds, and the chance of a find when nothing dropped.
        /// </summary>
        public static PetGatherBonus GatherBonusOf(GameData gameData, Guid characterId)
        {
            var pet = gameData.GetActivePet(characterId);
            if (pet == null || string.IsNullOrEmpty(pet.Species)) return default;
            var bonus = new PetGatherBonus();
            foreach (var id in (pet.Slots ?? "").Split(','))
            {
                var skill = BattlePets.GetSkill(id);
                if (skill == null) continue;
                switch (skill.Gather)
                {
                    case RavenNest.Combat.PetGatherBonus.ExtraResource: bonus.ExtraResource += skill.GatherAmount; break;
                    case RavenNest.Combat.PetGatherBonus.RareDrop: bonus.RareDrop += skill.GatherAmount; break;
                    case RavenNest.Combat.PetGatherBonus.BonusItem: bonus.BonusItem += skill.GatherAmount; break;
                }
            }
            return bonus;
        }

        // ---------------------------------------------------------------- helpers

        private Pet Create(Character character, PetSpecies species, PetSource source, Item item)
        {
            var pet = new Pet
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                Name = species.NameAt(0), // its kind's name (not "... Pet"), so evolving renames it too
                DateOfBirth = DateTime.UtcNow,
                Level = 1,
                Experience = 0,
                Health = 1,
                Happiness = 1,
                Prefab = item?.GenericPrefab ?? species.ModelAt(0),
                Species = species.Id,
                Stage = 0,
                Source = (int)source,
                ItemId = item?.Id,
                // the first pet goes straight out
                Active = !gameData.GetPets(character.Id).Any(p => IsBattlePet(p) && p.Active),
            };
            FillSlots(pet, species, TamingLevel(character));
            gameData.Add(pet);
            return pet;
        }

        /// <summary>Learned skills not in a slot go into the empty ones, in learning order.</summary>
        private static void FillSlots(Pet pet, PetSpecies species, int taming)
        {
            if (species == null) return;
            var count = BattlePets.SkillSlots(taming, pet.Stage);
            var slots = Slots(pet, count);
            foreach (var skill in species.SkillsAt(pet.Level, pet.Stage))
            {
                if (slots.Contains(skill.Id)) continue;
                var free = Array.IndexOf(slots, "");
                if (free < 0) break;
                slots[free] = skill.Id;
            }
            pet.Slots = string.Join(",", slots);
        }

        private static string[] Slots(Pet pet, int count)
        {
            var parts = (pet.Slots ?? "").Split(',').Select(s => s.Trim()).ToList();
            while (parts.Count < count) parts.Add("");
            return parts.Take(Math.Max(count, 0)).ToArray();
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var chars = name.Trim().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-' || c == '_').ToArray();
            var clean = string.Join(" ", new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (clean.Length == 0) return null;
            return clean.Length > BattlePets.MaxNameLength ? clean.Substring(0, BattlePets.MaxNameLength).Trim() : clean;
        }

        private static bool IsBattlePet(Pet p) => p != null && !string.IsNullOrEmpty(p.Species);

        private int Count(Guid characterId) => gameData.GetPets(characterId).Count(IsBattlePet);

        private Pet Find(Guid characterId, Guid petId)
        {
            var pet = gameData.GetPet(petId);
            return pet != null && pet.CharacterId == characterId && IsBattlePet(pet) ? pet : null;
        }

        private bool Has(Character character, Guid itemId) =>
            inventories.Get(character.Id).GetAllItems().Any(x => x.ItemId == itemId && x.Amount > 0);

        private int TamingLevel(Character character) => Math.Max(1, gameData.GetCharacterSkills(character.SkillsId)?.TamingLevel ?? 1);

        private PetResult Ok(Character character, Pet pet) =>
            new PetResult { Status = PetResultStatus.Ok, Pet = pet != null ? Map(pet) : null, Pets = Stable(character.Id), StableSize = StableSize(character) };

        public static BattlePet Map(Pet p) => new BattlePet
        {
            Id = p.Id,
            CharacterId = p.CharacterId,
            Name = p.Name,
            DateOfBirth = p.DateOfBirth,
            Level = p.Level,
            Experience = p.Experience,
            Prefab = p.Prefab,
            Species = p.Species,
            Stage = p.Stage,
            Slots = p.Slots,
            Source = p.Source,
            ItemId = p.ItemId,
            Active = p.Active,
        };

        /// <summary>
        ///     A character that has just left this session: a leaving player's last pet experience is sent as they go,
        ///     and the remove can be handled first (each command runs on its own), so it would be lost otherwise.
        /// </summary>
        private Character JustLeft(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            var character = gameData.GetCharacter(characterId);
            return character != null && character.UserIdLock == null && character.PrevUserIdLock == session.UserId ? character : null;
        }

        private Character GetCharacter(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            return gameData.GetActiveSessionCharacters(session)?.FirstOrDefault(x => x.Id == characterId);
        }
    }
}

using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using EquipmentSlot = RavenNest.Models.EquipmentSlot;

namespace RavenNest.Blazor.Services
{
    /// <summary>What an outfit call from a page came to.</summary>
    public sealed class OutfitChange
    {
        /// <summary>Why nothing was done (not signed in, not your character); null otherwise.</summary>
        public string Refused { get; set; }

        /// <summary>The server's answer: its status, the pieces that were gone and what filled them, the stable when a pet went out.</summary>
        public OutfitResult Result { get; set; }

        /// <summary>The character's outfits and the two switches afterwards (as they are, when refused or failed). Never null.</summary>
        public CharacterOutfits Outfits { get; set; } = new CharacterOutfits();

        public bool Ok => Refused == null && Result != null && Result.Status == OutfitStatus.Ok;
    }

    /// <summary>
    ///     Outfits per skill for the website's Equipment tab (v0.9.7, docs/outfits-design.md in the game repo): read a
    ///     character's outfits and switches, set or clear a slot, set the pet, wear an outfit now, set the switches.
    ///     Every call is checked here: signed in, and the character's owner or an administrator (the character id
    ///     arrives from the browser). The rules and the storage are OutfitManager's; a running game the character
    ///     plays in is told of every change.
    /// </summary>
    public class OutfitService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly OutfitManager outfits;

        public OutfitService(
            GameData gameData,
            OutfitManager outfits,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.outfits = outfits;
        }

        /// <summary>
        ///     The character's outfits: per skill the slots (inventory item, kind of item, pinned or best), the pet
        ///     and the role asked for with "!equip tank"; the two switches (Follow: change outfit when training
        ///     changes, Upgrade: better gear automatically); and Worn, the skill whose outfit is on. A skill that is
        ///     not in the list has no outfit yet: it keeps what is worn. Refused for anyone but the owner or an administrator.
        /// </summary>
        public OutfitChange Get(Guid characterId)
        {
            var refused = MayChange(characterId);
            return refused != null ? new OutfitChange { Refused = refused } : new OutfitChange { Outfits = outfits.Read(characterId), Result = new OutfitResult() };
        }

        /// <summary>
        ///     Puts one of the character's items (an inventory item id) into a slot of a skill's outfit, pinned or
        ///     best. The item must be the character's, belong in that slot and be wearable at its levels (BadItem
        ///     otherwise). When that outfit is on, the piece is put on at once.
        /// </summary>
        public OutfitChange SetSlot(Guid characterId, Skill skill, EquipmentSlot slot, Guid inventoryItemId, bool pinned)
        {
            return Act(characterId, () => outfits.SetSlot(characterId, skill, slot, inventoryItemId, pinned));
        }

        /// <summary>Takes a slot out of a skill's outfit: when the outfit goes on, what is worn there stays.</summary>
        public OutfitChange ClearSlot(Guid characterId, Skill skill, EquipmentSlot slot)
        {
            return Act(characterId, () => outfits.ClearSlot(characterId, skill, slot));
        }

        /// <summary>
        ///     Sets the battle pet that goes out with a skill's outfit (a pet id from the character's stable; BadPet
        ///     for an egg or someone else's), or none with an empty id. When that outfit is on, the pet goes out at once.
        /// </summary>
        public OutfitChange SetPet(Guid characterId, Skill skill, Guid petId)
        {
            return Act(characterId, () => outfits.SetPet(characterId, skill, petId));
        }

        /// <summary>
        ///     Puts a skill's outfit on now, whatever the character trains, as "!outfit attack" does. NoOutfit when
        ///     the skill has none. Pieces that were gone and what filled them are in the result (Gone, Filled).
        /// </summary>
        public OutfitChange Wear(Guid characterId, Skill skill)
        {
            return Act(characterId, () => outfits.WearNow(characterId, skill));
        }

        /// <summary>
        ///     Fills a skill's outfit with the best the character owns for it (every slot that is not pinned). Puts
        ///     nothing on, unless that outfit is the one that is on.
        /// </summary>
        public OutfitChange FillBest(Guid characterId, Skill skill)
        {
            return Act(characterId, () => outfits.FillBest(characterId, skill));
        }

        /// <summary>The two switches; null leaves one as it is.</summary>
        public OutfitChange SetSwitches(Guid characterId, bool? changeWithTraining, bool? upgradeAutomatically)
        {
            return Act(characterId, () => outfits.SetSwitches(characterId, changeWithTraining, upgradeAutomatically));
        }

        private OutfitChange Act(Guid characterId, Func<OutfitResult> action)
        {
            var refused = MayChange(characterId);
            if (refused != null) return new OutfitChange { Refused = refused };
            var result = action();
            return new OutfitChange { Result = result, Outfits = CharacterOutfits.Parse(result?.Outfits) };
        }

        // Null when the signed-in user may see and change this character's outfits, otherwise why not.
        private string MayChange(Guid characterId)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated || session.UserId == Guid.Empty)
            {
                return "You need to be signed in to do that.";
            }

            var character = gameData.GetCharacter(characterId);
            if (character == null)
            {
                return "That character is gone.";
            }

            if (character.UserId != session.UserId && !session.Administrator)
            {
                return "That character does not belong to you.";
            }

            return null;
        }
    }
}

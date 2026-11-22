using RavenNest.Blazor.Services;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

using EquipmentSlot = RavenNest.BusinessLogic.Game.EquipmentSlot;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     Outfits on the Equipment tab (phase 2; the game's docs/outfits-design.md): the row of skills is the outfit
    ///     picker. Picking a skill whose outfit is not on shows that outfit in the slots, on the figure and in the
    ///     numbers; a slot is pinned or left to "best"; an outfit is put on, gets a battle pet, and the two switches
    ///     are set. The server does all of it (IGearBackend, OutfitService behind it); the page shows its answer.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>The character's outfits as the server last gave them; null when they are not the viewer's to see.</summary>
        private CharacterOutfits outfits;
        private bool moreSkills;

        private bool OutfitsKnown => outfits != null;

        private void LoadOutfits()
        {
            outfits = null;
            if (!CanAct || player == null) return;
            var answer = Server.GetOutfits(player.Id);
            if (answer != null && answer.Refused == null) outfits = answer.Outfits;
        }

        /// <summary>What the character trains, as outfits count skills ("all" is Health, and gathering has outfits too).</summary>
        private Skill TrainedOutfitSkill => OutfitRules.SkillOf(player?.State?.Task, player?.State?.TaskArgument);

        /// <summary>The skill whose outfit is on: what the server says, or what is trained before anything was kept.</summary>
        private Skill WornOutfitSkill => outfits != null && outfits.Worn != Skill.None ? outfits.Worn : TrainedOutfitSkill;

        /// <summary>The skill the outfit actions are about: the picked one, or the one that is on.</summary>
        private Skill OutfitSkill => dressFor.HasValue ? OutfitRules.Normalize(dressFor.Value) : WornOutfitSkill;

        private Outfit ShownOutfit => outfits?.Find(OutfitSkill);

        /// <summary>
        ///     A skill is picked in the row (not "Now"): the page is that skill's outfit, its loadout. What is done here
        ///     is saved to the outfit; gear only goes on when that outfit is the one that is on, or with "Put it on".
        ///     "Now" (no skill picked) is the gear worn this moment, and what is done there goes on at once (2026-11-22:
        ///     the owner picked Strength, pressed "equip best for Strength", and it changed what was worn while the
        ///     Strength outfit stayed as it was).
        /// </summary>
        private bool SkillTab => outfits != null && dressFor.HasValue;

        /// <summary>A skill's outfit is looked at: the slots, the figure and the numbers show it.</summary>
        private bool OutfitView => SkillTab && ShownOutfit != null;

        /// <summary>The picked skill has no outfit yet: the slots show what is worn, and the outfit can be made.</summary>
        private bool NoOutfitYet => SkillTab && OutfitSkill != Skill.None && ShownOutfit == null;

        /// <summary>The outfit looked at is the one that is on: what changes in it changes what is worn too.</summary>
        private bool OutfitIsOn => OutfitView && OutfitSkill == WornOutfitSkill;

        private static RavenNest.Models.EquipmentSlot Wire(EquipmentSlot slot) => (RavenNest.Models.EquipmentSlot)(int)slot;

        private static bool IsOutfitSlot(EquipmentSlot slot) => OutfitRules.IsOutfitSlot(Wire(slot));

        /// <summary>The piece an outfit's slot means: that very stack, or any of its kind (a plain piece changes its id when it is taken off).</summary>
        private Entry PieceOf(OutfitSlot entry)
        {
            if (entry == null) return null;
            if (entry.Item != Guid.Empty && byId.TryGetValue(entry.Item, out var exact) && exact.Stack.ItemId == entry.ItemId) return exact;
            Entry found = null;
            foreach (var e in entries)
            {
                if (e.Stack.ItemId != entry.ItemId || (int)e.Slot != (int)entry.Slot) continue;
                if (found == null || (e.Stack.Equipped && !found.Stack.Equipped)) found = e;
            }
            return found;
        }

        /// <summary>What a slot shows: the worn piece, or the looked-at outfit's piece for it (a slot the outfit leaves alone keeps what is worn).</summary>
        private Entry Shown(EquipmentSlot slot)
        {
            worn.TryGetValue(slot, out var wornNow);
            if (!OutfitView) return wornNow;
            var entry = ShownOutfit.Get(Wire(slot));
            return entry == null ? wornNow : PieceOf(entry);
        }

        /// <summary>Every piece that shows as worn: on the figure and in the numbers.</summary>
        private List<Entry> ShownPieces()
        {
            if (!OutfitView) return entries.Where(x => x.Stack.Equipped).ToList();
            var list = new List<Entry>();
            foreach (var slot in GearRules.All)
            {
                var e = Shown(slot.Slot);
                if (e != null) list.Add(e);
            }
            return list;
        }

        /// <summary>The outfit a slot's pin mark is read from: the one looked at, or the one that is on.</summary>
        private Outfit MarksOutfit => OutfitView ? ShownOutfit : outfits?.Find(WornOutfitSkill);

        private bool IsPinned(EquipmentSlot slot) => (!dressFor.HasValue || OutfitView || OutfitSkill == WornOutfitSkill) && (MarksOutfit?.IsPinned(Wire(slot)) ?? false);

        /// <summary>The looked-at outfit names a piece for the slot that the character no longer has.</summary>
        private bool IsGone(EquipmentSlot slot) => OutfitView && ShownOutfit.Get(Wire(slot)) != null && Shown(slot) == null;

        /// <summary>The looked-at outfit leaves the slot alone: what is worn stays there.</summary>
        private bool StaysAsWorn(EquipmentSlot slot) => OutfitView && IsOutfitSlot(slot) && ShownOutfit.Get(Wire(slot)) == null;

        /// <summary>The skills in the picker: the fighting ones always, the others once they have an outfit, are trained or are asked for.</summary>
        private List<Skill> PickerSkills()
        {
            if (!OutfitsKnown) return GearRules.DressFor.ToList();
            var list = new List<Skill>();
            foreach (var skill in GearRules.OutfitSkills)
            {
                if (OutfitRules.IsCombat(skill) || moreSkills || outfits.Find(skill) != null || TrainedOutfitSkill == skill || (dressFor.HasValue && OutfitRules.Normalize(dressFor.Value) == skill)) list.Add(skill);
            }
            return list;
        }

        private bool HasMoreSkills => OutfitsKnown && GearRules.OutfitSkills.Any(s => !OutfitRules.IsCombat(s));

        private bool IsTrainedSkill(Skill skill) => OutfitsKnown ? TrainedOutfitSkill == skill : Trained == skill;

        private string OutfitSummary(Outfit outfit)
        {
            if (outfit == null) return null;
            var pinned = outfit.PinnedCount;
            return Plural(outfit.Slots.Count, "piece") + (pinned > 0 ? ", " + pinned + " pinned" : "");
        }

        private IReadOnlyList<BattlePet> Stable => player?.BattlePets ?? Array.Empty<BattlePet>();

        // ---- What the buttons do -----------------------------------------------------------------------------------------

        /// <summary>Takes the server's answer to an outfit call: the outfits as they are now, the character again when gear went on, and why when nothing was done.</summary>
        private bool TakeOutfit(OutfitChange change, string failed)
        {
            if (change == null)
            {
                Say(failed, true);
                return false;
            }

            if (change.Refused != null)
            {
                Say(change.Refused, true);
                return false;
            }

            outfits = change.Outfits ?? outfits;
            // gear may have gone on: the character as the server has it now
            var now = Server.GetPlayer(player.Id);
            if (now != null)
            {
                player = now;
                coins = Server.GetCoins(player);
            }
            Rebuild();

            switch (change.Result?.Status ?? OutfitStatus.Error)
            {
                case OutfitStatus.Ok: return true;
                case OutfitStatus.BadItem: Say("That piece cannot go into the outfit: it is no longer this character's, or its level needs are not met.", true); return false;
                case OutfitStatus.NoOutfit: Say("There is no outfit for " + GearRules.SkillName(OutfitSkill) + " yet.", true); return false;
                case OutfitStatus.BadPet: Say("That pet cannot go with an outfit: it is an egg, or no longer this character's.", true); return false;
                case OutfitStatus.UnknownSkill: Say("That skill has no outfit.", true); return false;
                default: Say(failed, true); return false;
            }
        }

        private void PinSlot(EquipmentSlot slot, bool pinned)
        {
            var e = Shown(slot);
            var skill = OutfitSkill;
            if (e == null || skill == Skill.None) return;
            notice = null;
            if (!TakeOutfit(Server.SetOutfitSlot(player.Id, skill, slot, e.Stack.Id, pinned), "The outfit was not changed. Try again in a moment.")) return;
            Say(pinned
                ? e.Name + " is pinned for " + GearRules.SkillName(skill) + ": nothing replaces it."
                : e.Name + " is no longer pinned: a better piece may take its place.");
        }

        /// <summary>A piece from the bag into the outfit that is looked at (or the one that is on), pinned: it is the player's own pick.</summary>
        private void AddToOutfit(Entry e)
        {
            var skill = OutfitSkill;
            if (e == null || skill == Skill.None) return;
            var slot = e.Slot;
            var name = e.Name;
            notice = null;
            if (!TakeOutfit(Server.SetOutfitSlot(player.Id, skill, slot, e.Stack.Id, true), name + " was not added to the outfit. Try again in a moment.")) return;
            Say(name + " is pinned in the " + GearRules.SkillName(skill) + " outfit.");
            pickedSlot = slot;
            pickedId = Shown(slot)?.Stack.Id;
            detailOpen = false;
        }

        private void ClearOutfitSlot(EquipmentSlot slot)
        {
            var skill = OutfitSkill;
            var label = GearRules.Get(slot)?.Label ?? slot.ToString();
            notice = null;
            if (!TakeOutfit(Server.ClearOutfitSlot(player.Id, skill, slot), "The outfit was not changed. Try again in a moment.")) return;
            Say(label + " is out of the " + GearRules.SkillName(skill) + " outfit: what is worn there stays.");
            ClearPick();
        }

        private void WearOutfit()
        {
            var skill = OutfitSkill;
            notice = null;
            var change = Server.WearOutfit(player.Id, skill);
            if (!TakeOutfit(change, "The outfit was not put on. Try again in a moment.")) return;
            var gone = change.Result?.Gone?.Length ?? 0;
            Say("The " + GearRules.SkillName(skill) + " outfit is on." + (gone > 0 ? " " + Plural(gone, "piece") + " of it " + (gone == 1 ? "is" : "are") + " gone: the best you own went in." : ""));
            ClearPick();
        }

        /// <summary>Fills the picked skill's outfit with the best owned for it (slots that are not pinned). Nothing worn changes unless it is the outfit that is on.</summary>
        private void FillOutfitBest()
        {
            var skill = OutfitSkill;
            if (skill == Skill.None) return;
            var name = GearRules.SkillName(skill);
            notice = null;
            if (!TakeOutfit(Server.FillOutfitBest(player.Id, skill), "The " + name + " outfit was not made. Try again in a moment.")) return;
            if (outfits?.Find(skill) == null)
            {
                Say("You have nothing to wear for " + name + " yet, so no outfit was made.", true);
                return;
            }
            Say(skill == WornOutfitSkill
                ? "The " + name + " outfit has your best gear for " + name + ", and it is on."
                : "The " + name + " outfit has your best gear for " + name + ". What you wear did not change: it goes on when you train " + name + ", or with the button below.");
            ClearPick();
        }

        private void SetOutfitPet(string value)
        {
            var skill = OutfitSkill;
            if (skill == Skill.None) return;
            Guid.TryParse(value, out var petId);
            notice = null;
            if (!TakeOutfit(Server.SetOutfitPet(player.Id, skill, petId), "The outfit's pet was not changed. Try again in a moment.")) return;
            var pet = Stable.FirstOrDefault(x => x.Id == petId);
            Say(pet == null
                ? "No pet goes with the " + GearRules.SkillName(skill) + " outfit: the pet that is out stays."
                : pet.Name + " goes out with the " + GearRules.SkillName(skill) + " outfit.");
        }

        private void SetFollow(bool on)
        {
            if (!TakeOutfit(Server.SetOutfitSwitches(player.Id, on, null), "The switch was not changed. Try again in a moment.")) return;
            Say(on ? "The outfit changes when training changes." : "The outfit stays as it is when training changes.");
        }

        private void SetUpgrade(bool on)
        {
            if (!TakeOutfit(Server.SetOutfitSwitches(player.Id, null, on), "The switch was not changed. Try again in a moment.")) return;
            Say(on ? "Better gear is put on by itself (pinned slots are left alone)." : "Better gear waits in the bag until you put it on.");
        }
    }
}

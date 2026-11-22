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
    ///     picker. "Now" is the gear worn this moment; every skill after it is that skill's outfit. Picking a skill
    ///     shows its outfit in the slots, on the figure and in the numbers (or, for a skill with none yet, what making
    ///     one would pick, as "Proposed"); a slot is pinned or left to "best"; an outfit is put on, gets a battle pet,
    ///     is copied, and the two switches are set. The server does all of it (IGearBackend, OutfitService behind
    ///     it); the page shows its answer.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>The character's outfits as the server last gave them; null when they are not the viewer's to see.</summary>
        private CharacterOutfits outfits;
        private bool moreSkills;

        /// <summary>For a picked skill with no outfit: what "make an outfit from my best gear" would pick, kept nowhere.</summary>
        private Outfit proposal;

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

        /// <summary>The outfit that is on; null when that skill has none kept.</summary>
        private Outfit WornOutfit => outfits?.Find(WornOutfitSkill);

        /// <summary>The outfit that is on was put on by hand: it is not the one of the skill trained.</summary>
        private bool WornByHand => OutfitsKnown && WornOutfitSkill != Skill.None && TrainedOutfitSkill != Skill.None && WornOutfitSkill != TrainedOutfitSkill;

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

        /// <summary>The picked skill has no outfit yet.</summary>
        private bool NoOutfitYet => SkillTab && OutfitSkill != Skill.None && ShownOutfit == null;

        /// <summary>The picked skill has no outfit yet and the slots show what making one would pick (nothing saved).</summary>
        private bool ProposalView => NoOutfitYet && proposal != null && proposal.Slots.Count > 0;

        /// <summary>An outfit, kept or proposed, is in the slots instead of what is worn.</summary>
        private bool LooksAtOutfit => OutfitView || ProposalView;

        private Outfit LookedOutfit => OutfitView ? ShownOutfit : ProposalView ? proposal : null;

        /// <summary>The outfit looked at is the one that is on: what changes in it changes what is worn too.</summary>
        private bool OutfitIsOn => OutfitView && OutfitSkill == WornOutfitSkill;

        /// <summary>The picked skill without an outfit is the one that is on: making its outfit puts it on.</summary>
        private bool NoOutfitYetIsOn => NoOutfitYet && OutfitSkill == WornOutfitSkill;

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
            var outfit = LookedOutfit;
            if (outfit == null) return wornNow;
            var entry = outfit.Get(Wire(slot));
            return entry == null ? wornNow : PieceOf(entry);
        }

        /// <summary>The same piece, or one of the same plain kind: no difference for the eye or the numbers.</summary>
        private static bool Same(Entry a, Entry b)
        {
            if (a == null || b == null) return a == b;
            if (a.Stack.Id == b.Stack.Id) return true;
            return a.Stack.ItemId == b.Stack.ItemId && string.IsNullOrEmpty(a.Stack.Enchantment) && string.IsNullOrEmpty(b.Stack.Enchantment)
                && string.IsNullOrEmpty(a.Stack.Sockets) && string.IsNullOrEmpty(b.Stack.Sockets) && a.Stack.TransmogrificationId == null && b.Stack.TransmogrificationId == null;
        }

        /// <summary>
        ///     The looked-at outfit (kept or proposed) puts something else in this slot than what is worn: putting it on
        ///     changes the slot (the review: "did anything happen?").
        /// </summary>
        private bool Differs(EquipmentSlot slot)
        {
            var outfit = LookedOutfit;
            if (outfit == null || !IsOutfitSlot(slot) || outfit.Get(Wire(slot)) == null) return false;
            worn.TryGetValue(slot, out var wornNow);
            return !Same(Shown(slot), wornNow);
        }

        private int DiffCount => LookedOutfit == null ? 0 : GearRules.All.Count(s => Differs(s.Slot));

        /// <summary>
        ///     On "Now": the outfit that is on keeps another piece in this slot than the one worn (gear changed by hand),
        ///     so the next time it goes on that piece comes back.
        /// </summary>
        private bool Drifted(EquipmentSlot slot)
        {
            var outfit = WornOutfit;
            if (outfit == null || !IsOutfitSlot(slot)) return false;
            var entry = outfit.Get(Wire(slot));
            if (entry == null) return false;
            worn.TryGetValue(slot, out var wornNow);
            var kept = PieceOf(entry);
            return kept != null && !Same(kept, wornNow);
        }

        private List<GearSlot> DriftedSlots() => OutfitsKnown && !dressFor.HasValue ? GearRules.All.Where(s => Drifted(s.Slot)).ToList() : new List<GearSlot>();

        /// <summary>The outfit that is on keeps this piece for the slot (Now), or the looked-at outfit's piece.</summary>
        private Entry KeptIn(Outfit outfit, EquipmentSlot slot) => outfit == null ? null : PieceOf(outfit.Get(Wire(slot)));

        /// <summary>Every piece that shows as worn: on the figure and in the numbers.</summary>
        private List<Entry> ShownPieces()
        {
            if (!LooksAtOutfit) return entries.Where(x => x.Stack.Equipped).ToList();
            var list = new List<Entry>();
            foreach (var slot in GearRules.All)
            {
                var e = Shown(slot.Slot);
                if (e != null) list.Add(e);
            }
            return list;
        }

        /// <summary>The outfit a slot's pin mark is read from: the one looked at, or the one that is on.</summary>
        private Outfit MarksOutfit => LooksAtOutfit ? LookedOutfit : WornOutfit;

        /// <summary>
        ///     The slot is pinned in the outfit the page is about, to the piece shown there. On "Now" a slot whose worn
        ///     piece is not the outfit's is not "pinned" (the review: the Dragon Helmet put on over the pinned cowl read
        ///     "pinned for Healing").
        /// </summary>
        private bool IsPinned(EquipmentSlot slot)
        {
            if (SkillTab && !LooksAtOutfit) return false;
            var outfit = MarksOutfit;
            if (outfit == null || !outfit.IsPinned(Wire(slot))) return false;
            return LooksAtOutfit || !Drifted(slot);
        }

        /// <summary>The looked-at outfit names a piece for the slot that the character no longer has.</summary>
        private bool IsGone(EquipmentSlot slot) => OutfitView && ShownOutfit.Get(Wire(slot)) != null && Shown(slot) == null;

        /// <summary>The looked-at outfit leaves the slot alone: what is worn stays there.</summary>
        private bool StaysAsWorn(EquipmentSlot slot) => LooksAtOutfit && IsOutfitSlot(slot) && LookedOutfit.Get(Wire(slot)) == null;

        /// <summary>
        ///     What a bag piece is compared with: in a skill's tab the piece that outfit has in the slot (the review:
        ///     the page recommended a Dragon Helmet against the worn cowl while the Strength outfit had an Abraxas
        ///     Helmet), on "Now" the worn one. Null for an empty slot.
        /// </summary>
        private Entry Against(Entry e)
        {
            if (e == null || e.Slot == EquipmentSlot.None) return null;
            var against = SkillTab ? Shown(e.Slot) : (worn.TryGetValue(e.Slot, out var w) ? w : null);
            return against == null || against.Stack.Id == e.Stack.Id ? null : against;
        }

        /// <summary>Where the piece a bag piece is compared with is: "In the Strength outfit", "Proposed for Strength", "Worn now".</summary>
        private string AgainstLabel(Entry against)
        {
            if (against == null) return null;
            if (SkillTab && LooksAtOutfit && !StaysAsWorn(against.Slot) && Shown(against.Slot) == against)
                return ProposalView ? "Proposed for " + GearRules.SkillName(OutfitSkill) : "In the " + GearRules.SkillName(OutfitSkill) + " outfit";
            return against.Stack.Equipped ? "Worn now" : "In your bag";
        }

        /// <summary>The bag piece is in the outfit looked at (or, on "Now", in the outfit that is on but not worn).</summary>
        private bool InOutfit(Entry e)
        {
            if (e == null || e.Stack.Equipped || !IsOutfitSlot(e.Slot)) return false;
            if (SkillTab) return LooksAtOutfit && !StaysAsWorn(e.Slot) && Shown(e.Slot) == e;
            return KeptIn(WornOutfit, e.Slot) == e;
        }

        private string InOutfitTitle => SkillTab
            ? (ProposalView ? "Proposed for the " : "In the ") + GearRules.SkillName(OutfitSkill) + " outfit"
            : "In your " + GearRules.SkillName(WornOutfitSkill) + " outfit, not worn now";

        /// <summary>The skills in the picker: the fighting ones always, the others once they have an outfit, are trained or are asked for.</summary>
        private List<Skill> PickerSkills()
        {
            if (!OutfitsKnown) return GearRules.DressFor.ToList();
            var list = new List<Skill>();
            foreach (var skill in GearRules.OutfitSkills)
            {
                if (OutfitRules.IsCombat(skill) || moreSkills || outfits.Find(skill) != null || TrainedOutfitSkill == skill || WornOutfitSkill == skill || (dressFor.HasValue && OutfitRules.Normalize(dressFor.Value) == skill)) list.Add(skill);
            }
            return list;
        }

        /// <summary>
        ///     Columns for the row of skills that leave no tile alone on a row: at most <paramref name="most"/>, rows
        ///     balanced (nine tiles in one wide row of eight left the trained skill alone below, in the review).
        /// </summary>
        private static int Columns(int tiles, int most)
        {
            if (tiles <= most) return Math.Max(1, tiles);
            var rows = (tiles + most - 1) / most;
            var cols = (tiles + rows - 1) / rows;
            // still one on the last row (ten tiles in threes): one column more, if allowed by one
            if (tiles % cols == 1 && cols < most + 1 && tiles % (cols + 1) != 1) cols++;
            return cols;
        }

        private bool HasMoreSkills => OutfitsKnown && GearRules.OutfitSkills.Any(s => !OutfitRules.IsCombat(s));

        private bool IsTrainedSkill(Skill skill) => OutfitsKnown ? TrainedOutfitSkill == skill : Trained == skill;

        /// <summary>The short line under a skill's name in the row: what it is to the character, in a word or two.</summary>
        private string SkillState(Skill skill)
        {
            if (!OutfitsKnown) return IsTrainedSkill(skill) ? "Training" : null;
            var has = outfits.Find(skill) != null;
            var on = WornOutfitSkill == skill && (has || TrainedOutfitSkill == skill);
            var trained = TrainedOutfitSkill == skill;
            if (on && trained) return "On, training";
            if (on) return "On";
            if (trained) return "Training";
            return has ? "Saved" : null;
        }

        private string OutfitSummary(Outfit outfit)
        {
            if (outfit == null) return null;
            var pinned = outfit.PinnedCount;
            return Plural(outfit.Slots.Count, "piece") + (pinned > 0 ? ", " + pinned + " pinned" : "");
        }

        private IReadOnlyList<BattlePet> Stable => player?.BattlePets ?? Array.Empty<BattlePet>();

        /// <summary>The character's pieces as the outfit rules see them (for the best picks and the marks).</summary>
        private List<OutfitPiece> RulePieces(out List<Entry> of)
        {
            var pieces = new List<OutfitPiece>();
            of = new List<Entry>();
            foreach (var e in entries)
            {
                if (!IsOutfitSlot(e.Slot)) continue;
                pieces.Add(new OutfitPiece
                {
                    Id = e.Stack.Id, ItemId = e.Stack.ItemId, Type = e.Item.Type, Category = e.Item.Category, Material = e.Item.Material,
                    Classic = e.Classic, Worn = e.Stack.Equipped, Wearable = e.CanWear, Name = e.Item.Name,
                    Plain = string.IsNullOrEmpty(e.Stack.Enchantment) && string.IsNullOrEmpty(e.Stack.Sockets) && e.Stack.TransmogrificationId == null,
                });
                of.Add(e);
            }
            return pieces;
        }

        /// <summary>The outfit the page's "best" is about: the one looked at, the one that is on (Now), or the skill dressed for.</summary>
        private Outfit BestOutfit()
        {
            if (SkillTab) return ShownOutfit ?? new Outfit { Skill = OutfitSkill };
            if (OutfitsKnown && WornOutfitSkill != Skill.None) return WornOutfit ?? new Outfit { Skill = WornOutfitSkill };
            return new Outfit { Skill = OutfitRules.Normalize(EffectiveSkill) };
        }

        // ---- Words for what changed ---------------------------------------------------------------------------------

        private Dictionary<EquipmentSlot, string> SnapWorn() => GearRules.All.ToDictionary(s => s.Slot, s => worn.TryGetValue(s.Slot, out var e) ? e.Name : null);

        /// <summary>The outfit's pieces by slot; with <paramref name="orWorn"/> a slot it leaves alone has what is worn (as it would be with it on).</summary>
        private Dictionary<EquipmentSlot, string> SnapOutfit(Skill skill, bool orWorn = false)
        {
            var outfit = outfits?.Find(skill);
            return GearRules.All.ToDictionary(s => s.Slot, s =>
            {
                var entry = outfit?.Get(Wire(s.Slot));
                if (entry == null) return orWorn && worn.TryGetValue(s.Slot, out var w) ? w.Name : null;
                return PieceOf(entry)?.Name;
            });
        }

        /// <summary>"Gloves: Rune Gloves to Priest Gloves, Boots: Dragon Boots to Priest Boots, and 2 more"; empty when nothing changed.</summary>
        private static string Changes(Dictionary<EquipmentSlot, string> before, Dictionary<EquipmentSlot, string> after, int most = 3)
        {
            var parts = new List<string>();
            foreach (var slot in GearRules.All)
            {
                before.TryGetValue(slot.Slot, out var a);
                after.TryGetValue(slot.Slot, out var b);
                if (a == b) continue;
                parts.Add(slot.Label + ": " + (a == null ? "nothing to " + b : b == null ? a + " to nothing" : a + " to " + b));
            }
            if (parts.Count == 0) return string.Empty;
            var shown = string.Join(", ", parts.Take(most));
            return parts.Count > most ? shown + ", and " + (parts.Count - most) + " more" : shown;
        }

        private static int ChangeCount(Dictionary<EquipmentSlot, string> before, Dictionary<EquipmentSlot, string> after)
        {
            return GearRules.All.Count(s => (before.TryGetValue(s.Slot, out var a) ? a : null) != (after.TryGetValue(s.Slot, out var b) ? b : null));
        }

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
            var wornBefore = SnapWorn();
            notice = null;
            if (!TakeOutfit(Server.SetOutfitSlot(player.Id, skill, slot, e.Stack.Id, true), name + " was not added to the outfit. Try again in a moment.")) return;
            var wentOn = ChangeCount(wornBefore, SnapWorn()) > 0;
            Say(name + " is pinned in the " + GearRules.SkillName(skill) + " outfit." + (wentOn ? " That outfit is on, so it is worn now too." : " Nothing you wear changed."));
            pickedSlot = slot;
            pickedId = Shown(slot)?.Stack.Id;
            detailOpen = false;
        }

        /// <summary>Now: the worn piece goes into the outfit that is on, in place of the one it kept there (its pin stays).</summary>
        private void KeepInWornOutfit(EquipmentSlot slot)
        {
            var skill = WornOutfitSkill;
            if (!worn.TryGetValue(slot, out var e) || skill == Skill.None) return;
            var pinned = WornOutfit?.IsPinned(Wire(slot)) ?? false;
            notice = null;
            if (!TakeOutfit(Server.SetOutfitSlot(player.Id, skill, slot, e.Stack.Id, pinned), "The outfit was not changed. Try again in a moment.")) return;
            Say(e.Name + " is kept in the " + GearRules.SkillName(skill) + " outfit" + (pinned ? ", pinned as the piece before it was." : "."));
        }

        /// <summary>Now: the piece the outfit that is on keeps for the slot goes back on.</summary>
        private void PutKeptBack(EquipmentSlot slot)
        {
            var kept = KeptIn(WornOutfit, slot);
            if (kept == null) return;
            if (kept.Stack.Equipped) return;
            Equip(kept);
            if (notice == null) Say(kept.Name + " is back on, as your " + GearRules.SkillName(WornOutfitSkill) + " outfit has it.");
        }

        /// <summary>Now: every slot that differs from the outfit that is on is saved into it (pins stay where they were).</summary>
        private void SaveWornToOutfit()
        {
            var skill = WornOutfitSkill;
            var slots = DriftedSlots();
            var pinned = slots.Count(s => WornOutfit?.IsPinned(Wire(s.Slot)) ?? false);
            var before = SnapOutfit(skill);
            notice = null;
            if (!TakeOutfit(Server.KeepWornInOutfit(player.Id, skill), "The outfit was not changed. Try again in a moment.")) return;
            var list = Changes(before, SnapOutfit(skill));
            Say("Saved to the " + GearRules.SkillName(skill) + " outfit" + (list.Length > 0 ? ": " + list : "") + "."
                + (pinned > 0 ? " " + Plural(pinned, "pinned slot") + " stay" + (pinned == 1 ? "s" : "") + " pinned, to what you wear now." : ""));
            ClearPick();
        }

        /// <summary>Now: the outfit that is on goes back on, every piece of it.</summary>
        private void PutOutfitBack()
        {
            var skill = WornOutfitSkill;
            var before = SnapWorn();
            notice = null;
            if (!TakeOutfit(Server.PutOutfitBack(player.Id, skill), "The outfit was not put back. Try again in a moment.")) return;
            var list = Changes(before, SnapWorn());
            Say("Your " + GearRules.SkillName(skill) + " outfit is back on" + (list.Length > 0 ? ": " + list : "") + ".");
            ClearPick();
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
            var before = SnapWorn();
            notice = null;
            var change = Server.WearOutfit(player.Id, skill);
            if (!TakeOutfit(change, "The outfit was not put on. Try again in a moment.")) return;
            var gone = change.Result?.Gone?.Length ?? 0;
            var list = Changes(before, SnapWorn());
            Say("The " + GearRules.SkillName(skill) + " outfit is on" + (list.Length > 0 ? ": " + list : ", nothing worn had to change") + "."
                + (gone > 0 ? " " + Plural(gone, "piece") + " of it " + (gone == 1 ? "is" : "are") + " gone: the best you own went in." : ""));
            ClearPick();
        }

        /// <summary>Fills the picked skill's outfit with the best owned for it (slots that are not pinned). Nothing worn changes unless it is the outfit that is on.</summary>
        private void FillOutfitBest() => FillOutfitBest(OutfitSkill);

        private void FillOutfitBest(Skill skill)
        {
            if (skill == Skill.None) return;
            var name = GearRules.SkillName(skill);
            var made = outfits?.Find(skill) == null;
            var wornBefore = SnapWorn();
            var outfitBefore = SnapOutfit(skill);
            notice = null;
            if (!TakeOutfit(Server.FillOutfitBest(player.Id, skill), "The " + name + " outfit was not made. Try again in a moment.")) return;
            if (outfits?.Find(skill) == null)
            {
                Say("You have nothing to wear for " + name + " yet, so no outfit was made.", true);
                return;
            }

            var wornNow = SnapWorn();
            string text;
            if (skill == WornOutfitSkill)
            {
                var list = Changes(wornBefore, wornNow);
                text = (made ? "Your " + name + " outfit is made from your best gear and it is on" : "Equip best for " + name)
                    + (list.Length > 0 ? (made ? ": " : " changed ") + list + "." : made ? ", nothing worn had to change." : ": you already wear the best for " + name + ".");
            }
            else
            {
                var list = made ? Changes(wornNow, SnapOutfit(skill, orWorn: true)) : Changes(outfitBefore, SnapOutfit(skill));
                text = made
                    ? "The " + name + " outfit is made" + (list.Length > 0 ? ". Against what you wear: " + list : ", the same as what you wear") + "."
                    : "The " + name + " outfit is refilled" + (list.Length > 0 ? ": " + list : ": it had your best already") + ".";
                text += " What you wear did not change.";
            }
            Say(text);
            ClearPick();
        }

        /// <summary>"Start from this outfit": the outfit of one skill copied into another skill's.</summary>
        private void CopyOutfit(Skill from, Skill to)
        {
            from = OutfitRules.Normalize(from);
            to = OutfitRules.Normalize(to);
            if (from == Skill.None || to == Skill.None || from == to) return;
            var replaced = outfits?.Find(to) != null;
            notice = null;
            if (!TakeOutfit(Server.CopyOutfit(player.Id, from, to), "The outfit was not copied. Try again in a moment.")) return;
            Say("The " + GearRules.SkillName(to) + " outfit " + (replaced ? "now has" : "starts with") + " the pieces, pins and pet of the " + GearRules.SkillName(from) + " outfit."
                + (to == WornOutfitSkill ? " It is on, so it is worn now." : ""));
            overviewOpen = false;
            copyFrom = null;
            dressFor = to;
            ClearPick();
            Judge();
        }

        private void SetOutfitPet(string value)
        {
            var skill = OutfitSkill;
            if (skill == Skill.None) return;
            Guid.TryParse(value, out var petId);
            notice = null;
            if (!TakeOutfit(Server.SetOutfitPet(player.Id, skill, petId), "The outfit's battle pet was not changed. Try again in a moment.")) return;
            var pet = Stable.FirstOrDefault(x => x.Id == petId);
            Say(pet == null
                ? "No battle pet goes with the " + GearRules.SkillName(skill) + " outfit: whichever is out stays out."
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

        // ---- The big button: the same place may mean something else after a click ---------------------------------------

        // what the big button under the numbers did when it was last drawn, and since when
        private string bigMeaning;
        private DateTime bigSince = DateTime.MinValue;

        /// <summary>Notes what the big button means now; when that changed, clicks are ignored for a moment.</summary>
        private void NoteBigButton(string meaning)
        {
            if (meaning == bigMeaning) return;
            // the first drawing of the tab is no change of meaning
            bigSince = bigMeaning == null ? DateTime.MinValue : DateTime.UtcNow;
            bigMeaning = meaning;
        }

        /// <summary>
        ///     A press of the big button. Ignored for about a second after the button changed its meaning: after "Make a
        ///     Strength outfit" the same place becomes "Put the Strength outfit on now", and a quick second click put
        ///     the outfit on (the review).
        /// </summary>
        private void PressBig(Action action)
        {
            if ((DateTime.UtcNow - bigSince).TotalMilliseconds < 1000) return;
            action();
        }

        // ---- All outfits ---------------------------------------------------------------------------------------------------

        private bool overviewOpen;
        private Skill? copyFrom;
        private Skill copyTo = Skill.None;

        private void OpenOverview()
        {
            overviewOpen = true;
            copyFrom = null;
        }

        /// <summary>The number that says most about an outfit for its skill: Max heal, Power or Armor.</summary>
        private (string Label, string Value) Headline(Outfit outfit)
        {
            var sheet = SheetFor(outfit);
            if (sheet == null) return (null, null);
            switch (outfit.Skill)
            {
                case Skill.Healing: return ("Max heal", GearSheet.Number(sheet.MaxHit));
                case Skill.Defense: return ("Armor", GearSheet.Number(sheet.Armor));
                case Skill.Attack:
                case Skill.Strength:
                case Skill.Health:
                case Skill.Ranged:
                case Skill.Magic: return ("Power", GearSheet.Number(sheet.Power));
                default: return ("Armor", GearSheet.Number(sheet.Armor));
            }
        }

        /// <summary>The numbers with an outfit on: its pieces, and what is worn in the slots it leaves alone.</summary>
        private GearSheet SheetFor(Outfit outfit)
        {
            if (outfit == null || player == null) return null;
            var shown = new HashSet<Guid>();
            foreach (var slot in GearRules.All)
            {
                var entry = outfit.Get(Wire(slot.Slot));
                var e = entry == null ? (worn.TryGetValue(slot.Slot, out var w) ? w : null) : PieceOf(entry);
                if (e != null) shown.Add(e.Stack.Id);
            }
            var marks = entries.Where(x => x.Slot != EquipmentSlot.None).Select(x => (x.Stack, x.Stack.Equipped)).ToList();
            try
            {
                foreach (var e in entries.Where(x => x.Slot != EquipmentSlot.None)) e.Stack.Equipped = shown.Contains(e.Stack.Id);
                return GearSheet.Build(player, GearRules.StyleOf(outfit.Skill), Server.GetSkillBuild(player.Id), id => ItemService.GetItem(id),
                    stack => string.IsNullOrEmpty(stack.Enchantment) ? null : ItemService.GetItemEnchantments(stack));
            }
            finally
            {
                foreach (var mark in marks) mark.Stack.Equipped = mark.Item2;
            }
        }
    }
}

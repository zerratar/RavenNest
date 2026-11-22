using System;
using System.Collections.Generic;
using System.Text;

namespace RavenNest.Models
{
    /// <summary>One slot of an outfit: the piece that goes there, and whether the player pinned it.</summary>
    public sealed class OutfitSlot
    {
        public EquipmentSlot Slot { get; set; }

        /// <summary>
        /// The inventory item (the stack's id): the exact enchanted or socketed piece. A plain piece gets a new id
        /// when it is taken off and put on again (it joins its stack), so <see cref="ItemId"/> finds it then. Empty
        /// when only the kind of item is known (a piece that was on its way to the bag).
        /// </summary>
        public Guid Item { get; set; }

        /// <summary>The kind of item.</summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Pinned: the player's own pick, which nothing replaces. Not pinned is "best": what was worn last, and a
        /// better piece that arrives takes its place.
        /// </summary>
        public bool Pinned { get; set; }
    }

    /// <summary>What a character wears for one skill, and the pet that is out with it.</summary>
    public sealed class Outfit
    {
        public Skill Skill { get; set; } = Skill.None;

        /// <summary>
        /// The role or class gear line asked for with "!equip tank" while this outfit was on (the word of a
        /// <see cref="GearOutfit"/>), null for none: gear for it is ranked by that role, until "!equip all".
        /// </summary>
        public string Word { get; set; }

        /// <summary>The battle pet that goes out with the outfit; empty for none (the pet stays as it is).</summary>
        public Guid Pet { get; set; }

        public List<OutfitSlot> Slots { get; } = new List<OutfitSlot>();

        public OutfitSlot Get(EquipmentSlot slot)
        {
            for (var i = 0; i < Slots.Count; ++i)
            {
                if (Slots[i].Slot == slot) return Slots[i];
            }
            return null;
        }

        public OutfitSlot Set(EquipmentSlot slot, Guid item, Guid itemId, bool pinned)
        {
            var entry = Get(slot);
            if (entry == null)
            {
                entry = new OutfitSlot { Slot = slot };
                Slots.Add(entry);
            }
            entry.Item = item;
            entry.ItemId = itemId;
            entry.Pinned = pinned;
            return entry;
        }

        public bool Clear(EquipmentSlot slot)
        {
            for (var i = 0; i < Slots.Count; ++i)
            {
                if (Slots[i].Slot != slot) continue;
                Slots.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>How many slots are pinned.</summary>
        public int PinnedCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Slots.Count; ++i) if (Slots[i].Pinned) count++;
                return count;
            }
        }

        public bool IsPinned(EquipmentSlot slot)
        {
            var entry = Get(slot);
            return entry != null && entry.Pinned;
        }
    }

    /// <summary>
    /// A character's outfits (v0.9.7, the game's docs/outfits-design.md): one per skill, the two switches, and which
    /// outfit is on. The server keeps it as one short text per character (<see cref="ToString"/>) and sends that text
    /// to the game, which reads it with the same code.
    ///
    /// The game loads this library on its own runtime: plain types only (see <see cref="GearScore"/>). Numbers are
    /// written and read by hand here: CultureInfo is one of the types that runtime does not find in this library
    /// (a TypeLoadException when the first character joined, in the first play test).
    /// </summary>
    public sealed class CharacterOutfits
    {
        /// <summary>"Change outfit when I switch training": on unless the player turned it off.</summary>
        public bool Follow { get; set; } = true;

        /// <summary>"Upgrade to better gear automatically": on unless the player turned it off.</summary>
        public bool Upgrade { get; set; } = true;

        /// <summary>The skill whose outfit is on; <see cref="Skill.None"/> before the first change of training.</summary>
        public Skill Worn { get; set; } = Skill.None;

        /// <summary>The combat skill trained last: what a gathering character fights a raid, dungeon or tower floor in.</summary>
        public Skill Fight { get; set; } = Skill.None;

        /// <summary>
        /// While a fight's outfit is on: the skill whose outfit comes back afterwards. Kept by the server so a
        /// character that leaves, or whose stream ends, in the middle of a raid is not left in the wrong outfit.
        /// </summary>
        public Skill Back { get; set; } = Skill.None;

        public List<Outfit> Outfits { get; } = new List<Outfit>();

        public Outfit Find(Skill skill)
        {
            skill = OutfitRules.Normalize(skill);
            if (skill == Skill.None) return null;
            for (var i = 0; i < Outfits.Count; ++i)
            {
                if (Outfits[i].Skill == skill) return Outfits[i];
            }
            return null;
        }

        public Outfit GetOrAdd(Skill skill)
        {
            skill = OutfitRules.Normalize(skill);
            if (skill == Skill.None) return null;
            var outfit = Find(skill);
            if (outfit != null) return outfit;
            outfit = new Outfit { Skill = skill };
            Outfits.Add(outfit);
            return outfit;
        }

        public bool Remove(Skill skill)
        {
            var outfit = Find(skill);
            return outfit != null && Outfits.Remove(outfit);
        }

        /// <summary>Nothing was ever set or remembered: the text is empty and nothing needs keeping.</summary>
        public bool IsEmpty => Follow && Upgrade && Worn == Skill.None && Fight == Skill.None && Back == Skill.None && Outfits.Count == 0;

        // ---- the text form ----------------------------------------------------------------------------------------
        // "1;f1;u1;w0;c0;b6|0;tank;<pet>;1:<item>:<kind>:p,4:<item>:<kind>|6;;;..."
        //  version; follow; upgrade; worn; fight (combat); back | one part an outfit: skill; word; pet; slots
        // Ids are 32 hex digits, an empty id is left out, skills and slots are their numbers. Parts and fields this
        // version does not know are skipped, so a newer server's text still reads.

        private const string Version = "1";

        public override string ToString()
        {
            if (IsEmpty) return string.Empty;
            var text = new StringBuilder(64 + Outfits.Count * 400);
            text.Append(Version);
            text.Append(";f").Append(Follow ? '1' : '0');
            text.Append(";u").Append(Upgrade ? '1' : '0');
            if (Worn != Skill.None) text.Append(";w").Append(Number((int)Worn));
            if (Fight != Skill.None) text.Append(";c").Append(Number((int)Fight));
            if (Back != Skill.None) text.Append(";b").Append(Number((int)Back));
            for (var i = 0; i < Outfits.Count; ++i)
            {
                var outfit = Outfits[i];
                if (outfit == null || outfit.Skill == Skill.None) continue;
                text.Append('|').Append(Number((int)outfit.Skill));
                text.Append(';').Append(CleanWord(outfit.Word));
                text.Append(';').Append(Id(outfit.Pet));
                text.Append(';');
                var first = true;
                for (var s = 0; s < outfit.Slots.Count; ++s)
                {
                    var slot = outfit.Slots[s];
                    if (slot == null || slot.Slot == EquipmentSlot.None || slot.ItemId == Guid.Empty) continue;
                    if (!first) text.Append(',');
                    first = false;
                    text.Append(Number((int)slot.Slot)).Append(':').Append(Id(slot.Item)).Append(':').Append(Id(slot.ItemId));
                    if (slot.Pinned) text.Append(":p");
                }
            }
            return text.ToString();
        }

        /// <summary>Reads the text form. Never null and never throws: text that cannot be read gives the defaults.</summary>
        public static CharacterOutfits Parse(string text)
        {
            var result = new CharacterOutfits();
            if (string.IsNullOrEmpty(text)) return result;
            try
            {
                var parts = text.Split(new[] { '|' });
                var head = parts[0].Split(new[] { ';' });
                for (var i = 1; i < head.Length; ++i)
                {
                    var field = head[i];
                    if (field.Length < 2) continue;
                    var value = field.Substring(1);
                    switch (field[0])
                    {
                        case 'f': result.Follow = value != "0"; break;
                        case 'u': result.Upgrade = value != "0"; break;
                        case 'w': result.Worn = SkillOf(value); break;
                        case 'c': result.Fight = SkillOf(value); break;
                        case 'b': result.Back = SkillOf(value); break;
                    }
                }

                for (var p = 1; p < parts.Length; ++p)
                {
                    var fields = parts[p].Split(new[] { ';' });
                    if (fields.Length < 1) continue;
                    var skill = SkillOf(fields[0]);
                    if (skill == Skill.None || result.Find(skill) != null) continue;
                    var outfit = new Outfit { Skill = skill };
                    if (fields.Length > 1 && fields[1].Length > 0) outfit.Word = fields[1];
                    if (fields.Length > 2) outfit.Pet = IdOf(fields[2]);
                    if (fields.Length > 3 && fields[3].Length > 0)
                    {
                        foreach (var entry in fields[3].Split(new[] { ',' }))
                        {
                            var values = entry.Split(new[] { ':' });
                            if (values.Length < 3) continue;
                            if (!TryNumber(values[0], out var slotNumber)) continue;
                            var slot = (EquipmentSlot)slotNumber;
                            var itemId = IdOf(values[2]);
                            if (!OutfitRules.IsOutfitSlot(slot) || itemId == Guid.Empty || outfit.Get(slot) != null) continue;
                            outfit.Set(slot, IdOf(values[1]), itemId, values.Length > 3 && values[3] == "p");
                        }
                    }
                    result.Outfits.Add(outfit);
                }
            }
            catch
            {
                // a text that cannot be read is no outfits: the character keeps what it wears
                return new CharacterOutfits();
            }
            return result;
        }

        // whole numbers from 0 up, in plain digits whatever the language of the machine
        private static string Number(int value)
        {
            if (value <= 0) return "0";
            var digits = new StringBuilder(4);
            for (var rest = value; rest > 0; rest /= 10) digits.Insert(0, (char)('0' + rest % 10));
            return digits.ToString();
        }

        private static bool TryNumber(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 6) return false;
            foreach (var c in text)
            {
                if (c < '0' || c > '9') return false;
                value = value * 10 + (c - '0');
            }
            return true;
        }

        private static string Id(Guid id) => id == Guid.Empty ? string.Empty : id.ToString("N");

        private static Guid IdOf(string text) => !string.IsNullOrEmpty(text) && Guid.TryParse(text, out var id) ? id : Guid.Empty;

        private static Skill SkillOf(string text)
        {
            return TryNumber(text, out var number) ? OutfitRules.Normalize((Skill)number) : Skill.None;
        }

        private static string CleanWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return string.Empty;
            var outfit = GearOutfit.Parse(word);
            return outfit != null ? outfit.Word : string.Empty;
        }
    }

    /// <summary>
    /// A piece of gear a character has, as the outfit rules see it. The server makes these from its inventory and the
    /// game from its own, so both decide from the same facts.
    /// </summary>
    public struct OutfitPiece
    {
        /// <summary>The inventory item (the stack's id); empty for a piece that is not in the bag yet.</summary>
        public Guid Id;

        /// <summary>The kind of item.</summary>
        public Guid ItemId;

        public ItemType Type;
        public ItemCategory Category;
        public ItemMaterial Material;

        /// <summary>The worth by the old rule: the seven classic columns with what its enchantments add.</summary>
        public int Classic;

        /// <summary>Worn now.</summary>
        public bool Worn;

        /// <summary>The character may wear it (its levels are high enough).</summary>
        public bool Wearable;

        /// <summary>
        /// A plain piece: not enchanted, socketed or changed in any way, so it is one of a stack and any other plain
        /// piece of its kind is the same thing.
        /// </summary>
        public bool Plain;

        public string Name;

        public EquipmentSlot Slot => OutfitRules.SlotOf(Type);

        /// <summary>
        /// Worn for its look: a hat or mask that is not class gear, or anything worth nothing. "Equip best" never
        /// picks one and never replaces one, and neither do the outfit rules.
        /// </summary>
        public bool IsLook => Classic <= 0 || (!ItemLines.IsClassGear(Material) && (Type == ItemType.Hat || Type == ItemType.Mask));
    }

    /// <summary>What putting an outfit on comes to (<see cref="OutfitRules.Wear"/>).</summary>
    public sealed class OutfitPlan
    {
        /// <summary>The pieces to put on, as indexes into the pieces given: those of the outfit that are not worn.</summary>
        public List<int> Equip { get; } = new List<int>();

        /// <summary>The kinds of item the outfit had that are gone (sold, traded, stashed).</summary>
        public List<Guid> Gone { get; } = new List<Guid>();

        /// <summary>What filled each gone piece's slot, by kind of item, in the order of <see cref="Gone"/>; empty when nothing owned fits.</summary>
        public List<Guid> Filled { get; } = new List<Guid>();

        /// <summary>The outfit itself changed (a slot filled, or pointed at the piece's new stack) and needs saving.</summary>
        public bool Changed { get; set; }
    }

    /// <summary>
    /// The rules of outfits (v0.9.7, the game's docs/outfits-design.md), in the one place the game and the server both
    /// use, as <see cref="GearScore"/> is for ranking gear. The server decides what a character wears; the game uses the
    /// same rules to know when to ask it, and for the gear that arrives while an outfit is on.
    ///
    ///  - An outfit is what was worn the last time its skill was trained: when a character leaves a skill, what it
    ///    wears is kept as that skill's outfit (<see cref="Remember"/>). Pinned slots keep their pin.
    ///  - When training changes the new skill's outfit goes on (<see cref="Wear"/>). A skill with no outfit keeps what
    ///    is worn. A slot whose piece is gone is filled with the best piece owned, by the rule "equip best" uses.
    ///  - A raid, dungeon or tower floor is fought in the outfit of the combat skill that is trained, or was trained
    ///    last (<see cref="Desired"/>); the other outfit comes back afterwards.
    ///  - A better piece that arrives goes into every outfit whose slot is not pinned (<see cref="Upgrades"/>), with
    ///    "upgrade automatically" on; nothing replaces a pinned piece.
    /// </summary>
    public static class OutfitRules
    {
        /// <summary>The slots an outfit has. Pet items, capes and boats are not part of one.</summary>
        public static readonly EquipmentSlot[] Slots =
        {
            EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Leggings, EquipmentSlot.Boots,
            EquipmentSlot.MeleeWeapon, EquipmentSlot.RangedWeapon, EquipmentSlot.MagicWeapon, EquipmentSlot.Shield,
            EquipmentSlot.Amulet, EquipmentSlot.Ring,
        };

        public static bool IsOutfitSlot(EquipmentSlot slot) => Array.IndexOf(Slots, slot) >= 0;

        /// <summary>Head, chest, gloves, leggings and boots: the slots class gear has pieces for.</summary>
        public static bool IsArmorSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.Head || slot == EquipmentSlot.Chest || slot == EquipmentSlot.Gloves
                || slot == EquipmentSlot.Leggings || slot == EquipmentSlot.Boots;
        }

        /// <summary>Where a type of item is worn; the server's and the game's own mapping, for both to share here.</summary>
        public static EquipmentSlot SlotOf(ItemType type)
        {
            switch (type)
            {
                case ItemType.Amulet: return EquipmentSlot.Amulet;
                case ItemType.Ring: return EquipmentSlot.Ring;
                case ItemType.Shield: return EquipmentSlot.Shield;
                case ItemType.Hat:
                case ItemType.Mask:
                case ItemType.Helmet:
                case ItemType.HeadCovering: return EquipmentSlot.Head;
                case ItemType.Chest: return EquipmentSlot.Chest;
                case ItemType.Gloves: return EquipmentSlot.Gloves;
                case ItemType.Leggings: return EquipmentSlot.Leggings;
                case ItemType.Boots: return EquipmentSlot.Boots;
                case ItemType.Pet: return EquipmentSlot.Pet;
                case ItemType.OneHandedAxe:
                case ItemType.TwoHandedAxe:
                case ItemType.TwoHandedSpear:
                case ItemType.OneHandedSword:
                case ItemType.TwoHandedSword: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedStaff: return EquipmentSlot.MagicWeapon;
                case ItemType.TwoHandedBow: return EquipmentSlot.RangedWeapon;
                case ItemType.Boat: return EquipmentSlot.Boat;
                default: return EquipmentSlot.None;
            }
        }

        // ---- skills ----------------------------------------------------------------------------------------------

        /// <summary>
        /// A skill as outfits know it: training "all" is Health (what the game trains for it, and Melee is the same
        /// thing), Taming is never trained as a task, and anything that is no skill is <see cref="Skill.None"/>.
        /// </summary>
        public static Skill Normalize(Skill skill)
        {
            if (skill == Skill.Melee) return Skill.Health;
            if (skill == Skill.Taming || skill == Skill.None) return Skill.None;
            return Enum.IsDefined(typeof(Skill), skill) ? skill : Skill.None;
        }

        /// <summary>Attack, Defense, Strength, all of them (Health), Ranged, Magic and Healing: what a fight is fought with.</summary>
        public static bool IsCombat(Skill skill)
        {
            switch (Normalize(skill))
            {
                case Skill.Attack:
                case Skill.Defense:
                case Skill.Strength:
                case Skill.Health:
                case Skill.Ranged:
                case Skill.Magic:
                case Skill.Healing:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The skill a character trains, from the task the server keeps for it; <see cref="Skill.None"/> for none.</summary>
        public static Skill SkillOf(string task, string taskArgument)
        {
            if (string.IsNullOrEmpty(task)) return Skill.None;
            var name = task.Trim();
            if (name.Equals("Fighting", StringComparison.OrdinalIgnoreCase))
            {
                var argument = (taskArgument ?? string.Empty).Trim().ToLowerInvariant();
                if (argument.Length == 0 || argument.StartsWith("all") || argument.StartsWith("health") || argument.StartsWith("melee")) return Skill.Health;
                var trained = GearScore.Trained(task, taskArgument);
                return trained == Skill.None ? Skill.Health : trained;
            }
            return ParseSkill(name);
        }

        /// <summary>
        /// A skill from what a player types ("!outfit mining", "!outfit atk", "!outfit all") or from a role's word
        /// ("tank" is Defense, "ranger" is Ranged). <see cref="Skill.None"/> for anything else.
        /// </summary>
        public static Skill ParseSkill(string text)
        {
            if (string.IsNullOrEmpty(text)) return Skill.None;
            var word = text.Trim().ToLowerInvariant();
            switch (word)
            {
                case "all":
                case "health":
                case "combat":
                case "fighting":
                    return Skill.Health;
                case "att":
                    return Skill.Attack;
                case "wood":
                case "wc":
                case "chop":
                case "chopping":
                    return Skill.Woodcutting;
                case "fish":
                    return Skill.Fishing;
                case "mine":
                    return Skill.Mining;
                case "craft":
                    return Skill.Crafting;
                case "cook":
                    return Skill.Cooking;
                case "farm":
                    return Skill.Farming;
                case "gather":
                    return Skill.Gathering;
                case "alch":
                case "brew":
                case "brewing":
                    return Skill.Alchemy;
                case "sail":
                    return Skill.Sailing;
                case "slay":
                    return Skill.Slayer;
            }

            foreach (Skill skill in Enum.GetValues(typeof(Skill)))
            {
                if (skill.ToString().ToLowerInvariant() == word) return Normalize(skill);
            }

            // a role's word, as "!equip" reads it: the skill it dresses for (not a line by name: "rogue" is no skill)
            var role = GearOutfit.Parse(word);
            return role != null && !role.OnlyLine ? Normalize(role.Skill) : Skill.None;
        }

        /// <summary>A skill's name as players type it, in lower case: "attack", "woodcutting", and "all" for Health.</summary>
        public static string NameOf(Skill skill)
        {
            skill = Normalize(skill);
            if (skill == Skill.None) return string.Empty;
            return skill == Skill.Health ? "all" : skill.ToString().ToLowerInvariant();
        }

        // ---- which outfit ----------------------------------------------------------------------------------------

        /// <summary>
        /// The skill whose outfit a character should have on: the one it trains, and in a raid, dungeon or tower
        /// floor the combat skill it trains or trained last. A gathering character that never trained combat keeps
        /// its own outfit in a fight.
        /// </summary>
        public static Skill Desired(Skill trained, bool fighting, Skill trainedLast)
        {
            trained = Normalize(trained);
            if (!fighting || trained == Skill.None || IsCombat(trained)) return trained;
            trainedLast = Normalize(trainedLast);
            return IsCombat(trainedLast) ? trainedLast : trained;
        }

        /// <summary>
        /// Does the game have to ask the server when a character's training, or being in a fight, has changed. Not
        /// when the outfit that should be on is on already, and not for a fight when the fight's skill has no outfit
        /// of its own yet (it keeps what is worn, so nothing would change). With "change outfit when I switch
        /// training" off, only to tell the server the combat skill that is trained now.
        /// </summary>
        /// <param name="worn">The skill whose outfit is on, as far as the game knows.</param>
        public static bool NeedsAsking(CharacterOutfits outfits, Skill trained, bool fighting, Skill worn)
        {
            trained = Normalize(trained);
            if (outfits == null || trained == Skill.None) return false;
            if (!fighting && IsCombat(trained) && outfits.Fight != trained) return true;
            if (!outfits.Follow) return false;
            var desired = Desired(trained, fighting, outfits.Fight);
            if (desired == Normalize(worn)) return false;
            if (fighting && desired != trained && outfits.Find(desired) == null) return false;
            return true;
        }

        // ---- remembering -----------------------------------------------------------------------------------------

        private static int WornIn(IList<OutfitPiece> pieces, EquipmentSlot slot)
        {
            if (pieces == null) return -1;
            for (var i = 0; i < pieces.Count; ++i)
            {
                if (pieces[i].Worn && pieces[i].Slot == slot) return i;
            }
            return -1;
        }

        /// <summary>
        /// Keeps what is worn as the outfit: every slot that is not pinned takes the piece worn there (an empty slot
        /// is forgotten), a pinned slot stays as it is, and the pet that is out becomes the outfit's pet.
        /// </summary>
        /// <returns>Whether the outfit changed.</returns>
        public static bool Remember(Outfit outfit, IList<OutfitPiece> pieces, Guid petOut)
        {
            if (outfit == null) return false;
            var changed = false;
            foreach (var slot in Slots)
            {
                var entry = outfit.Get(slot);
                if (entry != null && entry.Pinned) continue;
                var worn = WornIn(pieces, slot);
                if (worn < 0)
                {
                    if (outfit.Clear(slot)) changed = true;
                    continue;
                }
                var piece = pieces[worn];
                if (entry != null && entry.Item == piece.Id && entry.ItemId == piece.ItemId) continue;
                outfit.Set(slot, piece.Id, piece.ItemId, false);
                changed = true;
            }
            if (outfit.Pet != petOut)
            {
                outfit.Pet = petOut;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Pins what is worn: every slot with a piece in it is pinned to that piece, also one that was pinned to
        /// something else. Empty slots are forgotten.
        /// </summary>
        public static void Pin(Outfit outfit, IList<OutfitPiece> pieces, Guid petOut)
        {
            if (outfit == null) return;
            foreach (var slot in Slots)
            {
                var worn = WornIn(pieces, slot);
                if (worn < 0) outfit.Clear(slot);
                else outfit.Set(slot, pieces[worn].Id, pieces[worn].ItemId, true);
            }
            outfit.Pet = petOut;
        }

        /// <summary>Every slot back to "best": better gear replaces it again.</summary>
        /// <returns>How many slots were pinned.</returns>
        public static int Unpin(Outfit outfit)
        {
            if (outfit == null) return 0;
            var count = 0;
            for (var i = 0; i < outfit.Slots.Count; ++i)
            {
                if (!outfit.Slots[i].Pinned) continue;
                outfit.Slots[i].Pinned = false;
                count++;
            }
            return count;
        }

        // ---- wearing ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The piece a slot of an outfit means, among what the character has and may wear: the very inventory item,
        /// or when that is gone (a plain piece joins its stack when it is taken off) one of the same kind, the worn
        /// one first, then the one worth the most. A plain piece that is not worn is any of its kind too: the id it
        /// was kept under is its stack's, while the one taken from that stack is worn under a new id.
        /// -1 when the character has none: the piece is gone.
        /// </summary>
        public static int Find(OutfitSlot entry, IList<OutfitPiece> pieces)
        {
            if (entry == null || pieces == null) return -1;
            var found = -1;
            for (var i = 0; i < pieces.Count; ++i)
            {
                var piece = pieces[i];
                if (piece.ItemId != entry.ItemId || !piece.Wearable || piece.Slot != entry.Slot) continue;
                if (entry.Item != Guid.Empty && piece.Id == entry.Item && (piece.Worn || !piece.Plain)) return i;
                if (found < 0) { found = i; continue; }
                var best = pieces[found];
                if (piece.Worn != best.Worn)
                {
                    if (piece.Worn) found = i;
                    continue;
                }
                if (piece.Classic != best.Classic)
                {
                    if (piece.Classic > best.Classic) found = i;
                    continue;
                }
                if (piece.Id.CompareTo(best.Id) < 0) found = i;
            }
            return found;
        }

        /// <summary>
        /// The lines of class gear that fit an outfit, narrowed to one as "equip best" does
        /// (<see cref="GearScore.OneLine(int, ItemLine, ItemMaterial[], int[], int[], bool[])"/>): those of the role
        /// that was asked for with it ("!equip tank"), or of its skill. For a skill that names no line (a gathering
        /// skill, or training all) the line most of the outfit's own armor is of, so a set stays whole.
        /// </summary>
        /// <param name="mine">For each piece, whether it is one of the outfit's own (found for one of its slots).</param>
        public static int FitsOf(Outfit outfit, IList<OutfitPiece> pieces, bool[] mine)
        {
            if (outfit == null) return 0;
            var word = GearOutfit.Parse(outfit.Word);
            var fits = word != null ? word.Fits : ItemLines.Fits(outfit.Skill);
            if (pieces == null) return fits;
            if (fits == 0)
            {
                var counts = new int[ItemLines.Count + 1];
                for (var i = 0; i < pieces.Count; ++i)
                {
                    if (mine == null || i >= mine.Length || !mine[i]) continue;
                    if (!IsArmorSlot(pieces[i].Slot) || !ItemLines.IsClassGear(pieces[i].Material)) continue;
                    var line = (int)ItemLines.Of(pieces[i].Material);
                    if (line > 0 && line < counts.Length) counts[line]++;
                }
                var most = 0;
                for (var line = 1; line < counts.Length; ++line)
                {
                    if (counts[line] > counts[most]) most = line;
                }
                return most == 0 ? 0 : ItemLines.Bit((ItemLine)most);
            }

            var materials = new List<ItemMaterial>();
            var classics = new List<int>();
            var slots = new List<int>();
            var worn = new List<bool>();
            for (var i = 0; i < pieces.Count; ++i)
            {
                var piece = pieces[i];
                if (!piece.Wearable || !IsArmorSlot(piece.Slot) || !ItemLines.IsClassGear(piece.Material)) continue;
                materials.Add(piece.Material);
                classics.Add(piece.Classic);
                slots.Add((int)piece.Slot);
                worn.Add(mine != null && i < mine.Length && mine[i]);
            }
            return GearScore.OneLine(fits, word != null ? word.Line : ItemLine.Plate, materials.ToArray(), classics.ToArray(), slots.ToArray(), worn.ToArray());
        }

        /// <summary>
        /// The best piece owned for a slot of an outfit, by the rule "equip best" uses: the worth for the outfit's
        /// lines (<see cref="GearScore.Value(ItemMaterial, int, int)"/>), a piece of the role or line that was asked
        /// for before any other armor, the worn one among equals, then the first by name. Looks are never picked.
        /// -1 when the character has nothing to wear there.
        /// </summary>
        public static int Best(Outfit outfit, EquipmentSlot slot, IList<OutfitPiece> pieces, int fits)
        {
            if (pieces == null) return -1;
            var word = outfit != null ? GearOutfit.Parse(outfit.Word) : null;
            var covering = word != null && IsArmorSlot(slot);
            var best = -1;
            var bestValue = 0;
            var bestCovers = false;
            for (var i = 0; i < pieces.Count; ++i)
            {
                var piece = pieces[i];
                if (piece.Slot != slot || !piece.Wearable || piece.IsLook) continue;
                var value = GearScore.Value(piece.Material, piece.Classic, fits);
                var covers = covering && word.Covers(piece.Material);
                var better = false;
                if (best < 0) better = true;
                else if (covers != bestCovers) better = covers;
                else if (value != bestValue) better = value > bestValue;
                else if (piece.Worn != pieces[best].Worn) better = piece.Worn;
                else better = string.CompareOrdinal(piece.Name, pieces[best].Name) < 0;
                if (!better) continue;
                best = i;
                bestValue = value;
                bestCovers = covers;
            }
            return best;
        }

        /// <summary>
        /// The lines "equip best" ranks class gear by for an outfit's skill (or the role asked for with it), as the
        /// server's equip best narrows them (PlayerInventory.EquipBestItems): several fitting lines become one, the
        /// one worn now winning among equals. A skill that names no line (training all, a gathering skill) is 0: equip
        /// best ranks by worth alone for it.
        /// </summary>
        public static int FitsForBest(Outfit outfit, IList<OutfitPiece> pieces)
        {
            if (outfit == null) return 0;
            var word = GearOutfit.Parse(outfit.Word);
            var fits = word != null ? word.Fits : ItemLines.Fits(outfit.Skill);
            if (fits == 0 || pieces == null) return fits;
            var worn = new bool[pieces.Count];
            for (var i = 0; i < pieces.Count; ++i) worn[i] = pieces[i].Worn;
            return FitsOf(outfit, pieces, worn);
        }

        /// <summary>
        /// What "make an outfit from my best gear" fills an outfit with, slot by slot: the pieces "equip best" picks
        /// for the outfit's skill, so the website's fill, an outfit going on, the game and the server's equip best all
        /// agree (the website's review, 2026-11-22: the fill and equip best gave different gear). The lines are
        /// <see cref="FitsForBest"/>, each slot is <see cref="Best"/>. Indexes into <paramref name="pieces"/> in the
        /// order of <see cref="Slots"/>; -1 where nothing is owned, and for a pinned slot when
        /// <paramref name="keepPinned"/> (nothing replaces a pinned piece).
        /// </summary>
        public static int[] BestPicks(Outfit outfit, IList<OutfitPiece> pieces, bool keepPinned = true)
        {
            var picks = new int[Slots.Length];
            for (var s = 0; s < picks.Length; ++s) picks[s] = -1;
            if (outfit == null || pieces == null) return picks;
            var fits = FitsForBest(outfit, pieces);
            for (var s = 0; s < Slots.Length; ++s)
            {
                if (keepPinned && outfit.IsPinned(Slots[s])) continue;
                picks[s] = Best(outfit, Slots[s], pieces, fits);
            }
            return picks;
        }

        /// <summary>
        /// Fills an outfit with <see cref="BestPicks"/>: every slot that is not pinned gets its best piece, as "best".
        /// A slot with nothing owned for it keeps what it had.
        /// </summary>
        /// <returns>Whether the outfit changed.</returns>
        public static bool FillBest(Outfit outfit, IList<OutfitPiece> pieces)
        {
            if (outfit == null || pieces == null) return false;
            var picks = BestPicks(outfit, pieces);
            var changed = false;
            for (var s = 0; s < Slots.Length; ++s)
            {
                if (picks[s] < 0) continue;
                var piece = pieces[picks[s]];
                var entry = outfit.Get(Slots[s]);
                if (entry != null && entry.Item == piece.Id && entry.ItemId == piece.ItemId && !entry.Pinned) continue;
                outfit.Set(Slots[s], piece.Id, piece.ItemId, false);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Makes <paramref name="to"/> a copy of <paramref name="from"/>: the same pieces in the same slots, pinned
        /// where they were pinned, and the same battle pet. The role asked for with "!equip" is not copied: it was
        /// asked for while the other skill was trained.
        /// </summary>
        public static void CopyInto(Outfit from, Outfit to)
        {
            if (from == null || to == null || ReferenceEquals(from, to)) return;
            to.Slots.Clear();
            for (var i = 0; i < from.Slots.Count; ++i)
            {
                var slot = from.Slots[i];
                if (slot == null || !IsOutfitSlot(slot.Slot) || slot.ItemId == Guid.Empty) continue;
                to.Set(slot.Slot, slot.Item, slot.ItemId, slot.Pinned);
            }
            to.Pet = from.Pet;
        }

        /// <summary>
        /// Keeps what is worn in an outfit that is on, after gear was changed by hand: every slot where the worn piece
        /// is not the outfit's takes the worn piece, keeping its pin when it had one; a slot of the outfit where
        /// nothing is worn any more is forgotten. The "Save to the outfit" of the website's Now view.
        /// </summary>
        /// <returns>How many slots changed.</returns>
        public static int KeepWorn(Outfit outfit, IList<OutfitPiece> pieces)
        {
            if (outfit == null || pieces == null) return 0;
            var count = 0;
            foreach (var slot in Slots)
            {
                var entry = outfit.Get(slot);
                var worn = WornIn(pieces, slot);
                if (worn < 0)
                {
                    if (entry != null && Find(entry, pieces) >= 0 && outfit.Clear(slot)) count++;
                    continue;
                }
                if (entry != null && (Find(entry, pieces) == worn || (entry.Item == pieces[worn].Id && entry.ItemId == pieces[worn].ItemId))) continue;
                outfit.Set(slot, pieces[worn].Id, pieces[worn].ItemId, entry != null && entry.Pinned);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Puts an outfit on: every slot of it that has a piece gets that piece, slots it has nothing for keep what
        /// is worn (an outfit never takes gear off without putting something on). A slot whose piece is gone is
        /// filled with the best piece owned (<see cref="Best"/>) and loses its pin; with nothing to fill it, the slot
        /// is forgotten. The outfit is changed to what it comes to; the plan says what to put on and what was gone.
        /// </summary>
        /// <param name="pinnedOnly">Only the pinned slots: for an outfit that is on already, after gear was changed by hand.</param>
        public static OutfitPlan Wear(Outfit outfit, IList<OutfitPiece> pieces, bool pinnedOnly = false)
        {
            var plan = new OutfitPlan();
            if (outfit == null || pieces == null) return plan;

            // the outfit's own pieces first: they decide the line a gone piece is filled from
            var mine = new bool[pieces.Count];
            var found = new int[outfit.Slots.Count];
            for (var s = 0; s < outfit.Slots.Count; ++s)
            {
                found[s] = Find(outfit.Slots[s], pieces);
                if (found[s] >= 0) mine[found[s]] = true;
            }

            var fits = -1;
            for (var s = outfit.Slots.Count - 1; s >= 0; --s)
            {
                var entry = outfit.Slots[s];
                if (pinnedOnly && !entry.Pinned) continue;
                var index = found[s];
                if (index < 0)
                {
                    if (fits < 0) fits = FitsOf(outfit, pieces, mine);
                    plan.Gone.Add(entry.ItemId);
                    index = Best(outfit, entry.Slot, pieces, fits);
                    plan.Changed = true;
                    if (index < 0)
                    {
                        plan.Filled.Add(Guid.Empty);
                        outfit.Slots.RemoveAt(s);
                        continue;
                    }
                    plan.Filled.Add(pieces[index].ItemId);
                    entry.Pinned = false;
                    entry.ItemId = pieces[index].ItemId;
                }
                if (entry.Item != pieces[index].Id)
                {
                    entry.Item = pieces[index].Id;
                    plan.Changed = true;
                }
                if (!pieces[index].Worn) plan.Equip.Add(index);
            }
            return plan;
        }

        // ---- gear that arrives -----------------------------------------------------------------------------------

        /// <summary>
        /// May a piece that arrives replace what is worn in a slot by itself: not with "upgrade automatically" off,
        /// and never in a slot that is pinned in the outfit that is on. (An empty slot is always filled.)
        /// </summary>
        public static bool MayReplace(CharacterOutfits outfits, Skill worn, EquipmentSlot slot)
        {
            if (outfits == null) return true;
            if (!outfits.Upgrade) return false;
            var outfit = outfits.Find(worn);
            return outfit == null || !outfit.IsPinned(slot);
        }

        /// <summary>
        /// Does a piece that arrives go into an outfit that is not on: its slot there is not pinned, the outfit's
        /// piece is still owned, and the new piece beats it by the rule that puts a found piece on
        /// (<see cref="GearScore.Beats"/>, for the outfit's own skill or the role asked for with it; a piece of that
        /// role is only replaced by another one of it). A slot whose piece is gone is not taken here: it is filled
        /// when the outfit is next worn.
        /// </summary>
        /// <param name="pieces">What the character has; the arriving piece need not be among them yet.</param>
        public static bool Upgrades(Outfit outfit, OutfitPiece candidate, IList<OutfitPiece> pieces)
        {
            if (outfit == null || pieces == null) return false;
            var slot = candidate.Slot;
            if (!IsOutfitSlot(slot) || !candidate.Wearable || candidate.IsLook) return false;
            var entry = outfit.Get(slot);
            if (entry == null || entry.Pinned) return false;
            var index = Find(entry, pieces);
            if (index < 0) return false;
            var occupant = pieces[index];
            if (occupant.IsLook) return false;
            if (occupant.ItemId == candidate.ItemId && candidate.Classic <= occupant.Classic) return false;

            var word = GearOutfit.Parse(outfit.Word);
            if (word != null && IsArmorSlot(slot) && word.Covers(occupant.Material) && !word.Covers(candidate.Material)) return false;

            // the outfit's own pieces, and everything wearable with the new piece among it, decide the one line
            var all = new List<OutfitPiece>(pieces.Count + 1);
            var has = false;
            for (var i = 0; i < pieces.Count; ++i)
            {
                all.Add(pieces[i]);
                if (candidate.Id != Guid.Empty && pieces[i].Id == candidate.Id) has = true;
            }
            if (!has) all.Add(candidate);
            var mine = new bool[all.Count];
            for (var s = 0; s < outfit.Slots.Count; ++s)
            {
                var own = Find(outfit.Slots[s], pieces);
                if (own >= 0) mine[own] = true;
            }
            var fits = GearScore.KeepWorn(FitsOf(outfit, all, mine), occupant.Material);
            return GearScore.Beats(candidate.Material, candidate.Classic, occupant.Material, occupant.Classic, fits);
        }
    }
}

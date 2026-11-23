using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.Combat;
using RavenNest.DataModels;
using HeadMask = RavenNest.Models.HeadMask;
using ItemLine = RavenNest.Models.ItemLine;
using ItemLines = RavenNest.Models.ItemLines;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>
    ///     Class gear (v0.9.7, the game's docs/class-gear-design.md): armor lines of eight tiers, six pieces each (thirteen
    ///     lines: the first five, seven more since 2026-11-21, and the Taming line, Beastmaster, since 2026-11-22).
    ///     Everything about them is made here at startup from two tables: <see cref="ClassGearPieces"/> (what a piece is
    ///     and how it looks) and <see cref="ClassGearLines"/> (what a line gives). The code is the record for these items:
    ///     a row that differs is put right at the next start. Adding a line or a piece is adding rows.
    /// </summary>
    public partial class GameData
    {
        private sealed class ClassGearPiece
        {
            public ItemLine Line;
            /// <summary>Stays the same when the name, the type or the look changes: the item's id is made from it.</summary>
            public string Key;
            public ItemType Type;
            /// <summary>The name after the tier's: "Robe" makes "Adept Robe".</summary>
            public string Name;
            /// <summary>The parts on the character, as the game's part indices: the main part, then the additional ones.</summary>
            public string MaleModels;
            public string FemaleModels;
            /// <summary>What a head piece hides, or what a body piece leaves off.</summary>
            public HeadMask Mask;
            /// <summary>The tiers that look different from tier 1 (a plume, a pouch on the belt): see <see cref="From"/>.</summary>
            public readonly List<ClassGearLook> Looks = new List<ClassGearLook>();
            /// <summary>
            ///     The first tier that has this piece (1 for every tier). The Dark Fortress head pieces (the game's
            ///     docs/dark-fortress-design.md) are a third head look of a few lines from a high tier on.
            /// </summary>
            public int MinTier = 1;

            /// <summary>
            ///     From this tier on the piece is these parts (and this mask; the piece's own when none is given). The
            ///     item's id does not change: it is made from line, tier and key.
            /// </summary>
            public ClassGearPiece From(int tier, string models, HeadMask? mask = null, string femaleModels = null)
            {
                Looks.Add(new ClassGearLook { Tier = tier, MaleModels = models, FemaleModels = femaleModels ?? models, Mask = mask ?? Mask });
                return this;
            }

            /// <summary>
            ///     From this tier on the piece is a model of its own (a Resources prefab under the head bone, the
            ///     GenericPrefab column) with this mask, in place of character parts. Only for head pieces.
            /// </summary>
            public ClassGearPiece FromPrefab(int tier, string prefab, HeadMask? mask = null)
            {
                Looks.Add(new ClassGearLook { Tier = tier, MaleModels = "", FemaleModels = "", Mask = mask ?? Mask, Prefab = prefab });
                return this;
            }

            /// <summary>The piece exists from <paramref name="tier"/> on only.</summary>
            public ClassGearPiece Starting(int tier)
            {
                MinTier = tier;
                return this;
            }

            /// <summary>The parts and the mask of a tier: the last look that starts at or below it, or the piece's own.</summary>
            public ClassGearLook LookAt(int tier)
            {
                var look = new ClassGearLook { Tier = 1, MaleModels = MaleModels, FemaleModels = FemaleModels, Mask = Mask };
                foreach (var other in Looks)
                {
                    if (other.Tier <= tier && other.Tier >= look.Tier) look = other;
                }
                return look;
            }
        }

        private sealed class ClassGearLook
        {
            public int Tier;
            public string MaleModels;
            public string FemaleModels;
            public HeadMask Mask;
            /// <summary>A model of its own (the item's GenericPrefab), or null for character parts.</summary>
            public string Prefab;
        }

        private static ClassGearPiece Piece(ItemLine line, string key, ItemType type, string name, string models, HeadMask mask = HeadMask.None, string femaleModels = null)
        {
            return new ClassGearPiece { Line = line, Key = key, Type = type, Name = name, MaleModels = models, FemaleModels = femaleModels ?? models, Mask = mask };
        }

        /// <summary>
        ///     The pieces of every line. The model ids must be valid part indices in games before 0.9.7 too (they read the
        ///     same list and never look at the mask), so "no part" is said with a mask flag and a harmless index.
        ///     The two head pieces of a line have the same stats: they are a choice of look.
        ///     <para>
        ///     Higher tiers wear more (docs/class-gear-design.md, "Attachments"). The model ids, in the game's order:
        ///     a head piece is its part, then any head attachments (plume, wings, crown, cross); a chest is torso,
        ///     pauldrons, upper arms, elbow pads, then what is worn on the back; leggings are hips, knee pads, then what
        ///     hangs on the belt. A game before 0.9.7 reads the positions it knows (all of a head piece's, a chest's
        ///     first four, the leggings' first two) and only parses the rest, so those positions always hold a real
        ///     part index: leggings with a belt and no knee pads say knee pad 10 and NoKneePads.
        ///     The game's copy of this table is tools/items/class-gear-parts.txt (icons and previews are made from it).
        ///     </para>
        /// </summary>
        private static readonly ClassGearPiece[] ClassGearPieces =
        {
            //    line, key, type, name, model ids at tier 1, mask; then .From(tier, model ids, mask) for the tiers that have more
            Piece(ItemLine.Mage, "head1", ItemType.Hat, "Wizard Hat", "1").From(3, "1,8").From(6, "1,7"),
            Piece(ItemLine.Mage, "head2", ItemType.HeadCovering, "Hood", "6", HeadMask.Hair | HeadMask.Ears).From(3, "6,8").From(6, "6,7"),
            Piece(ItemLine.Mage, "chest", ItemType.Chest, "Robe", "27,20,20,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(6, "27,20,20,0,4")
                .From(7, "27,20,20,0,-1033,-3025", DarkFortressBody).From(8, "27,20,20,0,-1034,-3026", DarkFortressBody),
            Piece(ItemLine.Mage, "gloves", ItemType.Gloves, "Gloves", "9,18").From(7, "9,18,-1022,-2023").From(8, "9,18,-1023,-2024"),
            Piece(ItemLine.Mage, "leggings", ItemType.Leggings, "Robe Skirt", "9").From(2, "9,10,7", HeadMask.NoKneePads).From(4, "9,10,7,0", HeadMask.NoKneePads)
                .From(7, "9,10,-1033", HeadMask.NoKneePads).From(8, "9,10,-1034", HeadMask.NoKneePads),
            Piece(ItemLine.Mage, "boots", ItemType.Boots, "Boots", "11").From(7, "11,-1024").From(8, "11,-1025"),

            Piece(ItemLine.Healer, "head1", ItemType.HeadCovering, "Cowl", "4", HeadMask.Hair | HeadMask.Ears).From(3, "4,10").From(6, "4,7").From(7, "4,2"),
            Piece(ItemLine.Healer, "head2", ItemType.HeadCovering, "Cap", "0", HeadMask.Hair | HeadMask.Ears).From(3, "0,10").From(6, "0,7").From(7, "0,2"),
            Piece(ItemLine.Healer, "chest", ItemType.Chest, "Vestment", "10,20,20,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads),
            Piece(ItemLine.Healer, "gloves", ItemType.Gloves, "Gloves", "13,18"),
            Piece(ItemLine.Healer, "leggings", ItemType.Leggings, "Tabard", "8").From(2, "8,10,2", HeadMask.NoKneePads).From(4, "8,10,2,8", HeadMask.NoKneePads),
            Piece(ItemLine.Healer, "boots", ItemType.Boots, "Boots", "11"),

            Piece(ItemLine.Ranger, "head1", ItemType.Hat, "Cap", "4").From(5, "4,11"),
            Piece(ItemLine.Ranger, "head2", ItemType.HeadCovering, "Hood", "5", HeadMask.Hair | HeadMask.Ears).From(3, "5,12").From(6, "5,3"), // 12 left the nape bare and hung a strap over the beard
            Piece(ItemLine.Ranger, "chest", ItemType.Chest, "Jerkin", "14,20,10,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(4, "14,15,10,0", HeadMask.NoElbowPads).From(5, "14,15,10,0,1", HeadMask.NoElbowPads).From(7, "14,15,10,0,0", HeadMask.NoElbowPads),
            Piece(ItemLine.Ranger, "gloves", ItemType.Gloves, "Gloves", "3,3"),
            Piece(ItemLine.Ranger, "leggings", ItemType.Leggings, "Trousers", "6").From(2, "6,10,3", HeadMask.NoKneePads).From(4, "6,4,3").From(6, "6,4,3,6"),
            Piece(ItemLine.Ranger, "boots", ItemType.Boots, "Boots", "19"),

            Piece(ItemLine.Melee, "head1", ItemType.HeadCovering, "Helm", "1", HeadMask.Hair | HeadMask.Ears).From(3, "1,1").From(6, "1,6"),
            Piece(ItemLine.Melee, "head2", ItemType.HeadCovering, "Coif", "11", HeadMask.Hair | HeadMask.Ears).From(3, "11,8").From(6, "11,7"),
            Piece(ItemLine.Melee, "chest", ItemType.Chest, "Hauberk", "17,13,19,0", HeadMask.NoElbowPads).From(4, "17,9,19,0").From(7, "17,8,19,0,3"),
            Piece(ItemLine.Melee, "gloves", ItemType.Gloves, "Gauntlets", "5,17"),
            Piece(ItemLine.Melee, "leggings", ItemType.Leggings, "Tassets", "10").From(2, "10,10,0", HeadMask.NoKneePads).From(4, "10,8,0,9"),
            Piece(ItemLine.Melee, "boots", ItemType.Boots, "Boots", "4"),

            Piece(ItemLine.Tank, "head1", ItemType.Helmet, "Greathelm", "6", HeadMask.All).From(3, "6,0").From(6, "6,0,10").From(7, "6,2").From(8, "6,2,7"),
            Piece(ItemLine.Tank, "head2", ItemType.Helmet, "Visor Helm", "10", HeadMask.All).From(3, "10,1").From(6, "10,1,10").From(7, "10,2").From(8, "10,2,7"),
            Piece(ItemLine.Tank, "chest", ItemType.Chest, "Cuirass", "20,12,14,3").From(3, "20,10,14,3").From(6, "20,11,14,3,9"),
            Piece(ItemLine.Tank, "gloves", ItemType.Gloves, "Gauntlets", "12,4"),
            Piece(ItemLine.Tank, "leggings", ItemType.Leggings, "Greaves", "20,7").From(3, "20,6"),
            Piece(ItemLine.Tank, "boots", ItemType.Boots, "Sabatons", "13"),

            Piece(ItemLine.Rogue, "head1", ItemType.HeadCovering, "Hood", "12", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Rogue, "head2", ItemType.Mask, "Mask", "3", HeadMask.Beard),
            Piece(ItemLine.Rogue, "chest", ItemType.Chest, "Tunic", "7,20,2,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(6, "7,20,2,0,5"),
            Piece(ItemLine.Rogue, "gloves", ItemType.Gloves, "Gloves", "4,8"),
            Piece(ItemLine.Rogue, "leggings", ItemType.Leggings, "Breeches", "24").From(2, "24,10,5", HeadMask.NoKneePads).From(3, "24,2,5").From(4, "24,2,5,9"),
            Piece(ItemLine.Rogue, "boots", ItemType.Boots, "Boots", "18"),

            Piece(ItemLine.Barbarian, "head1", ItemType.HeadCovering, "Bear Hood", "8", HeadMask.Hair | HeadMask.Ears).From(6, "8,6"),
            Piece(ItemLine.Barbarian, "head2", ItemType.HeadCovering, "Wolf Hood", "9", HeadMask.Hair | HeadMask.Ears).From(6, "9,6"),
            Piece(ItemLine.Barbarian, "chest", ItemType.Chest, "Harness", "2,5,3,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(3, "2,5,3,0", HeadMask.NoElbowPads).From(6, "2,5,3,0,3", HeadMask.NoElbowPads)
                .From(7, "2,5,3,0,-1031,-3023", DarkFortressBody).From(8, "2,5,3,0,-1032,-3024", DarkFortressBody),
            Piece(ItemLine.Barbarian, "gloves", ItemType.Gloves, "Bracers", "16,2").From(7, "16,2,-1020,-2021").From(8, "16,2,-1021,-2022"),
            Piece(ItemLine.Barbarian, "leggings", ItemType.Leggings, "Kilt", "1").From(2, "1,10,11", HeadMask.NoKneePads).From(4, "1,10,0,11", HeadMask.NoKneePads)
                .From(7, "1,10,-1031", HeadMask.NoKneePads).From(8, "1,10,-1032", HeadMask.NoKneePads),
            Piece(ItemLine.Barbarian, "boots", ItemType.Boots, "Fur Boots", "9").From(7, "9,-1022").From(8, "9,-1023"),

            Piece(ItemLine.Samurai, "head1", ItemType.Helmet, "Kabuto", "12", HeadMask.All).From(3, "12,5").From(6, "12,3"),
            Piece(ItemLine.Samurai, "head2", ItemType.HeadCovering, "Jingasa", "2", HeadMask.Hair | HeadMask.Ears).From(6, "2,9"),
            Piece(ItemLine.Samurai, "chest", ItemType.Chest, "Yoroi", "26,15,9,0", HeadMask.NoElbowPads).From(3, "26,16,9,0"),
            Piece(ItemLine.Samurai, "gloves", ItemType.Gloves, "Kote", "11,10"),
            Piece(ItemLine.Samurai, "leggings", ItemType.Leggings, "Haidate", "26").From(2, "26,10,9", HeadMask.NoKneePads).From(4, "26,5,4,9"),
            Piece(ItemLine.Samurai, "boots", ItemType.Boots, "Suneate", "10"),

            Piece(ItemLine.Paladin, "head1", ItemType.HeadCovering, "Helm", "3", HeadMask.Hair | HeadMask.Ears).From(3, "3,1").From(6, "3,2"),
            Piece(ItemLine.Paladin, "head2", ItemType.Helmet, "Armet", "7", HeadMask.All).From(3, "7,1").From(6, "7,2"),
            Piece(ItemLine.Paladin, "chest", ItemType.Chest, "Breastplate", "24,1,6,1").From(3, "24,3,6,1").From(6, "24,3,6,1,8"),
            Piece(ItemLine.Paladin, "gloves", ItemType.Gloves, "Gauntlets", "1,7"),
            Piece(ItemLine.Paladin, "leggings", ItemType.Leggings, "Cuisses", "22,3").From(2, "22,3,7"),
            Piece(ItemLine.Paladin, "boots", ItemType.Boots, "Sabatons", "14"),

            Piece(ItemLine.DarkKnight, "head1", ItemType.Helmet, "Dread Helm", "3", HeadMask.All).From(3, "3,1").From(6, "3,6"),
            Piece(ItemLine.DarkKnight, "head2", ItemType.HeadCovering, "Horned Helm", "10", HeadMask.Hair | HeadMask.Ears).From(6, "10,7"),
            Piece(ItemLine.DarkKnight, "chest", ItemType.Chest, "Chestplate", "21,2,15,5").From(3, "21,7,15,5").From(6, "21,4,15,5,2")
                .From(7, "21,4,15,5,-1029,-3021", DarkFortressBody).From(8, "21,4,15,5,-1030,-3022", DarkFortressBody),
            Piece(ItemLine.DarkKnight, "gloves", ItemType.Gloves, "Gauntlets", "1,12").From(7, "1,12,-1018,-2019").From(8, "1,12,-1019,-2020"),
            Piece(ItemLine.DarkKnight, "leggings", ItemType.Leggings, "Legplates", "13,1").From(3, "13,2")
                .From(7, "13,2,-1029", HeadMask.NoKneePads).From(8, "13,2,-1030", HeadMask.NoKneePads),
            Piece(ItemLine.DarkKnight, "boots", ItemType.Boots, "Warboots", "12").From(7, "12,-1020").From(8, "12,-1021"),

            Piece(ItemLine.WitchHunter, "head1", ItemType.Hat, "Buckle Hat", "8").From(3, "8,12").From(6, "8,9"),
            Piece(ItemLine.WitchHunter, "head2", ItemType.Helmet, "Plague Mask", "0", HeadMask.All),
            Piece(ItemLine.WitchHunter, "chest", ItemType.Chest, "Coat", "4,20,18,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(6, "4,20,18,0,8"),
            Piece(ItemLine.WitchHunter, "gloves", ItemType.Gloves, "Gloves", "1,14"),
            Piece(ItemLine.WitchHunter, "leggings", ItemType.Leggings, "Coat Skirt", "5").From(2, "5,10,1", HeadMask.NoKneePads).From(4, "5,10,1,10", HeadMask.NoKneePads),
            Piece(ItemLine.WitchHunter, "boots", ItemType.Boots, "Boots", "4"),

            Piece(ItemLine.Monk, "head1", ItemType.Hat, "Headband", "0").From(6, "0,7"),
            Piece(ItemLine.Monk, "head2", ItemType.HeadCovering, "Hood", "7", HeadMask.Hair | HeadMask.Ears).From(6, "7,7"),
            Piece(ItemLine.Monk, "chest", ItemType.Chest, "Wrap", "8,20,0,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(3, "8,20,3,0"),
            Piece(ItemLine.Monk, "gloves", ItemType.Gloves, "Handwraps", "8,14"),
            Piece(ItemLine.Monk, "leggings", ItemType.Leggings, "Leg Wraps", "23").From(2, "23,10,4", HeadMask.NoKneePads),
            Piece(ItemLine.Monk, "boots", ItemType.Boots, "Footwraps", "3").From(6, "17"),

            // the Taming line (2026-11-22, the game's docs/taming-gear-design.md): furs, hides, feathers and bone
            Piece(ItemLine.Beastmaster, "head1", ItemType.Hat, "Feathered Hat", "7").From(3, "7,11").From(6, "7,4"),
            // a wolf pelt worn as a hood (the Barbarian's Wolf Hood is the same part): never plain, so the two differ
            Piece(ItemLine.Beastmaster, "head2", ItemType.HeadCovering, "Pelt Hood", "9,12", HeadMask.Hair | HeadMask.Ears).From(3, "9,11").From(6, "9,3"),
            Piece(ItemLine.Beastmaster, "chest", ItemType.Chest, "Hide Vest", "12,20,5,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads).From(3, "12,0,5,0", HeadMask.NoElbowPads).From(6, "12,8,5,0,3", HeadMask.NoElbowPads),
            Piece(ItemLine.Beastmaster, "gloves", ItemType.Gloves, "Handler Gloves", "6,16"),
            Piece(ItemLine.Beastmaster, "leggings", ItemType.Leggings, "Hide Breeches", "27").From(2, "27,10,11", HeadMask.NoKneePads).From(4, "27,10,11,2", HeadMask.NoKneePads),
            Piece(ItemLine.Beastmaster, "boots", ItemType.Boots, "Tracking Boots", "7"),

            // Dark Fortress head pieces (2026-11-23, the game's docs/dark-fortress-design.md): a third head look for a few
            // lines from a high tier on, each a model of its own on the head bone (Resources prefabs of the local
            // POLYGON Dark Fortress pack). Their model ids are empty: games before 0.9.7 have no such prefab and draw
            // no head piece for them. Same stats as the line's other head pieces of the tier.
            Piece(ItemLine.Melee, "head3", ItemType.Helmet, "Kettle Helm", "").Starting(4)
                .FromPrefab(4, DarkFortressHead + "Helmet 02", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.DarkKnight, "head3", ItemType.Helmet, "Fortress Helm", "").Starting(6)
                .FromPrefab(6, DarkFortressHead + "Helmet 06", DarkFortressClosed)
                .FromPrefab(7, DarkFortressHead + "Helmet 03", DarkFortressClosed)
                .FromPrefab(8, DarkFortressHead + "Helmet 08", DarkFortressClosed),
            Piece(ItemLine.Paladin, "head3", ItemType.Helmet, "Closed Helm", "").Starting(5)
                .FromPrefab(5, DarkFortressHead + "Helmet 07", DarkFortressClosed),
            Piece(ItemLine.WitchHunter, "head3", ItemType.Helmet, "Inquisitor Helm", "").Starting(5)
                .FromPrefab(5, DarkFortressHead + "Helmet 09", DarkFortressClosed),
            Piece(ItemLine.Barbarian, "head3", ItemType.Hat, "Antler Crown", "").Starting(5)
                .FromPrefab(5, DarkFortressHead + "Crown 03", HeadMask.None)
                .FromPrefab(8, DarkFortressHead + "Mask 03", HeadMask.Brows | HeadMask.Beard),
            Piece(ItemLine.Mage, "head3", ItemType.Hat, "Lich Crown", "").Starting(6)
                .FromPrefab(6, DarkFortressHead + "Mask 01", HeadMask.Brows)
                .FromPrefab(8, DarkFortressHead + "Mask 02", HeadMask.Brows),
        };

        /// <summary>Where the Dark Fortress head pieces' prefabs are (Assets/Resources in the game).</summary>
        internal const string DarkFortressHead = "Character/Appearance/Head/DarkFortress/DF ";

        /// <summary>
        ///     The Dark Fortress armor sets (the game's docs/dark-fortress-armor-test.md): the pack's Knight, Barbarian
        ///     and Necromancer, cut into the game's parts, are the Dark Knight, Barbarian and Mage looks of tiers 7 (a
        ///     colorway of the pack) and 8 (its own colors). Their parts are new in 0.9.7, so their model ids keep the
        ///     tier 6 look in the positions a 0.9.6 game reads and name the new parts with tokens of -1000 and less
        ///     (-(1000 * (position + 1) + index), the game's ItemManager.ParseModelIds): 0.9.6 games draw the tier 6
        ///     look. Their pauldrons and elbows are part of the arm pieces and their knee caps of the hips.
        ///     Part indices (male and female arrays alike): torso, hips 29 to 34; upper arms 21 to 26; lower arms 19 to
        ///     24; hands 18 to 23; legs 20 to 25; in the order Knight 7, Knight 8, Barbarian 7, Barbarian 8,
        ///     Necromancer 7, Necromancer 8 (the game's tools/dark-fortress-armor/armor-sets.cs adds them so).
        /// </summary>
        private const HeadMask DarkFortressBody = HeadMask.NoShoulderPads | HeadMask.NoElbowPads;

        /// <summary>A closed helmet: the face stays (nothing to see through), hair, ears, brows and beard go.</summary>
        private const HeadMask DarkFortressClosed = HeadMask.Hair | HeadMask.Ears | HeadMask.Brows | HeadMask.Beard;

        private enum ClassGearSkill { Magic, Ranged, Attack, Defense, Taming }

        /// <summary>
        ///     What a piece is made from, as { chest, leggings, each of head, gloves and boots }: linen and leather are
        ///     times the tier (one linen and one leather for all tiers, more of it higher up), bars are of the tier's
        ///     metal and not times anything (a higher tier's bar is dearer by itself).
        /// </summary>
        private sealed class ClassGearCost
        {
            public int[] Linen = { 0, 0, 0 };
            public int[] Leather = { 0, 0, 0 };
            public int[] Bars = { 0, 0, 0 };

            public static int Of(int[] amounts, ItemType type) => amounts[type == ItemType.Chest ? 0 : type == ItemType.Leggings ? 1 : 2];
        }

        /// <summary>A set's stat: in percent at tier 1 and tier 8. Fine: one too small for halves and tenths (see <see cref="ClassGearStats"/>).</summary>
        private static (GearStat Stat, double From, double To, bool Fine) Stat(GearStat stat, double from, double to, bool fine = false) => (stat, from, to, fine);

        private sealed class ClassGearLineInfo
        {
            public ItemLine Line;
            /// <summary>The skill whose level a piece asks for (Healing passes a Magic requirement in the game and here).</summary>
            public ClassGearSkill Requires;
            /// <summary>One line saying whom it is for.</summary>
            public string Description;
            /// <summary>
            ///     What a full set of five gives, in percent, at tier 1 and at tier 8 (linear between, in halves).
            ///     Evasion is a rating, not a share: see <see cref="ClassGearEvasion"/>.
            /// </summary>
            public (GearStat Stat, double From, double To, bool Fine)[] Stats;
            /// <summary>What a piece is made from.</summary>
            public ClassGearCost Cost;
        }

        // the recipes: cloth or leather per tier and a bar of trim, or bars and a strap of leather (or linen) per tier
        private static readonly int[] ClassGearCloth = { 4, 3, 2 };
        private static readonly int[] ClassGearOne = { 1, 1, 1 };

        private static readonly ClassGearLineInfo[] ClassGearLines =
        {
            new ClassGearLineInfo
            {
                Line = ItemLine.Mage, Requires = ClassGearSkill.Magic,
                Description = "Cloth for those who train Magic: far less armor than plate, but more magic power, faster casts and better accuracy.",
                Stats = new[] { Stat(GearStat.MagicPowerBonus, 2, 16), Stat(GearStat.CastSpeed, 1, 8), Stat(GearStat.AccuracyBonus, 1, 8) },
                Cost = new ClassGearCost { Linen = ClassGearCloth, Bars = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Healer, Requires = ClassGearSkill.Magic,
                Description = "Cloth for those who train Healing: far less armor than plate, but stronger heals that also shield, shorter cooldowns and more health.",
                Stats = new[] { Stat(GearStat.HealingPowerBonus, 2, 16), Stat(GearStat.HealShield, 1, 8), Stat(GearStat.CooldownReduction, 1, 6), Stat(GearStat.MaxHealthBonus, 1, 5) },
                Cost = new ClassGearCost { Linen = ClassGearCloth, Bars = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Ranger, Requires = ClassGearSkill.Ranged,
                Description = "Leather for those who train Ranged: less armor than plate, but more ranged power, critical hits, attack speed and evasion.",
                Stats = new[] { Stat(GearStat.RangedPowerBonus, 2, 14), Stat(GearStat.CritChance, 0.5, 4), Stat(GearStat.AttackSpeed, 1, 6), Stat(GearStat.Evasion, 1, 6) },
                Cost = new ClassGearCost { Leather = ClassGearCloth, Bars = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Melee, Requires = ClassGearSkill.Attack,
                Description = "Mail for those who train Attack or Strength: a little less armor than plate, but more melee power, critical damage, attack speed and armor penetration.",
                Stats = new[] { Stat(GearStat.MeleePowerBonus, 2, 14), Stat(GearStat.CritDamage, 3, 20), Stat(GearStat.AttackSpeed, 1, 5), Stat(GearStat.ArmorPenetration, 2, 12) },
                Cost = new ClassGearCost { Bars = new[] { 4, 3, 2 }, Leather = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Tank, Requires = ClassGearSkill.Defense,
                Description = "Heavy plate for those who train Defense and stand in front: the armor of plate, with more health, more threat and less damage taken.",
                Stats = new[] { Stat(GearStat.MaxHealthBonus, 2, 12), Stat(GearStat.Threat, 5, 30), Stat(GearStat.DamageReduction, 1, 5) },
                Cost = new ClassGearCost { Bars = new[] { 6, 5, 3 }, Leather = ClassGearOne },
            },

            // the second seven (2026-11-21)
            new ClassGearLineInfo
            {
                Line = ItemLine.Rogue, Requires = ClassGearSkill.Attack,
                Description = "Light leather for those who train Attack and strike first: less armor than plate, but more critical hits, attack speed, melee power and evasion.",
                Stats = new[] { Stat(GearStat.CritChance, 1, 6), Stat(GearStat.AttackSpeed, 1, 8), Stat(GearStat.MeleePowerBonus, 1, 8), Stat(GearStat.Evasion, 1, 6) },
                Cost = new ClassGearCost { Leather = ClassGearCloth, Bars = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Barbarian, Requires = ClassGearSkill.Attack,
                Description = "Hide and fur for those who train Strength: half the armor of plate, but much more melee power, critical damage and health.",
                Stats = new[] { Stat(GearStat.MeleePowerBonus, 2, 16), Stat(GearStat.CritDamage, 3, 20), Stat(GearStat.MaxHealthBonus, 1, 8) },
                // hide all over; metal only where it holds the harness and the kilt together
                Cost = new ClassGearCost { Leather = ClassGearCloth, Bars = new[] { 2, 1, 0 } },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Samurai, Requires = ClassGearSkill.Attack,
                Description = "Lacquered plates for those who train Attack or Strength: a little less armor than plate, but more melee power, accuracy, armor penetration and critical hits.",
                Stats = new[] { Stat(GearStat.MeleePowerBonus, 2, 12), Stat(GearStat.AccuracyBonus, 1, 8), Stat(GearStat.ArmorPenetration, 2, 14), Stat(GearStat.CritChance, 0.5, 3) },
                Cost = new ClassGearCost { Bars = new[] { 4, 3, 2 }, Leather = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Paladin, Requires = ClassGearSkill.Defense,
                Description = "Blessed plate for those who train Defense or Healing: nearly the armor of plate, with more health, healing power and threat, and less damage taken.",
                Stats = new[] { Stat(GearStat.MaxHealthBonus, 1, 8), Stat(GearStat.HealingPowerBonus, 1, 10), Stat(GearStat.DamageReduction, 1, 4), Stat(GearStat.Threat, 3, 15) },
                Cost = new ClassGearCost { Bars = new[] { 5, 4, 3 }, Linen = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.DarkKnight, Requires = ClassGearSkill.Defense,
                Description = "Dark plate for those who train Defense or Strength: nearly the armor of plate, with life steal, thorns that hurt melee attackers, melee power and more health.",
                // thorns: the THORNS enchantment gives 2% to 15% on one item, so a whole set gives a little more than one
                Stats = new[] { Stat(GearStat.LifeSteal, 0.5, 4), Stat(GearStat.Thorns, 3, 20), Stat(GearStat.MeleePowerBonus, 1, 8), Stat(GearStat.MaxHealthBonus, 1, 8) },
                Cost = new ClassGearCost { Bars = new[] { 5, 4, 3 }, Leather = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.WitchHunter, Requires = ClassGearSkill.Ranged,
                Description = "A hunter's coat for those who train Ranged: less armor than plate, but more ranged power and critical hits, and more damage to bosses and the undead.",
                Stats = new[] { Stat(GearStat.RangedPowerBonus, 2, 12), Stat(GearStat.BossDamage, 1, 8), Stat(GearStat.UndeadDamage, 2, 12), Stat(GearStat.CritChance, 0.5, 3) },
                // leather lined with linen: as much cloth as the Ranger's leather, and the bar of trim every piece has
                Cost = new ClassGearCost { Leather = new[] { 3, 2, 1 }, Linen = ClassGearOne, Bars = ClassGearOne },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Monk, Requires = ClassGearSkill.Magic,
                Description = "Wraps for those who train Healing: far less armor than plate, but more healing power, shorter cooldowns, health that comes back in a fight, more health and evasion.",
                // combat regen is a share of all health every second (the Vitality tree's Regeneration gives 0.2% a rank, its
                // capstone 0.6%): a set gives 0.1% to 0.6%
                Stats = new[] { Stat(GearStat.HealingPowerBonus, 1, 10), Stat(GearStat.CooldownReduction, 1, 6), Stat(GearStat.CombatRegen, 0.1, 0.6, fine: true), Stat(GearStat.MaxHealthBonus, 1, 6), Stat(GearStat.Evasion, 1, 6) },
                Cost = new ClassGearCost { Linen = ClassGearCloth, Bars = ClassGearOne },
            },

            // the Taming line (2026-11-22): needs Taming (ItemLines.TamingLevelFor, no column of its own), so no
            // requirement column is set. The wearer's own numbers work with any weapon; the rest is for the pet, which
            // reads them where it reads its owner's Beastmaster tree (the same stats), and the tame chance.
            new ClassGearLineInfo
            {
                Line = ItemLine.Beastmaster, Requires = ClassGearSkill.Taming,
                Description = "Furs and hides for those who fight beside a battle pet: less armor than plate, a stronger, sturdier pet that is up sooner, a better chance to tame, and more health and critical hits of your own with any weapon.",
                Stats = new[]
                {
                    Stat(GearStat.MaxHealthBonus, 1, 8), Stat(GearStat.CritChance, 0.5, 3),
                    Stat(GearStat.PetPower, 4, 75), Stat(GearStat.PetHealth, 3, 20), Stat(GearStat.PetRecovery, 2, 15),
                    Stat(GearStat.PetGathering, 1, 10), Stat(GearStat.TameChance, 2, 15),
                },
                Cost = new ClassGearCost { Leather = ClassGearCloth, Bars = ClassGearOne },
            },
        };

        /// <summary>The item id of a class gear piece: fixed, so it is the same in every database and survives a rename.</summary>
        public static Guid ClassGearItemId(ItemLine line, int tier, string key)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes("ravenfall-class-gear-" + line.ToString().ToLowerInvariant() + "-" + tier + "-" + key));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash);
        }

        /// <summary>
        ///     A set's total of a stat at a tier, in percent: linear from tier 1 to tier 8, in halves (in steps of 0.05
        ///     for a fine one).
        /// </summary>
        internal static decimal ClassGearSetTotal(double from, double to, int tier, bool fine = false)
        {
            var raw = (decimal)from + ((decimal)to - (decimal)from) * (tier - 1) / (ItemLines.Tiers - 1);
            var steps = fine ? 20 : 2;
            return Math.Round(raw * steps, MidpointRounding.AwayFromZero) / steps;
        }

        /// <summary>
        ///     Evasion is a rating in this game (2 per Defense level plus gear), not a share. A set's total is what takes
        ///     about that many points off the hit chance of an enemy of the tier's level: 4 x level x the share.
        /// </summary>
        internal static int ClassGearEvasion(decimal percent, int level) => Math.Max(1, (int)Math.Round(percent / 100m * 4 * level, MidpointRounding.AwayFromZero));

        /// <summary>
        ///     One piece's share of a set total (in whole units): 15% each for head, gloves and boots, 25% for the leggings
        ///     and the chest takes what is left (about 30%), so the five pieces add up to the total exactly.
        /// </summary>
        internal static int ClassGearShare(int total, ItemType type)
        {
            var other = (total * 15 + 49) / 100;   // a half goes down
            var leggings = (total * 25 + 50) / 100; // a half goes up
            switch (type)
            {
                case ItemType.Chest: return total - leggings - 3 * other;
                case ItemType.Leggings: return leggings;
                default: return other;
            }
        }

        /// <summary>The stats of a piece beyond its armor, in the text form RavenNest.Combat.GearStats reads.</summary>
        internal static string ClassGearStats(ItemLine line, int tier, ItemType type)
        {
            var info = ClassGearLines.First(x => x.Line == line);
            var level = ItemLines.TierLevels[tier - 1];
            var stats = new RavenNest.Combat.GearStats();
            foreach (var (stat, from, to, fine) in info.Stats)
            {
                var percent = ClassGearSetTotal(from, to, tier, fine);
                if (fine)
                {
                    // a share too small for tenths (combat regen, a share of all health every second): a piece's
                    // part in hundredths of a percent
                    var hundredths = ClassGearShare((int)(percent * 100), type);
                    stats[stat] = (double)(hundredths / 10000m);
                }
                else if (RavenNest.Combat.GearStats.IsShare(stat))
                {
                    // shares in tenths of a percent, so no piece says "0.37%"
                    var tenths = ClassGearShare((int)(percent * 10), type);
                    stats[stat] = (double)(tenths / 1000m);
                }
                else
                {
                    stats[stat] = ClassGearShare(ClassGearEvasion(percent, level), type);
                }
            }
            return stats.ToString();
        }

        private static ItemType ClassGearPlateSlot(ItemType type)
        {
            switch (type)
            {
                case ItemType.Chest:
                case ItemType.Gloves:
                case ItemType.Leggings:
                case ItemType.Boots:
                    return type;
                default:
                    return ItemType.Helmet; // hats, hoods and helms all count as the helmet
            }
        }

        /// <summary>
        ///     The armor a plate piece of a slot has at a required Defense level, from the metal sets as they are now:
        ///     the set of that level (the better one where two share a level, as Bronze and Iron do), or a point on the
        ///     line between the two sets around it.
        /// </summary>
        private static double PlateArmorAt(IReadOnlyList<ItemSet> plate, ItemType slot, int level)
        {
            var points = new SortedDictionary<int, int>();
            foreach (var set in plate)
            {
                Item piece;
                switch (slot)
                {
                    case ItemType.Chest: piece = set.Chest; break;
                    case ItemType.Gloves: piece = set.Gloves; break;
                    case ItemType.Leggings: piece = set.Leggings; break;
                    case ItemType.Boots: piece = set.Boots; break;
                    default: piece = set.Helmet; break;
                }
                if (piece == null || piece.ArmorPower <= 0) continue;
                var required = Math.Max(1, piece.RequiredDefenseLevel);
                if (!points.TryGetValue(required, out var armor) || piece.ArmorPower > armor) points[required] = piece.ArmorPower;
            }

            if (points.Count == 0) return 0;
            var below = points.First();
            foreach (var point in points)
            {
                if (point.Key == level) return point.Value;
                if (point.Key > level)
                {
                    if (below.Key >= point.Key) return point.Value; // below the first set
                    return below.Value + (point.Value - below.Value) * (double)(level - below.Key) / (point.Key - below.Key);
                }
                below = point;
            }
            return below.Value; // above the last set
        }

        /// <summary>
        ///     The item ids of a line's pieces at a tier, by slot: head (both head pieces), chest, gloves, leggings,
        ///     boots. Empty for a line without pieces. For whoever hands class gear out (the tower's chests).
        /// </summary>
        public static IReadOnlyList<Guid[]> ClassGearSlots(ItemLine line, int tier)
        {
            var slots = new List<Guid[]>();
            if (line == ItemLine.Plate || tier < 1 || tier > ItemLines.Tiers) return slots;
            foreach (var slot in new[] { ItemType.Helmet, ItemType.Chest, ItemType.Gloves, ItemType.Leggings, ItemType.Boots })
            {
                var ids = ClassGearPieces.Where(p => p.Line == line && p.MinTier <= tier && ClassGearPlateSlot(p.Type) == slot).Select(p => ClassGearItemId(line, tier, p.Key)).ToArray();
                if (ids.Length > 0) slots.Add(ids);
            }
            return slots;
        }

        /// <summary>The lines that have pieces, the first one first.</summary>
        public static IReadOnlyList<ItemLine> ClassGearLinesMade => ClassGearLines.Select(x => x.Line).Where(l => ClassGearPieces.Any(p => p.Line == l)).ToList();

        private void EnsureClassGear()
        {
            var known = GetKnownItems();
            var farming = RavenNest.Models.Skill.Farming;
            var slayer = RavenNest.Models.Skill.Slayer;

            // the two cloths: flax is farmed and woven into linen, hide comes from fighting creatures and is tanned into
            // leather. One linen and one leather for every tier: the higher tiers take more of it.
            var flax = GetOrCreateItem(items.Entities, "Flax", "A tall blue-flowered plant. Its fibers are woven into linen.", ItemCategory.Resource, ItemType.Farming);
            var linen = GetOrCreateItem(items.Entities, "Linen", "Cloth woven from flax, for robes and vestments.", ItemCategory.Resource, ItemType.Crafting);
            var hide = GetOrCreateItem(items.Entities, "Hide", "The raw skin of a beast, taken while fighting. Tanned, it becomes leather.", ItemCategory.Resource, ItemType.Crafting);
            var leather = GetOrCreateItem(items.Entities, "Leather", "Tanned hide, for jerkins, straps and the padding under mail and plate.", ItemCategory.Resource, ItemType.Crafting);
            // a drop of level 1 would be worth 1 coin (flax, a level 5 crop, is worth 5); the cloths are worth their two
            // pieces, said here so the first start prices the gear from them whatever order the items come in
            if (hide.ShopSellPrice <= 1) hide.ShopSellPrice = 5;
            if (linen.ShopSellPrice <= 1) linen.ShopSellPrice = 10;
            if (leather.ShopSellPrice <= 1) leather.ShopSellPrice = 10;

            EnsureDropRate(5, flax, 20, 0.15, farming);
            // FightingTaskProcessor rolls the Slayer drops while a combat skill is trained
            EnsureDropRate(1, hide, 20, 0.2, slayer);
            EnsureExactCraftingRecipe(1, linen, new Ingredient { Item = flax, Amount = 2 });
            EnsureExactCraftingRecipe(1, leather, new Ingredient { Item = hide, Amount = 2 });

            // the metal of each tier's level, for trim and buckles or for the piece itself
            var bars = new[] { known.BronzeBar, known.MithrilBar, known.RuneBar, known.AbraxasBar, known.LioniteBar, known.AtlarusBar, known.ElderMithrilBar, known.ElderEthereumBar };
            var plate = new[]
            {
                known.Bronze, known.Iron, known.Steel, known.Black, known.Mithril, known.Adamantite, known.Rune, known.Dragon, known.Abraxas,
                known.Phantom, known.Lionsbane, known.Ether, known.Ancient, known.Atlarus,
                known.ElderBronze, known.ElderIron, known.ElderSteel, known.ElderBlack, known.ElderMithril, known.ElderAdamantite, known.ElderRune,
                known.ElderDragon, known.ElderAbraxas, known.ElderPhantom, known.ElderLionsbane, known.ElderEther, known.ElderAncient, known.ElderAtlarus,
            };

            var byName = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in items.Entities)
            {
                if (!string.IsNullOrEmpty(existing.Name)) byName[existing.Name] = existing;
            }

            var created = 0;
            var corrected = 0;
            foreach (var piece in ClassGearPieces)
            {
                var info = ClassGearLines.First(x => x.Line == piece.Line);
                var factor = ItemLines.ArmorFactor(piece.Line);
                for (var tier = piece.MinTier; tier <= ItemLines.Tiers; ++tier)
                {
                    var material = ItemLines.Material(piece.Line, tier);
                    var level = ItemLines.TierLevels[tier - 1];
                    var id = ClassGearItemId(piece.Line, tier, piece.Key);
                    var look = piece.LookAt(tier);
                    var name = material + " " + piece.Name;

                    if (byName.TryGetValue(name, out var sameName) && sameName.Id != id)
                    {
                        logger.LogError("Class gear: '{Name}' is already the name of another item ({Id}); the piece was not made.", name, sameName.Id);
                        continue;
                    }

                    // a shade above so 0.4 x 20 is 8 and not 9; rounded up, so a piece never has less than its share of plate
                    var armor = (int)Math.Ceiling(PlateArmorAt(plate, ClassGearPlateSlot(piece.Type), level) * factor - 0.000001);
                    var stats = ClassGearStats(piece.Line, tier, piece.Type);

                    var item = GetItem(id);
                    var isNew = item == null;
                    if (isNew)
                    {
                        item = new Item
                        {
                            Id = id,
                            Name = name,
                            RequiredCraftingLevel = 1000, // the old columns, as on every item made here: the recipe is the record
                            RequiredCookingLevel = 1000,
                            Craftable = false,
                        };
                    }

                    var changed = false;
                    void Fix<T>(T current, T wanted, Action<T> set)
                    {
                        if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                        set(wanted);
                        changed = true;
                    }

                    Fix(item.Name, name, v => { byName.Remove(item.Name ?? ""); item.Name = v; });
                    Fix(item.Description, info.Description, v => item.Description = v);
                    Fix(item.Category, (int)ItemCategory.Armor, v => item.Category = v);
                    Fix(item.Type, (int)piece.Type, v => item.Type = v);
                    Fix(item.Material, (int)material, v => item.Material = v);
                    Fix(item.Level, level, v => item.Level = v);
                    Fix(item.MaleModelId, look.MaleModels, v => item.MaleModelId = v);
                    Fix(item.FemaleModelId, look.FemaleModels, v => item.FemaleModelId = v);
                    Fix(item.HeadMask, (int)look.Mask, v => item.HeadMask = v);
                    // the parts and the tier's material are the look, or a model of its own (the Dark Fortress heads)
                    Fix(item.GenericPrefab, look.Prefab, v => item.GenericPrefab = v);
                    Fix(item.MalePrefab, null, v => item.MalePrefab = v);
                    Fix(item.FemalePrefab, null, v => item.FemalePrefab = v);
                    Fix(item.IsGenericModel, look.Prefab != null, v => item.IsGenericModel = v);
                    Fix(item.ArmorPower, armor, v => item.ArmorPower = v);
                    Fix(item.WeaponAim, 0, v => item.WeaponAim = v);
                    Fix(item.WeaponPower, 0, v => item.WeaponPower = v);
                    Fix(item.RangedAim, 0, v => item.RangedAim = v);
                    Fix(item.RangedPower, 0, v => item.RangedPower = v);
                    Fix(item.MagicAim, 0, v => item.MagicAim = v);
                    Fix(item.MagicPower, 0, v => item.MagicPower = v);
                    Fix(item.ExtraStats, stats, v => item.ExtraStats = v);
                    Fix(item.RequiredMagicLevel, info.Requires == ClassGearSkill.Magic ? level : 0, v => item.RequiredMagicLevel = v);
                    Fix(item.RequiredRangedLevel, info.Requires == ClassGearSkill.Ranged ? level : 0, v => item.RequiredRangedLevel = v);
                    Fix(item.RequiredAttackLevel, info.Requires == ClassGearSkill.Attack ? level : 0, v => item.RequiredAttackLevel = v);
                    Fix(item.RequiredDefenseLevel, info.Requires == ClassGearSkill.Defense ? level : 0, v => item.RequiredDefenseLevel = v);
                    Fix(item.RequiredSlayerLevel, 0, v => item.RequiredSlayerLevel = v);
                    Fix(item.ShopBuyPrice, 0L, v => item.ShopBuyPrice = v); // made, not bought; the sell price follows the recipe (ApplyVendorPrices)
                    Fix(item.Soulbound, false, v => item.Soulbound = v);
                    Fix(item.Hidden, false, v => item.Hidden = v);

                    if (isNew)
                    {
                        item.Modified = DateTime.UtcNow;
                        Add(item);
                        created++;
                    }
                    else if (changed)
                    {
                        item.Modified = DateTime.UtcNow;
                        corrected++;
                    }
                    byName[name] = item;

                    // the recipe: Crafting at the tier's level, always a success, as the metal tiers
                    var bar = bars[tier - 1];
                    var wanted = new List<Ingredient>();
                    var linenAmount = ClassGearCost.Of(info.Cost.Linen, piece.Type) * tier;
                    var leatherAmount = ClassGearCost.Of(info.Cost.Leather, piece.Type) * tier;
                    var barAmount = ClassGearCost.Of(info.Cost.Bars, piece.Type);
                    if (linenAmount > 0) wanted.Add(new Ingredient { Item = linen, Amount = linenAmount });
                    if (leatherAmount > 0) wanted.Add(new Ingredient { Item = leather, Amount = leatherAmount });
                    if (barAmount > 0) wanted.Add(new Ingredient { Item = bar, Amount = barAmount });
                    var ingredients = wanted.ToArray();

                    var recipe = EnsureExactCraftingRecipe(level, item, ingredients);
                    if (recipe.Name != item.Name) recipe.Name = item.Name;
                    if (recipe.Description != item.Description) recipe.Description = item.Description;
                }
            }

            if (created > 0 || corrected > 0)
                logger.LogInformation("Class gear: {Created} item(s) created, {Corrected} corrected.", created, corrected);
        }

        /// <summary>
        ///     A crafting recipe with exactly these ingredients: <see cref="EnsureRecipe"/> only looks at how many
        ///     there are and what they add up to, so a changed ingredient of the same amount would stay.
        /// </summary>
        private ItemRecipe EnsureExactCraftingRecipe(int level, Item target, params Ingredient[] wanted)
        {
            var recipe = GetItemRecipeByItem(target.Id);
            if (recipe != null)
            {
                var have = GetRecipeIngredients(recipe.Id);
                var same = have.Count == wanted.Length && wanted.All(w => have.Any(h => h.ItemId == w.Item.Id && h.Amount == w.Amount));
                if (!same)
                {
                    foreach (var old in have.ToList()) Remove(old);
                }
            }
            return EnsureCraftingRecipe(level, target, wanted);
        }
    }
}

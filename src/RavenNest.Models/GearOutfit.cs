using System;

namespace RavenNest.Models
{
    /// <summary>
    /// What "!equip tank" or "!equip rogue" asks for (v0.9.7): an outfit by role or by class gear line, whatever the
    /// character trains. The game and the server both read the word here, so they dress a character the same way.
    ///
    /// Two kinds:
    ///  - a role (<see cref="OnlyLine"/> false): "equip best" as if <see cref="Skill"/> were trained, and then the
    ///    class gear of that skill is worn whatever other armor is worth (someone who asks for ranger gear gets it,
    ///    also when their plate ranks higher). The lines that fit the skill are narrowed to one as ever
    ///    (<see cref="GearScore.OneLine(int, ItemLine, ItemMaterial[], int[], int[], bool[])"/>); a word that is also
    ///    a line's name ("tank", "melee", "ranger", "mage", "healer") takes that line when it is as good as the
    ///    best of them (<see cref="Line"/>), and the better line otherwise;
    ///  - a line by name (<see cref="OnlyLine"/> true): the best piece per slot of <see cref="Line"/> and nothing
    ///    else changes. <see cref="ItemLine.Plate"/> is the classic metal armor. A line whose name is a role's word
    ///    is asked for with "line" after it ("tank line").
    ///
    /// The game loads this library on its own runtime: plain types only (see <see cref="GearScore"/>).
    /// </summary>
    public sealed class GearOutfit
    {
        private GearOutfit(string word, Skill skill, ItemLine line, bool onlyLine)
        {
            Word = word;
            Skill = skill;
            Line = line;
            OnlyLine = onlyLine;
        }

        /// <summary>The word as both sides know it (lower case, no spaces): what the game sends to the server.</summary>
        public string Word { get; }

        /// <summary>The skill to dress for; <see cref="Skill.None"/> for a line by name.</summary>
        public Skill Skill { get; }

        /// <summary>
        /// For a line by name, the line. For a role, the line the word names, which wins among equals;
        /// <see cref="ItemLine.Plate"/> when the word names none ("defense", "ranged").
        /// </summary>
        public ItemLine Line { get; }

        /// <summary>Only pieces of <see cref="Line"/> are worn: a line asked for by name.</summary>
        public bool OnlyLine { get; }

        /// <summary>The lines that fit this outfit, as a set (<see cref="ItemLines.Has"/>); 0 for plate.</summary>
        public int Fits => OnlyLine ? ItemLines.Bit(Line) : ItemLines.Fits(Skill);

        /// <summary>
        /// Is a piece of this material part of the outfit: of the line that was asked for by name, or for a role
        /// class gear of a line that fits its skill. A worn piece of the outfit is only replaced by another one.
        /// </summary>
        public bool Covers(ItemMaterial material)
        {
            if (OnlyLine) return ItemLines.IsOf(Line, material);
            return ItemLines.IsClassGear(material) && ItemLines.Has(Fits, ItemLines.Of(material));
        }

        /// <summary>
        /// Reads the whole argument of "!equip": a role, a skill or a line's name, in any case, with or without the
        /// space in "dark knight" and "witch hunter"; a line's name with "line" after it is that line and no other
        /// ("tank line"). Null for anything else (an item's name, "all").
        /// </summary>
        public static GearOutfit Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var chars = new System.Text.StringBuilder(text.Length);
            foreach (var c in text.Trim())
            {
                if (c == ' ' || c == '-' || c == '_') continue;
                chars.Append(char.ToLowerInvariant(c));
            }
            var word = chars.ToString();
            switch (word)
            {
                // roles: a skill, or what players call the one who trains it
                case "attack":
                case "atk":
                    return new GearOutfit(word, Skill.Attack, ItemLine.Plate, false);
                case "melee":
                    return new GearOutfit(word, Skill.Attack, ItemLine.Melee, false);
                case "strength":
                case "str":
                    return new GearOutfit(word, Skill.Strength, ItemLine.Plate, false);
                case "defense":
                case "defence":
                case "def":
                    return new GearOutfit(word, Skill.Defense, ItemLine.Plate, false);
                case "tank":
                    return new GearOutfit(word, Skill.Defense, ItemLine.Tank, false);
                case "ranged":
                case "range":
                case "archer":
                case "bow":
                    return new GearOutfit(word, Skill.Ranged, ItemLine.Plate, false);
                case "ranger":
                    return new GearOutfit(word, Skill.Ranged, ItemLine.Ranger, false);
                case "magic":
                    return new GearOutfit(word, Skill.Magic, ItemLine.Plate, false);
                case "mage":
                    return new GearOutfit(word, Skill.Magic, ItemLine.Mage, false);
                case "healing":
                case "heal":
                    return new GearOutfit(word, Skill.Healing, ItemLine.Plate, false);
                case "healer":
                    return new GearOutfit(word, Skill.Healing, ItemLine.Healer, false);

                // lines by name: the ones whose name is not a role's word
                case "rogue":
                case "barbarian":
                case "samurai":
                case "paladin":
                case "darkknight":
                case "witchhunter":
                case "monk":
                case "plate":
                    return new GearOutfit(word, Skill.None, LineNamed(word).Value, true);

                // the Taming line by its name or the one who wears it: a line, never a role, since no combat skill
                // fits it (the gear a tamer fights in is a choice, not the ranking's)
                case "beastmaster":
                case "tamer":
                case "taming":
                    return new GearOutfit(word, Skill.None, ItemLine.Beastmaster, true);
            }

            // any line with "line" after its name: the way to the five lines whose name is a role's word
            if (word.Length > 4 && word.EndsWith("line", StringComparison.Ordinal))
            {
                var line = LineNamed(word.Substring(0, word.Length - 4));
                if (line != null) return new GearOutfit(word, Skill.None, line.Value, true);
            }
            return null;
        }

        private static ItemLine? LineNamed(string name)
        {
            switch (name)
            {
                case "mage": return ItemLine.Mage;
                case "healer": return ItemLine.Healer;
                case "ranger": return ItemLine.Ranger;
                case "melee": return ItemLine.Melee;
                case "tank": return ItemLine.Tank;
                case "rogue": return ItemLine.Rogue;
                case "barbarian": return ItemLine.Barbarian;
                case "samurai": return ItemLine.Samurai;
                case "paladin": return ItemLine.Paladin;
                case "darkknight": return ItemLine.DarkKnight;
                case "witchhunter": return ItemLine.WitchHunter;
                case "monk": return ItemLine.Monk;
                case "beastmaster": return ItemLine.Beastmaster;
                case "plate": return ItemLine.Plate;
                default: return null;
            }
        }

        /// <summary>A line's name as players read it, in lower case: "dark knight", "witch hunter", "plate".</summary>
        public static string NameOf(ItemLine line)
        {
            switch (line)
            {
                case ItemLine.DarkKnight: return "dark knight";
                case ItemLine.WitchHunter: return "witch hunter";
                default: return line.ToString().ToLowerInvariant();
            }
        }
    }
}

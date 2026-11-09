using System;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        ///     American spelling in what players read (user, 2026-11-09: the skill is Defense, so Armor, not Armour).
        ///     Item descriptions written at creation keep their text in the database, so the British words in them
        ///     follow here at startup; gems rewrite theirs every start anyway.
        /// </summary>
        private static readonly (string British, string American)[] Spellings =
        {
            ("armour", "armor"), ("Armour", "Armor"), ("colour", "color"), ("Colour", "Color"),
            ("favourite", "favorite"), ("jewellery", "jewelry"), ("metres", "meters"), ("rumours", "rumors"),
        };

        private void EnsureAmericanSpelling()
        {
            foreach (var item in items.Entities)
            {
                var d = item?.Description;
                if (string.IsNullOrEmpty(d)) continue;
                var fixedText = d;
                foreach (var (british, american) in Spellings) fixedText = fixedText.Replace(british, american, StringComparison.Ordinal);
                if (fixedText != d) item.Description = fixedText;
            }
        }
    }
}

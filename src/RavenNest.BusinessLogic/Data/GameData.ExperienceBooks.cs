using System;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        ///     The experience books (ExperienceBooks): a small, a medium and a large one per category of skills, given by quests
        ///     in place of experience for a skill at the highest level. Scrolls, so the game lets them be used; bound to
        ///     their owner (no trading, gifting, marketplace or clan bank) and worth nothing at the vendor.
        /// </summary>
        private void EnsureExperienceBooks()
        {
            var changed = 0;
            foreach (var book in ExperienceBooks.All)
            {
                var percent = (int)Math.Round(book.Share * 100);
                var description = "Read it to give " + percent + "% of a level to " + ExperienceBooks.SkillNames(book.Category)
                    + ": !use " + book.Name.ToLowerInvariant() + " <skill>. Quests give it in place of experience for a skill at the highest level.";
                var item = GetOrCreateItem(items.Entities, book.Name, description, ItemCategory.Scroll, ItemType.Scroll);
                // the share a size carries can change (the large one was 50% before the medium one came): the words follow
                if (!item.Soulbound || item.ShopSellPrice != 0 || item.Craftable || item.Description != description)
                {
                    item.Description = description;
                    item.Soulbound = true;
                    item.ShopSellPrice = 0;
                    item.Craftable = false;
                    item.Modified = DateTime.UtcNow;
                    changed++;
                }
            }

            if (changed > 0) logger.LogInformation("Experience books: {Count} book item(s) set up.", changed);
        }
    }
}

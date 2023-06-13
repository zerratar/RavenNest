using RavenNest.DataModels;
using Shinobytes.Console.Forms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Tools.Actions
{
    public class RestoreInventoryItems
    {

        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }

        private readonly string[] folders;

        public RestoreInventoryItems(ProgressBar toolProgress, TextBlock toolStatus, params string[] folders)
        {
            this.ToolProgress = toolProgress;
            this.ToolStatus = toolStatus;
            this.folders = folders;
        }

        internal void Apply()
        {
            var wasIndeterminate = this.ToolProgress.Indeterminate;
            var itemsDictionary = new Dictionary<Guid, InventoryItem>();
            var replacedCount = 0;
            foreach (var folder in folders)
            {
                var items = BackupLib.Backups.GetInventoryItems(folder);
                foreach (var item in items)
                {
                    // first make sure we take the most up to date one
                    // either it be the one with biggest stack OR if either of them has an enchantment
                    if (!itemsDictionary.ContainsKey(item.Id))
                    {
                        // easy one, just set it.
                        itemsDictionary[item.Id] = item;
                    }
                    else
                    {
                        if (itemsDictionary.TryGetValue(item.Id, out var existing))
                        {
                            if (existing.Equipped != item.Equipped)
                            {
                                existing.Equipped = false;
                                item.Equipped = false;
                            }

                            if (!string.IsNullOrEmpty(existing.Enchantment) || !string.IsNullOrEmpty(item.Enchantment))
                            {
                                // new one did not have an enchantment but the old one did
                                if (string.IsNullOrEmpty(item.Enchantment))
                                {
                                    continue;
                                }

                                if (existing.Enchantment == null || item.Enchantment.Length > existing.Enchantment.Length)
                                {
                                    itemsDictionary[item.Id] = item;
                                    replacedCount++;
                                }
                                continue;
                            }

                            if (existing.Amount >= item.Amount)
                            {
                                continue;
                            }
                        }

                        itemsDictionary[item.Id] = item;
                        replacedCount++;
                    }
                }
            }

            System.IO.File.WriteAllText("RavenNest.DataModels.InventoryItem.json", Newtonsoft.Json.JsonConvert.SerializeObject(itemsDictionary.Values.ToList()));
        }
    }
}

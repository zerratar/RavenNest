using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RavenNest.DataModels;
using RavenNest.Models;
using Appearance = RavenNest.DataModels.Appearance;
using InventoryItem = RavenNest.DataModels.InventoryItem;
using Item = RavenNest.DataModels.Item;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;

namespace RavenNest.BusinessLogic.Extensions
{
    public static class ModelMapper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Appearance Map(Appearance data)
        {
            return DataMapper.Map<Models.Appearance, Appearance>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Statistics Map(DataModels.Statistics data)
        {
            return DataMapper.Map<Models.Statistics, DataModels.Statistics>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Skills Map(Skills data)
        {
            return DataMapper.Map<Models.Skills, Skills>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.GameEvent Map(DataModels.GameEvent data)
        {
            return DataMapper.Map<Models.GameEvent, DataModels.GameEvent>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Resources Map(Resources data)
        {
            return DataMapper.Map<Models.Resources, Resources>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Item Map(Item itemsItem)
        {
            return DataMapper.Map<Models.Item, Item>(itemsItem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Item Map(Models.Item itemsItem)
        {
            return DataMapper.Map<Item, Models.Item>(itemsItem);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.SyntyAppearance Map(DataModels.SyntyAppearance appearance)
        {
            return DataMapper.Map<Models.SyntyAppearance, DataModels.SyntyAppearance>(appearance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.InventoryItem Map(InventoryItem items)
        {
            return new Models.InventoryItem
            {
                Id = items.Id,
                Amount = items.Amount.GetValueOrDefault(),
                Equipped = items.Equipped,
                ItemId = items.ItemId
                //Item = Map(items.Item)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<Models.InventoryItem> Map(ICollection<InventoryItem> items)
        {
            return items.Select(Map).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this Character character, User user)
        {
            return new Player
            {
                UserName = user.UserName,
                UserId = user.UserId,
                Name = character.Name,
                Appearance = Map(character.SyntyAppearance),
                Resources = Map(character.Resources),
                Skills = Map(character.Skills),
                InventoryItems = Map(character.InventoryItem),
                Statistics = Map(character.Statistics),
                Local = character.Local,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this User user, Character character)
        {
            return new Player
            {
                UserName = user.UserName,
                UserId = user.UserId,
                Name = character.Name,
                Appearance = Map(character.SyntyAppearance),
                Resources = Map(character.Resources),
                Skills = Map(character.Skills),
                InventoryItems = Map(character.InventoryItem),
                Local = character.Local,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault()
            };
        }
    }
}

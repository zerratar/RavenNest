using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;
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
        public static Models.CharacterState Map(DataModels.CharacterState data)
        {
            return DataMapper.Map<Models.CharacterState, DataModels.CharacterState>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Statistics Map(DataModels.Statistics data)
        {
            return DataMapper.Map<Models.Statistics, DataModels.Statistics>(data);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Clan Map(IGameData gameData, DataModels.Clan data)
        {
            if (data == null) return null;
            var user = gameData.GetUser(data.UserId);
            return new Models.Clan()
            {
                Id = data.Id,
                Logo = data.Logo,
                Name = data.Name,
                Owner = user.UserId
            };
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
        public static Models.Item Map(IGameData gameData, Item item)
        {
            var mapped = DataMapper.Map<Models.Item, Item>(item);

            mapped.CraftingRequirements = gameData.GetCraftingRequirements(item.Id)
                .Select(x => DataMapper.Map<Models.ItemCraftingRequirement, DataModels.ItemCraftingRequirement>(x))
                .ToList();

            return mapped;
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
        public static IReadOnlyList<Models.InventoryItem> Map(IEnumerable<InventoryItem> items)
        {
            return items.Select(Map).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this Character character, IGameData gameData, User user)
        {
            return user.Map(gameData, character);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this User user, IGameData gameData, Character character)
        {
            return new Player
            {
                UserName = user.UserName,
                UserId = user.UserId,
                Name = character.Name,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = Map(gameData.GetSkills(character.SkillsId)),
                State = Map(gameData.GetState(character.StateId)),
                InventoryItems = Map(gameData.GetAllPlayerItems(character.Id)),
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                Clan = Map(gameData, gameData.GetClan(character.ClanId.GetValueOrDefault())),
                Local = character.Local,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault()
            };
        }
    }
}

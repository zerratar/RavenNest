using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
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
        public static Models.GameSession Map(IGameData gameData, DataModels.GameSession data)
        {
            var session = DataMapper.Map<Models.GameSession, DataModels.GameSession>(data);
            var user = gameData.GetUser(session.UserId);
            if (user == null)
                return null;

            session.TwitchUserId = user.UserId;
            session.UserName = user.UserName;
            session.AdminPrivileges = user.IsAdmin.GetValueOrDefault();
            session.ModPrivileges = user.IsModerator.GetValueOrDefault();
            session.Players = gameData.GetSessionCharacters(data)
                .Select(x => Map(gameData, x))
                .Where(x => x != null)
                .ToList();

            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.GameSessionPlayer Map(IGameData gameData, DataModels.Character character)
        {
            var user = gameData.GetUser(character.UserId);
            if (user == null) return null;
            return new GameSessionPlayer
            {
                TwitchUserId = user.UserId,
                UserName = user.UserName,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
            };
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
            if (user == null) return null;
            return new Models.Clan()
            {
                Id = data.Id,
                Logo = data.Logo,
                Name = data.Name,
                Owner = user.UserId
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.ClanRole Map(DataModels.ClanRole data, DataModels.CharacterClanMembership membership = null)
        {
            return new Models.ClanRole
            {
                Id = data.Id,
                Level = data.Level,
                Name = data.Name,
                Joined = membership?.Joined
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Models.Skills Map(Skills data)
        {
            return DataMapper.Map<Models.Skills, Skills>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SkillsExtended MapForWebsite(Skills data)
        {
            return DataMapper.Map<SkillsExtended, Skills>(data);
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
                ItemId = items.ItemId,
                Tag = items.Tag
                //Item = Map(items.Item)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<Models.InventoryItem> Map(IEnumerable<InventoryItem> items)
        {
            return items.Select(Map).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this Character character, IGameData gameData, User user, bool rejoin = false, bool isSessionPlayer = false)
        {
            return user.Map(gameData, character, rejoin, isSessionPlayer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsitePlayer MapForWebsite(this Character character, IGameData gameData, User user)
        {
            return user.MapForWebsite(gameData, character);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(
            this User user,
            IGameData gameData,
            Character character,
            bool rejoin = false,
            bool inGame = false)
        {
            var playItems = gameData.GetAllPlayerItems(character.Id);
            if (inGame && character.UserIdLock != null)
            {
                var targetStreamUser = gameData.GetUser(character.UserIdLock.Value);
                // if we got streamer tokens, only send the ones for the appropriate streamer
                playItems = playItems.Where(x => x.Tag == null || x.Tag == targetStreamUser.UserId).ToList();
            }

            var invItems = Map(playItems);
            if (user == null)
                return null;

            var clanMembership = gameData.GetClanMembership(character.Id);
            var clan = clanMembership != null ? Map(gameData, gameData.GetClan(clanMembership.ClanId)) : null;
            var clanRole = clanMembership != null ? Map(gameData.GetClanRole(clanMembership.ClanRoleId), clanMembership) : null;
            return new Player
            {
                Id = character.Id,
                UserName = user.UserName,
                UserId = user.UserId,
                Name = character.Name,
                IsRejoin = rejoin,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = Map(gameData.GetSkills(character.SkillsId)),
                State = Map(gameData.GetState(character.StateId)),
                InventoryItems = invItems,
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                Clan = clan,
                ClanRole = clanRole,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault(),
                Identifier = character.Identifier,
                CharacterIndex = character.CharacterIndex,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsiteAdminPlayer MapForAdmin(this User user, IGameData gameData, Character character)
        {
            var sessionName = "";
            if (character.UserIdLock != null)
            {
                var sessionOwner = gameData.GetUser(character.UserIdLock.GetValueOrDefault());
                if (sessionOwner != null)
                {
                    sessionName = sessionOwner.UserName;
                }
            }

            var clanMembership = gameData.GetClanMembership(character.Id);
            var clan = clanMembership != null ? Map(gameData, gameData.GetClan(clanMembership.ClanId)) : null;
            var clanRole = clanMembership != null ? Map(gameData.GetClanRole(clanMembership.ClanRoleId), clanMembership) : null;

            return new WebsiteAdminPlayer
            {
                Created = user.Created,
                Id = character.Id,
                PasswordHash = user.PasswordHash,
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
                Clan = clan,
                ClanRole = clanRole,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault(),
                Identifier = character.Identifier,
                CharacterIndex = character.CharacterIndex,
                SessionName = sessionName,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsitePlayer MapForWebsite(this User user, IGameData gameData, Character character)
        {
            var items = gameData.GetAllPlayerItems(character.Id).OrderBy(x => gameData.GetItem(x.ItemId).Name).ToList();

            var clanMembership = gameData.GetClanMembership(character.Id);
            var clan = clanMembership != null ? Map(gameData, gameData.GetClan(clanMembership.ClanId)) : null;
            var clanRole = clanMembership != null ? Map(gameData.GetClanRole(clanMembership.ClanRoleId), clanMembership) : null;

            return new WebsitePlayer
            {
                Id = character.Id,
                UserName = user.UserName,
                UserId = user.UserId,
                Name = character.Name,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = MapForWebsite(gameData.GetSkills(character.SkillsId)),
                State = Map(gameData.GetState(character.StateId)),
                InventoryItems = Map(items),
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                Clan = clan,
                ClanRole = clanRole,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault()
            };
        }
    }
}

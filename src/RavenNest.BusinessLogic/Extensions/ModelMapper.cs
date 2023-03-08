using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Models;
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
        public static RavenNest.Models.Appearance Map(Appearance data)
        {
            return DataMapper.Map<RavenNest.Models.Appearance>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.GameSession Map(GameData gameData, DataModels.GameSession data)
        {
            var session = DataMapper.Map<RavenNest.Models.GameSession>(data);
            var user = gameData.GetUser(session.UserId);
            if (user == null)
                return null;


            var state = gameData.GetSessionState(session.Id);
            if (state != null)
            {
                session.ClientVersion = state.ClientVersion;
                session.SyncTime = state.SyncTime;
            }

            session.TwitchUserId = user.UserId;
            session.UserName = user.UserName;
            session.AdminPrivileges = user.IsAdmin.GetValueOrDefault();
            session.ModPrivileges = user.IsModerator.GetValueOrDefault();
            session.Players = gameData.GetActiveSessionCharacters(data)
                .Select(x => Map(gameData, x))
                .Where(x => x != null)
                .ToList();

            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.GameSessionPlayer Map(GameData gameData, DataModels.Character character)
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
        public static RavenNest.Models.CharacterState Map(DataModels.CharacterState data)
        {
            var state = DataMapper.Map<RavenNest.Models.CharacterState>(data);
            if (state == null)
            {
                state = new RavenNest.Models.CharacterState();
                if (data != null)
                {
                    state.Id = data.Id;
                    state.Task = data.Task;
                    state.TaskArgument = data.TaskArgument;
                    state.InRaid = data.InRaid;
                    state.Island = data.Island;
                    state.DuelOpponent = data.DuelOpponent;
                    state.RestedTime = data.RestedTime ?? 0d;
                    state.X = data.X;
                    state.Y = data.Y;
                    state.Z = data.Z;
                }
            }

            if (data != null)
            {
                state.InDungeon = data.InDungeon.GetValueOrDefault();
                state.InOnsen = data.InOnsen.GetValueOrDefault();
                state.RestedTime = data.RestedTime.GetValueOrDefault();
            }

            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.Statistics Map(DataModels.Statistics data)
        {
            return DataMapper.Map<RavenNest.Models.Statistics>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.Clan Map(GameData gameData, DataModels.Clan data)
        {
            if (data == null) return null;
            var user = gameData.GetUser(data.UserId);
            if (user == null) return null;

            var s = gameData.GetClanSkills(data.Id);
            var skills = new RavenNest.Models.ClanSkill[s.Count];
            for (var i = 0; i < s.Count; ++i)
            {
                var s0 = s[i];
                var s1 = gameData.GetSkill(s0.SkillId);
                if (s1 == null)
                {
                    s1 = new Skill
                    {
                        Name = "Err",
                        MaxLevel = 999,
                    };
                    Console.Error.WriteLine("gameData.GetSkill(s0.SkillId) returns null");

                }
                skills[i] = new RavenNest.Models.ClanSkill
                {
                    Id = s0.Id,
                    Experience = s0.Experience,
                    Level = s0.Level,
                    MaxLevel = s1.MaxLevel,
                    Name = s1.Name
                };
            }

            return new RavenNest.Models.Clan()
            {
                Id = data.Id,
                Logo = data.Logo,
                Name = data.Name,
                Owner = user.UserId,
                Experience = data.Experience,
                Level = data.Level,
                ClanSkills = skills
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.ClanRole Map(DataModels.ClanRole data, DataModels.CharacterClanMembership membership = null)
        {
            return new RavenNest.Models.ClanRole
            {
                Id = data.Id,
                Level = data.Level,
                Name = data.Name,
                Joined = membership?.Joined
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.Skills Map(Skills data)
        {
            return DataMapper.Map<RavenNest.Models.Skills>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SkillsExtended MapForWebsite(Skills data)
        {
            return DataMapper.Map<SkillsExtended>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.GameEvent Map(DataModels.GameEvent data)
        {
            return DataMapper.Map<RavenNest.Models.GameEvent>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.Resources Map(Resources data)
        {
            return DataMapper.Map<RavenNest.Models.Resources>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.Item Map(GameData gameData, Item item)
        {
            if (item == null) return null;
            var mapped = DataMapper.Map<RavenNest.Models.Item>(item);
            mapped.CraftingRequirements = gameData.GetCraftingRequirements(item.Id)
                .Select(x => DataMapper.Map<RavenNest.Models.ItemCraftingRequirement>(x))
                .ToList();

            return mapped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Item Map(RavenNest.Models.Item itemsItem)
        {
            return DataMapper.Map<Item>(itemsItem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.SyntyAppearance Map(DataModels.SyntyAppearance appearance)
        {
            if (appearance == null) return new RavenNest.Models.SyntyAppearance();
            return DataMapper.Map<RavenNest.Models.SyntyAppearance>(appearance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RavenNest.Models.InventoryItem Map(InventoryItem items)
        {
            return new RavenNest.Models.InventoryItem
            {
                Id = items.Id,
                Amount = items.Amount.GetValueOrDefault(),
                Equipped = items.Equipped,
                ItemId = items.ItemId,
                Tag = items.Tag,
                Enchantment = items.Enchantment,
                Flags = items.Flags ?? 0,
                Name = items.Name,
                Soulbound = items.Soulbound,
                TransmogrificationId = items.TransmogrificationId,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<RavenNest.Models.InventoryItem> Map(IEnumerable<InventoryItem> items)
        {
            return items.Select(Map).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(this Character character, GameData gameData, User user, bool rejoin = false, bool isSessionPlayer = false)
        {
            return user.Map(gameData, character, rejoin, isSessionPlayer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsitePlayer MapForWebsite(this Character character, GameData gameData, User user)
        {
            return user.MapForWebsite(gameData, character);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HighscorePlayer MapForHighscore(this Character character, GameData gameData)
        {
            var user = gameData.GetUser(character.UserId);
            if (user == null
                || user.Status > 0
#if !DEBUG
                || user.IsAdmin.GetValueOrDefault()
                || user.IsModerator.GetValueOrDefault()
#endif
                || user.IsHiddenInHighscore.GetValueOrDefault())
            {
                return null;
            }

            return new HighscorePlayer
            {
                CharacterIndex = character.CharacterIndex,
                Id = character.Id,
                Name = character.Name,
                Skills = gameData.GetCharacterSkills(character.SkillsId)
                // either replace this with a that maps the skills one by one and not use reflection
                // or use DataModels.Skills instead.
                //Skills = Map(gameData.GetCharacterSkills(character.SkillsId))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Player Map(
            this User user,
            GameData gameData,
            Character character,
            bool rejoin = false,
            bool inGame = false)
        {
            var playItems = gameData.GetAllPlayerItems(character.Id);
            if (inGame && character.UserIdLock != null)
            {
                var targetStreamUser = gameData.GetUser(character.UserIdLock.Value);
                // if we got streamer tokens, only send the ones for the appropriate streamer
                playItems = playItems.AsList(x => x.Tag == null || x.Tag == targetStreamUser.UserId);
            }

            var invItems = Map(playItems);
            if (user == null)
                return null;

            var clanMembership = gameData.GetClanMembership(character.Id);
            var clan = clanMembership != null ? Map(gameData, gameData.GetClan(clanMembership.ClanId)) : null;
            var clanRole = clanMembership != null ? Map(gameData.GetClanRole(clanMembership.ClanRoleId), clanMembership) : null;

            var characterState = gameData.GetCharacterState(character.StateId);

            if (character.StateId == null || characterState == null)
            {
                characterState = new DataModels.CharacterState()
                {
                    Id = Guid.NewGuid()
                };
                character.StateId = characterState.Id;
                gameData.Add(characterState);
            }

            (var battlePets, var activeBattlePet) = character.MapBattlePets(gameData);
            var connections = new List<AuthServiceConnection>();

            var uac = gameData.GetUserAccess(user.Id);
            foreach (var access in uac)
            {
                connections.Add(new AuthServiceConnection
                {
                    Platform = access.Platform,
                    PlatformId = access.PlatformId,
                    PlatformUserName = access.PlatformUsername
                });
            }

            return new Player
            {
                Id = character.Id,
                UserName = user.UserName,
                UserId = user.Id,
                Connections = connections,
                Name = character.Name,
                IsRejoin = rejoin,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = Map(gameData.GetCharacterSkills(character.SkillsId)),
                State = Map(characterState),
                InventoryItems = invItems,
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                ActiveBattlePet = activeBattlePet,
                BattlePets = battlePets,
                Clan = clan,
                ClanRole = clanRole,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault(),
                Identifier = character.Identifier,
                CharacterIndex = character.CharacterIndex,
                PatreonTier = user.PatreonTier.GetValueOrDefault(),
                IsHiddenInHighscore = user.IsHiddenInHighscore.GetValueOrDefault()
            };
        }

        private static (IReadOnlyList<BattlePet>, Guid?) MapBattlePets(this Character character, GameData gameData)
        {
            Guid? activeBattlePet = null;
            var pets = gameData.GetPets(character.Id);
            var battlePets = new List<BattlePet>();
            foreach (var p in pets)
            {
                battlePets.Add(DataMapper.Map<BattlePet>(p));
                if (p.Active)
                {
                    activeBattlePet = p.Id;
                }
            }
            return (battlePets, activeBattlePet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsiteAdminPlayer MapForAdmin(this User user, GameData gameData, Character character)
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
            (var battlePets, var activeBattlePet) = character.MapBattlePets(gameData);

            var connections = new List<AuthServiceConnection>();
            var uac = gameData.GetUserAccess(user.Id);
            foreach (var access in uac)
            {
                connections.Add(new AuthServiceConnection
                {
                    Platform = access.Platform,
                    PlatformId = access.PlatformId,
                    PlatformUserName = access.PlatformUsername
                });
            }

            return new WebsiteAdminPlayer
            {
                Created = user.Created,
                Id = character.Id,
                PasswordHash = user.PasswordHash,
                UserName = user.UserName,
                UserId = user.Id,
                Connections = connections,
                Name = character.Name,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = MapForWebsite(gameData.GetCharacterSkills(character.SkillsId)),
                State = Map(gameData.GetCharacterState(character.StateId)),
                InventoryItems = Map(gameData.GetAllPlayerItems(character.Id)),
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                ActiveBattlePet = activeBattlePet,
                BattlePets = battlePets,
                Clan = clan,
                ClanRole = clanRole,
                OriginUserId = character.OriginUserId,
                Revision = character.Revision.GetValueOrDefault(),
                Identifier = character.Identifier,
                CharacterIndex = character.CharacterIndex,
                SessionName = sessionName,
                PatreonTier = user.PatreonTier.GetValueOrDefault(),
                IsHiddenInHighscore = user.IsHiddenInHighscore.GetValueOrDefault()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebsitePlayer MapForWebsite(this User user, GameData gameData, Character character)
        {
            var items = gameData.GetAllPlayerItems(character.Id)
                //.OrderByDescending(x => gameData.GetItem(x.ItemId)?.ShopSellPrice)
                .OrderBy(x => x.Name ?? gameData.GetItem(x.ItemId)?.Name)
                .ToList();

            var clanMembership = gameData.GetClanMembership(character.Id);
            var clan = clanMembership != null ? Map(gameData, gameData.GetClan(clanMembership.ClanId)) : null;
            var clanRole = clanMembership != null ? Map(gameData.GetClanRole(clanMembership.ClanRoleId), clanMembership) : null;

            var sessionInfo = GetCharacterSessionInfo(gameData, character);
            (var battlePets, var activeBattlePet) = character.MapBattlePets(gameData);

            var connections = new List<AuthServiceConnection>();
            var uac = gameData.GetUserAccess(user.Id);
            foreach (var access in uac)
            {
                connections.Add(new AuthServiceConnection
                {
                    Platform = access.Platform,
                    PlatformId = access.PlatformId,
                    PlatformUserName = access.PlatformUsername
                });
            }

            return new WebsitePlayer
            {
                Id = character.Id,
                UserName = user.UserName,
                CharacterIndex = character.CharacterIndex,
                UserId = user.Id,
                Connections = connections,
                Name = character.Name,
                IsAdmin = user.IsAdmin.GetValueOrDefault(),
                IsModerator = user.IsModerator.GetValueOrDefault(),
                Appearance = Map(gameData.GetAppearance(character.SyntyAppearanceId)),
                Resources = Map(gameData.GetResources(character.ResourcesId)),
                Skills = MapForWebsite(gameData.GetCharacterSkills(character.SkillsId)),
                State = Map(gameData.GetCharacterState(character.StateId)),
                InventoryItems = Map(items),
                Statistics = Map(gameData.GetStatistics(character.StatisticsId)),
                ActiveBattlePet = activeBattlePet,
                BattlePets = battlePets,
                Clan = clan,
                ClanRole = clanRole,
                Identifier = character.Identifier,
                OriginUserId = character.OriginUserId,
                SessionInfo = sessionInfo,
                PatreonTier = user.PatreonTier.GetValueOrDefault(),
                Revision = character.Revision.GetValueOrDefault(),
                IsHiddenInHighscore = user.IsHiddenInHighscore.GetValueOrDefault()
            };
        }

        private static CharacterSessionInfo GetCharacterSessionInfo(GameData gameData, Character character)
        {
            var sessionInfo = new CharacterSessionInfo();
            var session = gameData.GetSessionByCharacterId(character.Id);
            if (session != null)
            {
                sessionInfo.Started = session.Started;
                var sessionOwner = gameData.GetUser(session.UserId);
                if (sessionOwner != null)
                {
                    sessionInfo.OwnerDisplayName = sessionOwner.DisplayName;
                    sessionInfo.OwnerUserName = sessionOwner.UserName;
                }
                var css = gameData.GetCharacterSessionState(session.Id, character.Id);
                if (css != null)
                {
                    sessionInfo.SkillsUpdated = css.LastSkillUpdate;
                }
            }

            return sessionInfo;
        }
    }
}

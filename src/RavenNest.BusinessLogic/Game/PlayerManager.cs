using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

using Appearance = RavenNest.DataModels.Appearance;
using Gender = RavenNest.DataModels.Gender;
using InventoryItem = RavenNest.DataModels.InventoryItem;
using Item = RavenNest.DataModels.Item;
using ItemCategory = RavenNest.DataModels.ItemCategory;
using ItemType = RavenNest.DataModels.ItemType;
using Resources = RavenNest.DataModels.Resources;
using Skills = RavenNest.DataModels.Skills;
using Statistics = RavenNest.DataModels.Statistics;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerManager : IPlayerManager
    {
        private readonly IRavenfallDbContextProvider dbProvider;

        public PlayerManager(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }

        public async Task<Player> CreatePlayerIfNotExistsAsync(string userId, string userName)
        {
            using (var db = dbProvider.Get())
            {
                var player = await GetGlobalPlayerAsync(userId);
                if (player != null) return player;
                return await CreatePlayerAsync(null, userId, userName, db);
            }
        }

        public async Task<Player> CreatePlayerAsync(string userId, string userName)
        {
            using (var db = dbProvider.Get())
            {
                return await CreatePlayerAsync(null, userId, userName, db);
            }
        }

        public async Task<Player> AddPlayerAsync(
            SessionToken token,
            string userId,
            string userName)
        {
            using (var db = dbProvider.Get())
            {
                var session = await db.GameSession.FirstOrDefaultAsync(
                    x => x.Id == token.SessionId &&
                         x.Status == (int)SessionStatus.Active);

                if (session == null)
                {
                    return null;
                }

                var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
                if (user == null)
                {
                    return await CreatePlayerAsync(session, userId, userName, db);
                }

                var character = await db.Character
                    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                    .Include(x => x.Appearance)
                    .Include(x => x.SyntyAppearance)
                    .Include(x => x.Resources)
                    .Include(x => x.Skills)
                    .Include(x => x.Statistics)
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        (session.Local && x.Local && x.OriginUserId == session.UserId ||
                         !session.Local && !x.Local));

                if (character == null)
                {
                    return await CreatePlayerAsync(session, user, db);
                }

                if (character.SyntyAppearance == null && character.Appearance != null)
                {
                    var syntyApp = GenerateSyntyAppearance(character.Appearance);
                    await db.SyntyAppearance.AddAsync(syntyApp);

                    character.SyntyAppearanceId = syntyApp.Id;
                    character.SyntyAppearance = syntyApp;
                }

                character.UserIdLock = session.UserId;
                character.LastUsed = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return character.Map(user);
            }
        }

        public async Task<Player> GetGlobalPlayerAsync(Guid userId)
        {
            using (var db = dbProvider.Get())
            {
                var user = await db.User.FirstOrDefaultAsync(x => x.Id == userId);
                if (user == null)
                {
                    return null;
                }

                return await GetGlobalPlayerAsync(db, user);
            }
        }

        public async Task<Player> GetGlobalPlayerAsync(string userId)
        {
            using (var db = dbProvider.Get())
            {
                var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
                if (user == null)
                {
                    return null;
                }

                return await GetGlobalPlayerAsync(db, user);
            }
        }

        public async Task<Player> GetPlayerAsync(SessionToken sessionToken)
        {
            using (var db = dbProvider.Get())
            {

                var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == sessionToken.SessionId);
                if (session == null)
                {
                    return null;
                }

                var user = await db.User.FirstOrDefaultAsync(x => x.Id == session.UserId);
                if (user == null)
                {
                    return null;
                }

                var character = await db.Character
                    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                    .Include(x => x.State)
                    .Include(x => x.Appearance)
                    .Include(x => x.SyntyAppearance)
                    .Include(x => x.Resources)
                    .Include(x => x.Skills)
                    .Include(x => x.Statistics)
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        (session.Local && x.Local && x.OriginUserId == session.UserId ||
                         !session.Local && !x.Local));

                return character.Map(user);
            }
        }

        public async Task<bool> UpdatePlayerStateAsync(
            SessionToken sessionToken,
            CharacterStateUpdate update)
        {
            try
            {
                using (var db = dbProvider.Get())
                {
                    var player = await GetCharacterAsync(db, sessionToken, update.UserId);
                    if (player == null)
                    {
                        return false;
                    }
                    if (player.StateId == null)
                    {
                        var state = CreateCharacterState(update);
                        await db.CharacterState.AddAsync(state);
                        player.State = state;
                        player.StateId = state.Id;
                    }
                    else
                    {
                        player.State.DuelOpponent = update.DuelOpponent;
                        player.State.Health = update.Health;
                        player.State.InArena = update.InArena;
                        player.State.InRaid = update.InRaid;
                        player.State.Island = update.Island;
                        player.State.Task = update.Task;
                        player.State.TaskArgument = update.TaskArgument;
                        player.State.X = (decimal)update.Position.X;
                        player.State.Y = (decimal)update.Position.Y;
                        player.State.Z = (decimal)update.Position.Z;
                        db.Update(player.State);
                    }

                    await db.SaveChangesAsync();
                    return true;
                }
            }
            catch { return false; }
        }

        private DataModels.CharacterState CreateCharacterState(CharacterStateUpdate update)
        {
            var state = new DataModels.CharacterState();
            state.Id = Guid.NewGuid();
            state.DuelOpponent = update.DuelOpponent;
            state.Health = update.Health;
            state.InArena = update.InArena;
            state.InRaid = update.InRaid;
            state.Island = update.Island;
            state.Task = update.Task;
            state.TaskArgument = update.TaskArgument;
            state.X = (decimal)update.Position.X;
            state.Y = (decimal)update.Position.Y;
            state.Z = (decimal)update.Position.Z;
            return state;
        }

        public async Task<Player> GetPlayerAsync(SessionToken sessionToken, string userId)
        {
            using (var db = dbProvider.Get())
            {
                var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
                if (user == null)
                {
                    return null;
                }

                var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == sessionToken.SessionId);
                if (session == null)
                {
                    return null;
                }

                var character = await db.Character
                    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                    .Include(x => x.Appearance)
                    .Include(x => x.SyntyAppearance)
                    .Include(x => x.Resources)
                    .Include(x => x.Skills)
                    .Include(x => x.Statistics)
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        (session.Local && x.Local && x.OriginUserId == session.UserId ||
                        !session.Local && !x.Local));

                return character.Map(user);
            }
        }

        public async Task<bool> KickPlayerAsync(SessionToken token, string userId)
        {
            using (var db = dbProvider.Get())
            {
                //var characterSession = await GetActiveCharacterSessionAsync(token, userId);
                //if (characterSession == null) return false;

                //characterSession.Ended = DateTime.UtcNow;
                //characterSession.Status = (int)SessionStatus.Inactive;

                //db.Update(characterSession);
                //await db.SaveChangesAsync();
                return true;
            }
        }

        public Task<bool> UpdateStatisticsAsync(
            SessionToken token,
            string userId,
            decimal[] statistics)
        {
            return UpdateStatisticsAsync(token, userId, statistics, dbProvider.Get(), true);
        }

        public Task<bool> UpdateResourcesAsync(
            SessionToken token, string userId, decimal[] resources)
        {
            return UpdateResourcesAsync(token, userId, resources, dbProvider.Get(), true);
        }

        public Task<bool> UpdateExperienceAsync(
            SessionToken token, string userId, decimal[] experience)
        {
            return UpdateExperienceAsync(token, userId, experience, dbProvider.Get(), true);
        }

        //public Task<bool> UpdateAppearanceAsync(
        //    SessionToken token, string userId, int[] appearance)
        //{
        //    return UpdateAppearanceAsync(token, userId, appearance, dbProvider.Get(), true);
        //}

        public async Task<bool> UpdateSyntyAppearanceAsync(
            SessionToken token, string userId, Models.SyntyAppearance appearance)
        {
            using (var db = dbProvider.Get())
            {
                var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null || character.UserIdLock != session.UserId)
                {
                    return false;
                }
            }

            return await UpdateSyntyAppearanceAsync(userId, appearance);
        }


        public async Task<bool[]> UpdateManyAsync(
            SessionToken token, PlayerState[] states)
        {
            var results = new List<bool>();
            using (var db = dbProvider.Get())
            {
                var gameSession = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
                if (gameSession == null)
                {
                    return Enumerable.Range(0, states.Length).Select(x => false).ToArray();
                }

                foreach (var state in states)
                {
                    var character = gameSession.Local
                        ? await db.Character.Include(x => x.User).FirstOrDefaultAsync(x => x.User.UserId == state.UserId && x.OriginUserId == gameSession.UserId && x.Local)
                        : await db.Character.Include(x => x.User).FirstOrDefaultAsync(x => x.User.UserId == state.UserId && !x.Local);

                    if (character == null)
                    {
                        results.Add(false);
                        continue;
                    }

                    if (!await AcquiredUserLockAsync(token, db, character))
                    {
                        results.Add(false);
                        continue;
                    }

                    try
                    {
                        if (state.Experience != null && state.Experience.Length > 0)
                        {
                            await UpdateExperienceAsync(token, state.UserId, state.Experience, db, false);
                        }

                        if (state.Statistics != null && state.Statistics.Length > 0)
                        {
                            await UpdateStatisticsAsync(token, state.UserId, state.Statistics, db, false);
                        }

                        if (state.Resources != null && state.Resources.Length > 0)
                        {
                            await UpdateResourcesAsync(token, state.UserId, state.Resources, db, false);
                        }

                        EquipBestItems(db, character);

                        character.Revision = character.Revision.GetValueOrDefault() + 1;
                        db.Update(character);

                        results.Add(true);
                    }
                    catch
                    {
                        results.Add(false);
                    }
                }

                if (results.Any(x => x))
                {
                    await db.SaveChangesAsync();
                }

                return results.ToArray();
            }
        }

        public async Task<AddItemResult> AddItemAsync(
            SessionToken token, string userId, Guid itemId)
        {
            using (var db = dbProvider.Get())
            {
                var item = await db.Item.FirstOrDefaultAsync(x => x.Id == itemId);
                if (item == null) return AddItemResult.Failed;

                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return AddItemResult.Failed;

                //var characterSession = await GetActiveCharacterSessionAsync(token, userId);
                //if (characterSession == null) return AddItemResult.Failed;
                //var character = characterSession.Character;
                var skills = character.Skills;
                var equippedItems = await db.InventoryItem
                        .Include(x => x.Item)
                        .Where(x =>
                            x.CharacterId == character.Id &&
                            x.Equipped &&
                            x.Item.Type == item.Type &&
                            x.Item.Category == item.Category)
                        .ToListAsync();

                var equipped = false;
                if (equippedItems.Count > 0)
                {
                    var bestFirst = equippedItems.OrderByDescending(x => GetItemValue(x.Item));
                    var bestEquipped = bestFirst.First();
                    if (CanEquipItem(item, skills) && IsItemBetter(item, bestEquipped.Item))
                    {
                        equipped = true;
                    }

                    if (!equipped)
                    {
                        bestEquipped.Equipped = true;
                    }

                    for (var i = 1; i < equippedItems.Count; ++i)
                    {
                        equippedItems[i].Equipped = false;
                    }

                    db.UpdateRange(equippedItems);
                }

                var addNew = true;
                if (!equipped)
                {
                    var inv = await db.InventoryItem.FirstOrDefaultAsync(x => x.ItemId == itemId && !x.Equipped && x.CharacterId == character.Id);
                    if (inv != null)
                    {
                        inv.Amount += 1;
                        db.Update(inv);
                        addNew = false;
                    }
                }

                if (addNew)
                {
                    await db.InventoryItem.AddAsync(new InventoryItem
                    {
                        Id = Guid.NewGuid(),
                        ItemId = item.Id,
                        Amount = 1,
                        CharacterId = character.Id,
                        Equipped = equipped
                    });
                }

                await db.SaveChangesAsync();

                return equipped
                    ? AddItemResult.AddedAndEquipped
                    : AddItemResult.Added;
            }
        }

        public async Task<bool> GiftItemAsync(
            SessionToken token,
            string gifterUserId,
            string receiverUserId,
            Guid itemId)
        {
            return false;
        }

        public async Task<bool> GiftResourcesAsync(
            SessionToken token,
            string giftUserId,
            string receiverUserId,
            string resource,
            long amount)
        {
            return false;
        }

        public async Task<bool> EquipItemAsync(SessionToken token, string userId, Guid itemId)
        {
            using (var db = dbProvider.Get())
            {
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return false;

                var item = await db.Item.FirstOrDefaultAsync(x => x.Id == itemId);
                if (item == null) return false;

                var invItem = await db.InventoryItem
                    .Include(x => x.Item)
                    .FirstOrDefaultAsync(x =>
                        x.CharacterId == character.Id &&
                        x.ItemId == itemId &&
                        !x.Equipped);

                var skills = character.Skills;
                if (invItem == null || !CanEquipItem(invItem.Item, skills))
                    return false;

                if (invItem.Amount > 1)
                {
                    invItem.Amount = invItem.Amount - 1;
                    db.Update(invItem);

                    invItem = new InventoryItem
                    {
                        Id = Guid.NewGuid(),
                        CharacterId = character.Id,
                        Character = character,
                        Amount = 1,
                        Equipped = true,
                        ItemId = invItem.ItemId,
                        Item = item
                    };
                    await db.InventoryItem.AddAsync(invItem);
                }
                else
                {
                    invItem.Equipped = true;
                    db.Update(invItem);
                }

                var invItemEq = await db.InventoryItem
                    .Include(x => x.Item)
                    .FirstOrDefaultAsync(x =>
                        x.CharacterId == character.Id &&
                        x.Item.Type == item.Type &&
                        x.Item.Category == item.Category && x.Equipped);

                if (invItemEq != null)
                {
                    var stack = await db.InventoryItem
                    .Include(x => x.Item)
                    .FirstOrDefaultAsync(x =>
                        x.CharacterId == character.Id &&
                        x.ItemId == invItemEq.ItemId &&
                        !x.Equipped);

                    if (stack != null)
                    {
                        ++stack.Amount;
                        db.Remove(invItemEq);
                        db.Update(stack);
                    }
                    else
                    {
                        invItemEq.Equipped = false;
                        db.Update(invItemEq);
                    }
                }

                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task<bool> UnEquipItemAsync(SessionToken token, string userId, Guid itemId)
        {
            using (var db = dbProvider.Get())
            {
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return false;

                var invItem = await db.InventoryItem
                    .Include(x => x.Item)
                    .FirstOrDefaultAsync(x =>
                        x.CharacterId == character.Id &&
                        x.ItemId == itemId &&
                        x.Equipped);

                if (invItem == null) return false;

                var stack = await db.InventoryItem
                    .Include(x => x.Item)
                    .FirstOrDefaultAsync(x =>
                        x.CharacterId == character.Id &&
                        x.ItemId == itemId &&
                        !x.Equipped);

                if (stack != null)
                {
                    ++stack.Amount;
                    db.InventoryItem.Remove(invItem);
                    db.Update(stack);
                }
                else
                {
                    invItem.Equipped = false;
                    db.Update(invItem);
                }
                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task<ItemCollection> GetEquippedItemsAsync(
            SessionToken token, string userId)
        {
            using (var db = dbProvider.Get())
            {
                var itemCollection = new ItemCollection();
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return itemCollection;

                foreach (var inv in db.InventoryItem
                    .Include(x => x.Item)
                    .Where(x => x.CharacterId == character.Id && x.Equipped))
                {
                    itemCollection.Add(ModelMapper.Map(inv.Item));
                }

                return itemCollection;
            }
        }

        public async Task<ItemCollection> GetAllItemsAsync(
            SessionToken token, string userId)
        {
            using (var db = dbProvider.Get())
            {
                var itemCollection = new ItemCollection();
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return itemCollection;

                foreach (var inv in db.InventoryItem
                    .Include(x => x.Item)
                    .Where(x => x.CharacterId == character.Id))
                {
                    itemCollection.Add(ModelMapper.Map(inv.Item));
                }

                return null;
            }
        }
        public async Task<IReadOnlyList<Player>> GetPlayersAsync()
        {
            using (var db = dbProvider.Get())
            {
                var characters = await db.Character
                    .Include(x => x.User)
                    .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                    .Include(x => x.Appearance)
                    .Include(x => x.SyntyAppearance)
                    .Include(x => x.Resources)
                    .Include(x => x.Skills)
                    .Include(x => x.Statistics)
                    .Where(x => !x.Local)
                    .ToListAsync();

                return characters.Select(x => x.Map(x.User)).ToList();
            }
        }
        private async Task<bool> UpdateStatisticsAsync(
            SessionToken token,
            string userId,
            decimal[] statistics, RavenfallDbContext db, bool save)
        {
            var character = await GetCharacterAsync(db, token, userId);
            if (character == null) return false;
            if (!await AcquiredUserLockAsync(token, db, character)) return false;

            var index = 0;

            character.Statistics.RaidsWon += (int)statistics[index++];
            character.Statistics.RaidsLost += (int)statistics[index++];
            character.Statistics.RaidsJoined += (int)statistics[index++];

            character.Statistics.DuelsWon += (int)statistics[index++];
            character.Statistics.DuelsLost += (int)statistics[index++];

            character.Statistics.PlayersKilled += (int)statistics[index++];
            character.Statistics.EnemiesKilled += (int)statistics[index++];

            character.Statistics.ArenaFightsJoined += (int)statistics[index++];
            character.Statistics.ArenaFightsWon += (int)statistics[index++];

            character.Statistics.TotalDamageDone += (long)statistics[index++];
            character.Statistics.TotalDamageTaken += (long)statistics[index++];
            character.Statistics.DeathCount += (int)statistics[index++];

            character.Statistics.TotalWoodCollected += statistics[index++];
            character.Statistics.TotalOreCollected += statistics[index++];
            character.Statistics.TotalFishCollected += statistics[index++];
            character.Statistics.TotalWheatCollected += statistics[index++];

            character.Statistics.CraftedWeapons += (int)statistics[index++];
            character.Statistics.CraftedArmors += (int)statistics[index++];
            character.Statistics.CraftedPotions += (int)statistics[index++];
            character.Statistics.CraftedRings += (int)statistics[index++];
            character.Statistics.CraftedAmulets += (int)statistics[index++];

            character.Statistics.CookedFood += (int)statistics[index++];

            character.Statistics.ConsumedPotions += (int)statistics[index++];
            character.Statistics.ConsumedFood += (int)statistics[index++];

            character.Statistics.TotalTreesCutDown += (long)statistics[index];

            if (save) db.Update(character.Appearance);
            await db.SaveChangesAsync();

            if (save) await db.SaveChangesAsync();

            return false;
        }

        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, Models.SyntyAppearance appearance)
        {
            try
            {
                using (var db = dbProvider.Get())
                {
                    var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
                    if (user == null) return false;

                    var character = await db.Character
                        .Include(x => x.SyntyAppearance)
                        .FirstOrDefaultAsync(x => !x.Local && x.UserId == user.Id);

                    if (character == null) return false;

                    UpdateCharacterAppearance(appearance, db, character);

                    var sessionOwnerUserId = character.UserIdLock;
                    var gameSession = await db.GameSession
                        .Where(x => x.UserId == sessionOwnerUserId && x.Status == (int)SessionStatus.Active)
                        .OrderByDescending(x => x.Started)
                        .FirstOrDefaultAsync();

                    if (gameSession != null)
                    {
                        var lastEvent = await db.GameEvent
                            .Where(x => x.GameSessionId == gameSession.Id)
                            .OrderByDescending(x => x.Revision)
                            .FirstOrDefaultAsync();

                        var gameEvent = new DataModels.GameEvent
                        {
                            Id = Guid.NewGuid(),
                            GameSessionId = gameSession.Id,
                            Revision = (lastEvent?.Revision).GetValueOrDefault() + 1,
                            Type = (int)GameEventType.PlayerAppearance,
                            Data = JSON.Stringify(new SyntyAppearanceUpdate
                            {
                                UserId = userId,
                                Value = appearance
                            })
                        };

                        await db.GameEvent.AddAsync(gameEvent);
                    }

                    await db.SaveChangesAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        //public async Task<bool> UpdateAppearanceAsync(string userId, int[] appearance)
        //{
        //    try
        //    {
        //        using (var db = dbProvider.Get())
        //        {
        //            var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
        //            if (user == null) return false;

        //            var character = await db.Character
        //                .Include(x => x.Appearance)
        //                .Include(x => x.SyntyAppearance)
        //                .FirstOrDefaultAsync(x => !x.Local && x.UserId == user.Id);

        //            if (character == null) return false;

        //            UpdateCharacterAppearance(appearance, db, character);

        //            var sessionOwnerUserId = character.UserIdLock;
        //            var gameSession = await db.GameSession
        //                .Where(x => x.UserId == sessionOwnerUserId && x.Status == (int)SessionStatus.Active)
        //                .OrderByDescending(x => x.Started)
        //                .FirstOrDefaultAsync();

        //            if (gameSession != null)
        //            {
        //                var lastEvent = await db.GameEvent
        //                    .Where(x => x.GameSessionId == gameSession.Id)
        //                    .OrderByDescending(x => x.Revision)
        //                    .FirstOrDefaultAsync();

        //                var gameEvent = new DataModels.GameEvent
        //                {
        //                    Id = Guid.NewGuid(),
        //                    GameSessionId = gameSession.Id,
        //                    Revision = (lastEvent?.Revision).GetValueOrDefault() + 1,
        //                    Type = (int)GameEventType.PlayerAppearance,
        //                    Data = JSON.Stringify(new AppearanceUpdate
        //                    {
        //                        UserId = userId,
        //                        Values = ToAppearanceData(character.Appearance)
        //                    })
        //                };

        //                await db.GameEvent.AddAsync(gameEvent);
        //            }

        //            await db.SaveChangesAsync();
        //            return true;
        //        }
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        public int[] ToAppearanceData(Appearance appearance)
        {
            return new int[]
            {
                (int)appearance.Gender,
                appearance.Gender == Gender.Female ? appearance.FemaleHairModelNumber : appearance.MaleHairModelNumber,
                (int)appearance.HairColor,
                appearance.EyesModelNumber,
                (int)appearance.SkinColor,
                appearance.BeardModelNumber,
                (int)appearance.BeardColor,
                appearance.BrowsModelNumber,
                (int)appearance.BrowColor,
                appearance.MouthModelNumber
            };
        }

        private static async Task<Player> GetGlobalPlayerAsync(RavenfallDbContext db, User user)
        {
            var character = await db.Character
                .Include(x => x.InventoryItem) //.ThenInclude(x => x.Item)
                .Include(x => x.Appearance)
                .Include(x => x.SyntyAppearance)
                .Include(x => x.Resources)
                .Include(x => x.Skills)
                .Include(x => x.Statistics)
                .FirstOrDefaultAsync(x => x.UserId == user.Id && !x.Local);

            return character.Map(user);
        }

        //private async Task<bool> UpdateAppearanceAsync(
        //    SessionToken token, string userId, int[] appearance, RavenfallDbContext db, bool save)
        //{
        //    try
        //    {
        //        var character = await GetCharacterAsync(db, token, userId);
        //        if (character == null) return false;

        //        UpdateCharacterAppearance(appearance, db, character);
        //        if (save) await db.SaveChangesAsync();
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        private static void UpdateCharacterAppearance(
            Models.SyntyAppearance appearance, RavenfallDbContext db, Character character)
        {
            DataMapper.RefMap(appearance, character.SyntyAppearance, nameof(appearance.Id));
            db.Update(character.SyntyAppearance);
        }

        //private static void UpdateCharacterAppearance(
        //    int[] appearance, RavenfallDbContext db, Character character)
        //{
        //    var appearanceIndex = 0;
        //    int Next(int max) => Math.Max(0, Math.Min(max, appearance[appearanceIndex++]));
        //    int TryNext(int max, int def = 0) => appearanceIndex + 1 < appearance.Length ? Math.Max(0, Math.Min(max, appearance[appearanceIndex++])) : def;

        //    var hairColors = Enum.GetValues(typeof(HairColor)).Length - 1;

        //    character.Appearance.Gender = (Gender)Next(1);
        //    if (character.Appearance.Gender == Gender.Male)
        //        character.Appearance.MaleHairModelNumber = Next(11);
        //    else
        //        character.Appearance.FemaleHairModelNumber = Next(20);

        //    character.Appearance.HairColor = (DataModels.HairColor)Next(hairColors);
        //    character.Appearance.EyesModelNumber = Next(7);
        //    character.Appearance.SkinColor = (DataModels.SkinColor)Next(2);
        //    character.Appearance.BeardModelNumber = Next(10);
        //    character.Appearance.BeardColor = (DataModels.HairColor)Next(hairColors);
        //    character.Appearance.BrowsModelNumber = Next(15);
        //    character.Appearance.BrowColor = (DataModels.HairColor)Next(hairColors);
        //    character.Appearance.MouthModelNumber = Next(11);
        //    character.Appearance.HelmetVisible = TryNext(1, 1) == 1;

        //    db.Update(character.Appearance);
        //}

        private async Task<bool> UpdateExperienceAsync(
            SessionToken token,
            string userId,
            decimal[] experience,
            RavenfallDbContext db,
            bool save)
        {
            try
            {
                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return false;
                if (!await AcquiredUserLockAsync(token, db, character)) return false;

                var skillIndex = 0;
                var attackExperience = Math.Max(character.Skills.Attack, experience[skillIndex++]);
                var defenseExperience = Math.Max(character.Skills.Defense, experience[skillIndex++]);

                character.Skills.Attack = attackExperience;
                character.Skills.Defense = defenseExperience;
                character.Skills.Strength = Math.Max(character.Skills.Strength, experience[skillIndex++]);
                character.Skills.Health = Math.Max(character.Skills.Health, experience[skillIndex++]);
                character.Skills.Woodcutting = Math.Max(character.Skills.Woodcutting, experience[skillIndex++]);
                character.Skills.Fishing = Math.Max(character.Skills.Fishing, experience[skillIndex++]);
                character.Skills.Mining = Math.Max(character.Skills.Mining, experience[skillIndex++]);
                character.Skills.Crafting = Math.Max(character.Skills.Crafting, experience[skillIndex++]);
                character.Skills.Cooking = Math.Max(character.Skills.Cooking, experience[skillIndex++]);
                character.Skills.Farming = Math.Max(character.Skills.Farming, experience[skillIndex++]);

                if (experience.Length > 10) character.Skills.Slayer = Math.Max(character.Skills.Slayer, experience[skillIndex++]);
                if (experience.Length > 11) character.Skills.Magic = Math.Max(character.Skills.Magic, experience[skillIndex++]);
                if (experience.Length > 12) character.Skills.Ranged = Math.Max(character.Skills.Ranged, experience[skillIndex++]);
                if (experience.Length > 13) character.Skills.Sailing = Math.Max(character.Skills.Sailing, experience[skillIndex++]);

                db.Update(character.Skills);
                if (save) await db.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static void EquipBestItems(RavenfallDbContext db, Character character)
        {
            var weapons = character.InventoryItem
                .Where(x => x.Item.Category == (int)ItemCategory.Weapon)
                .OrderByDescending(x => GetItemValue(x.Item))
                .ToList();

            if (weapons.Count > 0)
            {
                var weaponToEquip = weapons.FirstOrDefault(x => CanEquipItem(x.Item, character.Skills));
                if (weaponToEquip != null)
                {
                    weaponToEquip.Equipped = true;
                }
                foreach (var weapon in weapons.Where(x => x != weaponToEquip))
                {
                    weapon.Equipped = false;
                }

                db.UpdateRange(weapons);
            }

            foreach (var itemGroup in character.InventoryItem
                .Where(x => x.Item.Category != (int)ItemCategory.Weapon)
                .GroupBy(x => x.Item.Type))
            {
                var itemToEquip = itemGroup
                    .OrderByDescending(x => GetItemValue(x.Item))
                    .FirstOrDefault(x => CanEquipItem(x.Item, character.Skills));

                // ensure we don't change equipped pet
                if (itemGroup.Key == (int)ItemType.Pet)
                {
                    var alreadyEquipped = itemGroup.FirstOrDefault(x => x.Equipped);
                    if (alreadyEquipped != null)
                    {
                        itemToEquip = alreadyEquipped;
                    }
                }

                if (itemToEquip != null)
                {
                    itemToEquip.Equipped = true;

                    if (itemToEquip.Amount > 1)
                    {
                        var diff = itemToEquip.Amount - 1;
                        var inventoryItem = character.InventoryItem
                            .FirstOrDefault(x => x.ItemId == itemToEquip.ItemId && !x.Equipped);

                        if (inventoryItem == null)
                        {
                            db.InventoryItem.Add(new InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                ItemId = itemToEquip.ItemId,
                                CharacterId = itemToEquip.CharacterId,
                                Equipped = false,
                                Amount = diff
                            });
                        }
                        else
                        {
                            inventoryItem.Amount += diff;
                            db.Update(inventoryItem);
                        }

                        itemToEquip.Amount = 1;
                    }

                    db.Update(itemToEquip);
                }

                foreach (var item in itemGroup.Where(x => x != itemToEquip))
                {
                    item.Equipped = false;
                    db.Update(item);
                }

                if (itemToEquip != null)
                    db.Update(itemToEquip);
            }
        }

        private async Task<bool> UpdateResourcesAsync(
            SessionToken token,
            string userId,
            decimal[] resources,
            RavenfallDbContext db,
            bool save)
        {
            try
            {
                var index = 0;

                var character = await GetCharacterAsync(db, token, userId);
                if (character == null) return false;
                if (!await AcquiredUserLockAsync(token, db, character)) return false;

                character.Resources.Wood += resources[index++];
                character.Resources.Fish += resources[index++];
                character.Resources.Ore += resources[index++];
                character.Resources.Wheat += resources[index++];
                character.Resources.Coins += resources[index++];
                character.Resources.Magic += resources[index++];
                character.Resources.Arrows += resources[index];

                db.Update(character.Resources);
                if (save) await db.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<Player> CreatePlayerAsync(
            GameSession session, string userId, string userName, RavenfallDbContext db)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                Created = DateTime.UtcNow
            };

            await db.User.AddAsync(user);
            return await CreatePlayerAsync(session, user, db);
        }

        private async Task<Player> CreatePlayerAsync(GameSession session, User user, RavenfallDbContext db)
        {
            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = user.UserName,
                UserId = user.Id,
                OriginUserId = session?.UserId ?? Guid.Empty,
                Local = session?.Local ?? false,
                Created = DateTime.UtcNow
            };

            var appearance = GenerateRandomAppearance();
            var syntyAppearance = GenerateRandomSyntyAppearance();

            var skills = GenerateSkills();
            var resources = GenerateResources();
            var statistics = GenerateStatistics();

            await db.SyntyAppearance.AddAsync(syntyAppearance);
            await db.Statistics.AddAsync(statistics);
            await db.Skills.AddAsync(skills);
            await db.Appearance.AddAsync(appearance);
            await db.Resources.AddAsync(resources);

            character.SyntyAppearanceId = syntyAppearance.Id;
            character.SyntyAppearance = syntyAppearance;
            character.Resources = resources;
            character.ResourcesId = resources.Id;
            character.Appearance = appearance;
            character.AppearanceId = appearance.Id;
            character.Statistics = statistics;
            character.StatisticsId = statistics.Id;
            character.Skills = skills;
            character.SkillsId = skills.Id;
            character.UserIdLock = session?.UserId;
            character.LastUsed = DateTime.UtcNow;
            await db.Character.AddAsync(character);
            await db.SaveChangesAsync();
            return character.Map(user);
        }

        private Statistics GenerateStatistics()
        {
            return new Statistics
            {
                Id = Guid.NewGuid()
            };
        }

        private Resources GenerateResources()
        {
            return new Resources
            {
                Id = Guid.NewGuid()
            };
        }

        private Skills GenerateSkills()
        {
            return new Skills
            {
                Id = Guid.NewGuid(),
                Health = 1154
            };
        }

        private async Task<Character> GetCharacterAsync(RavenfallDbContext db, SessionToken token, string userId)
        {
            var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
            if (session == null) return null;

            var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null) return null;

            return await db.Character
                .Include(x => x.InventoryItem).ThenInclude(x => x.Item)
                .Include(x => x.Appearance)
                .Include(x => x.SyntyAppearance)
                .Include(x => x.State)
                .Include(x => x.Resources)
                .Include(x => x.Skills)
                .Include(x => x.Statistics)
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.Id &&
                    (session.Local && x.Local && x.OriginUserId == session.UserId ||
                     !session.Local && !x.Local));
        }

        private static DataModels.SyntyAppearance GenerateRandomSyntyAppearance()
        {
            var gender = Utility.Random<Gender>();
            return new DataModels.SyntyAppearance
            {
                Id = Guid.NewGuid(),
                Gender = gender,
                SkinColor = GetHexColor(Utility.Random<DataModels.SkinColor>()),
                HairColor = GetHexColor(Utility.Random<DataModels.HairColor>()),
                BeardColor = GetHexColor(Utility.Random<DataModels.HairColor>()),
                EyeColor = "#000000",
                Eyebrows = Utility.Random(0, gender == Gender.Male ? 10 : 7),
                Hair = Utility.Random(0, 38),
                FacialHair = gender == Gender.Male ? Utility.Random(0, 18) : -1,
                Head = Utility.Random(0, 23),
                HelmetVisible = true
            };
        }

        private static DataModels.SyntyAppearance GenerateSyntyAppearance(Appearance appearance)
        {
            return new DataModels.SyntyAppearance
            {
                Id = Guid.NewGuid(),
                Gender = appearance.Gender,
                SkinColor = GetHexColor(appearance.SkinColor),
                HairColor = GetHexColor(appearance.HairColor),
                BeardColor = GetHexColor(appearance.BeardColor),
                Hair = 0,
                FacialHair = 0,
                Head = 0,
                Eyebrows = 0,
                EyeColor = "#000000",
                HelmetVisible = appearance.HelmetVisible
            };
        }

        private static string GetHexColor(DataModels.HairColor color)
        {
            switch (color)
            {
                case DataModels.HairColor.Blonde:
                    return "#A8912A";
                case DataModels.HairColor.Blue:
                    return "#0D9BB9";
                case DataModels.HairColor.Brown:
                    return "#3C2823";
                case DataModels.HairColor.Grey:
                    return "#595959";
                case DataModels.HairColor.Pink:
                    return "#DF62C7";
                case DataModels.HairColor.Red:
                    return "#C52A4A";
                default:
                    return "#000000";
            }
        }

        private static string GetHexColor(DataModels.SkinColor color)
        {
            switch (color)
            {
                case DataModels.SkinColor.Light:
                    return "#d6b8ae";
                case DataModels.SkinColor.Medium:
                    return "#faa276";
                default:
                    return "#40251e";
            }
        }

        private static Appearance GenerateRandomAppearance()
        {
            return new Appearance
            {
                Id = Guid.NewGuid(),
                Gender = Utility.Random<DataModels.Gender>(),
                SkinColor = Utility.Random<DataModels.SkinColor>(),
                HairColor = Utility.Random<DataModels.HairColor>(),
                BrowColor = Utility.Random<DataModels.HairColor>(),
                BeardColor = Utility.Random<DataModels.HairColor>(),
                EyeColor = Utility.Random<DataModels.EyeColor>(),
                CostumeColor = Utility.Random<DataModels.CostumeColor>(),
                BaseModelNumber = Utility.Random(1, 20),
                TorsoModelNumber = Utility.Random(1, 7),
                BottomModelNumber = Utility.Random(1, 7),
                FeetModelNumber = 1,
                HandModelNumber = 1,
                BeltModelNumber = Utility.Random(0, 10),
                EyesModelNumber = Utility.Random(1, 7),
                BrowsModelNumber = Utility.Random(1, 15),
                MouthModelNumber = Utility.Random(1, 10),
                MaleHairModelNumber = Utility.Random(0, 10),
                FemaleHairModelNumber = Utility.Random(0, 20),
                BeardModelNumber = Utility.Random(0, 10)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<bool> AcquiredUserLockAsync(SessionToken token, RavenfallDbContext db, Character character)
        {
            var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
            return character.UserIdLock.GetValueOrDefault() == session.UserId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanEquipItem(Item item, Skills skills)
        {
            return item.RequiredDefenseLevel <= GameMath.ExperienceToLevel(skills.Defense) &&
                   item.RequiredAttackLevel <= GameMath.ExperienceToLevel(skills.Attack);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsItemBetter(Item itemA, Item itemB)
        {
            var valueA = GetItemValue(itemA);
            var valueB = GetItemValue(itemB);
            return valueA > valueB;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetItemValue(Item itemA)
        {
            return itemA.Level + itemA.WeaponAim + itemA.WeaponPower + itemA.ArmorPower;
        }
    }
}

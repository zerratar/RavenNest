using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using EntityState = Microsoft.EntityFrameworkCore.EntityState;

namespace RavenNest.BusinessLogic.Data
{
    public class GameData : IGameData
    {
        private const int SaveInterval = 60000 * 5; // every 5 minutes
        private const int SaveMaxBatchSize = 100;

        private readonly IRavenfallDbContextProvider db;
        private readonly ILogger logger;
        private readonly IKernel kernel;
        private readonly IQueryBuilder queryBuilder;

        private readonly EntitySet<Appearance, Guid> appearances;
        private readonly EntitySet<SyntyAppearance, Guid> syntyAppearances;
        private readonly EntitySet<Character, Guid> characters;
        private readonly EntitySet<CharacterState, Guid> characterStates;
        private readonly EntitySet<GameSession, Guid> gameSessions;
        private readonly EntitySet<GameEvent, Guid> gameEvents;
        private readonly EntitySet<InventoryItem, Guid> inventoryItems;
        private readonly EntitySet<MarketItem, Guid> marketItems;
        private readonly EntitySet<Item, Guid> items;
        private readonly EntitySet<Resources, Guid> resources;
        private readonly EntitySet<Statistics, Guid> statistics;
        private readonly EntitySet<Skills, Guid> skills;
        private readonly EntitySet<User, Guid> users;
        private readonly EntitySet<GameClient, Guid> gameClients;
        private ITimeoutHandle scheduleHandler;

        public GameData(IRavenfallDbContextProvider db, ILogger logger, IKernel kernel, IQueryBuilder queryBuilder)
        {
            this.db = db;
            this.logger = logger;
            this.kernel = kernel;
            this.queryBuilder = queryBuilder;

            using (var ctx = this.db.Get())
            {
                appearances = new EntitySet<Appearance, Guid>(ctx.Appearance.ToList(), i => i.Id);
                syntyAppearances = new EntitySet<SyntyAppearance, Guid>(ctx.SyntyAppearance.ToList(), i => i.Id);

                characters = new EntitySet<Character, Guid>(ctx.Character.ToList(), i => i.Id);
                characters.RegisterLookupGroup(nameof(User), x => x.UserId);
                characters.RegisterLookupGroup(nameof(GameSession), x => x.UserIdLock.GetValueOrDefault());

                characterStates = new EntitySet<CharacterState, Guid>(ctx.CharacterState.ToList(), i => i.Id);
                gameSessions = new EntitySet<GameSession, Guid>(ctx.GameSession.ToList(), i => i.Id);
                gameSessions.RegisterLookupGroup(nameof(User), x => x.UserId);

                gameEvents = new EntitySet<GameEvent, Guid>(ctx.GameEvent.ToList(), i => i.Id);
                gameEvents.RegisterLookupGroup(nameof(GameSession), x => x.GameSessionId);

                inventoryItems = new EntitySet<InventoryItem, Guid>(ctx.InventoryItem.ToList(), i => i.Id);
                inventoryItems.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                marketItems = new EntitySet<MarketItem, Guid>(ctx.MarketItem.ToList(), i => i.Id);
                marketItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                items = new EntitySet<Item, Guid>(ctx.Item.ToList(), i => i.Id);
                resources = new EntitySet<Resources, Guid>(ctx.Resources.ToList(), i => i.Id);
                statistics = new EntitySet<Statistics, Guid>(ctx.Statistics.ToList(), i => i.Id);
                skills = new EntitySet<Skills, Guid>(ctx.Skills.ToList(), i => i.Id);
                users = new EntitySet<User, Guid>(ctx.User.ToList(), i => i.Id);

                gameClients = new EntitySet<GameClient, Guid>(ctx.GameClient.ToList(), i => i.Id);

                Client = gameClients.Entities.First();
            }
        }

        public GameClient Client { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Item entity) => Update(() => items.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(CharacterState entity) => Update(() => characterStates.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(SyntyAppearance entity) => Update(() => syntyAppearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Statistics entity) => Update(() => statistics.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Skills entity) => Update(() => skills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Appearance entity) => Update(() => appearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Resources entity) => Update(() => resources.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Character entity) => Update(() => characters.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(User entity) => Update(() => users.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(InventoryItem entity) => Update(() => inventoryItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(GameSession entity) => Update(() => gameSessions.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(MarketItem entity) => Update(() => marketItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(GameEvent entity) => Update(() => gameEvents.Add(entity));

        public GameSession CreateSession(Guid userId)
        {
            return new GameSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Revision = 0,
                Started = DateTime.UtcNow,
                Status = (int)SessionStatus.Active
            };
        }

        public GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data)
        {
            return new GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                Type = (int)type,
                Revision = GetNextGameEventRevision(session.Id),
                Data = JSON.Stringify(data)
            };
        }

        // This is not code, it is a shrimp. Cant you see?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character FindCharacter(Func<Character, bool> predicate) =>
            characters.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem FindPlayerItem(Guid id, Func<InventoryItem, bool> predicate) =>
            characters.TryGet(id, out var player)
                ? inventoryItems[nameof(Character), player.Id].FirstOrDefault(predicate)
                : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> FindPlayerItems(Guid id, Func<InventoryItem, bool> predicate) =>
            characters.TryGet(id, out var player)
                ? inventoryItems[nameof(Character), player.Id].Where(predicate).ToList()
                : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession FindSession(Func<GameSession, bool> predicate) =>
            gameSessions.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User FindUser(Func<User, bool> predicate) => users.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User FindUser(string userIdOrUsername) => users.Entities.FirstOrDefault(x =>
            x.UserId == userIdOrUsername || x.UserName.Equals(userIdOrUsername, StringComparison.OrdinalIgnoreCase));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetAllPlayerItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacter(Guid characterId) => characters[characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacterByUserId(Guid userId) => characters[nameof(User), userId].FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate) =>
            characters.Entities.Where(predicate).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetEquippedItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].FirstOrDefault(x => x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetInventoryItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].FirstOrDefault(x => !x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetEquippedItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId].Where(x => x.Equipped).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetInventoryItems(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].Where(x => !x.Equipped).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetItem(Guid id) => items[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Item> GetItems() => items.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMarketItemCount() => marketItems.Entities.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(Guid itemId) => marketItems[nameof(Item), itemId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(int skip, int take) =>
            marketItems.Entities.Skip(skip).Take(take).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextGameEventRevision(Guid sessionId)
        {
            var events = GetSessionEvents(sessionId);
            if (events.Count == 0) return 1;
            return events.Max(x => x.Revision) + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetSession(Guid sessionId) => gameSessions[sessionId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession) =>
            characters[nameof(GameSession), currentSession.UserId].Where(x => x.LastUsed > currentSession.Started)
                .ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(GameSession gameSession) => GetSessionEvents(gameSession.Id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(Guid sessionId) => gameEvents[nameof(GameSession), sessionId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUser(Guid userId) => users[userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUser(string twitchUserId) => users.Entities.FirstOrDefault(x => x.UserId == twitchUserId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetUserSession(Guid userId) => gameSessions[nameof(User), userId]
            .OrderByDescending(x => x.Started).FirstOrDefault(x => x.Stopped == null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(MarketItem marketItem) => marketItems.Remove(marketItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(InventoryItem invItem) => inventoryItems.Remove(invItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(IReadOnlyList<InventoryItem> items) => items.ForEach(Remove);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Resources GetResources(Guid resourcesId) => resources[resourcesId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Resources GetResourcesByCharacterId(Guid sellerCharacterId) =>
            GetResources(GetCharacter(sellerCharacterId).ResourcesId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Statistics GetStatistics(Guid statisticsId) => statistics[statisticsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyntyAppearance GetAppearance(Guid? syntyAppearanceId) =>
            syntyAppearanceId == null ? null : syntyAppearances[syntyAppearanceId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skills GetSkills(Guid skillsId) => skills[skillsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterState GetState(Guid? stateId) => stateId == null ? null : characterStates[stateId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetActiveSessions() => gameSessions.Entities.OrderByDescending(x => x.Started)
            .Where(x => x.Stopped == null).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ScheduleNextSave()
        {
            if (scheduleHandler != null) return;
            scheduleHandler = kernel.SetTimeout(SaveChanges, SaveInterval);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Update(Action update)
        {
            if (update == null) return;
            update.Invoke();
            ScheduleNextSave();
        }

        private void SaveChanges()
        {
            kernel.ClearTimeout(scheduleHandler);
            scheduleHandler = null;
            try
            {
                logger.WriteDebug("Saving all pending changes to the database.");

                var queue = BuildSaveQueue();
                using (var ctx = db.Get())
                {
                    while (queue.TryPeek(out var saveData))
                    {
                        var query = queryBuilder.Build(saveData);
                        if (query == null) return;
                        var result = ctx.Database.ExecuteSqlCommand(query.Command, query.Parameters);
                        if (result == 0)
                        {
                            logger.WriteError("Unable to save data! Abort Query failed: ");
                            return;
                        }

                        queue.Dequeue();
                    }
                }

                // do actual save logic                
            }
            catch (Exception exc)
            {
                logger.WriteError("ERROR SAVING DATA!! " + exc);
                // log this
            }
            finally
            {
                ScheduleNextSave();
            }
        }


        private Queue<EntityStoreItems> BuildSaveQueue()
        {
            Queue<EntityStoreItems> queue = new Queue<EntityStoreItems>();

            var addedItems = JoinChangeSets(appearances.Added, syntyAppearances.Added, characters.Added, characterStates.Added,
                gameSessions.Added, gameEvents.Added, inventoryItems.Added, marketItems.Added, items.Added,
                resources.Added, statistics.Added, skills.Added, users.Added, gameClients.Added);

            // All adds must be added sequential in the same batch, since an added item may refer to an entity that comes in the next batch.
            foreach (var batch in CreateBatches(RavenNest.DataModels.EntityState.Added, addedItems, int.MaxValue))
            {
                queue.Enqueue(batch);
            }

            var updateItems = JoinChangeSets(appearances.Updated, syntyAppearances.Updated, characters.Updated, characterStates.Updated,
                gameSessions.Updated, gameEvents.Updated, inventoryItems.Updated, marketItems.Updated, items.Updated,
                resources.Updated, statistics.Updated, skills.Updated, users.Updated, gameClients.Updated);

            foreach (var batch in CreateBatches(RavenNest.DataModels.EntityState.Modified, updateItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var deletedItems = JoinChangeSets(appearances.Removed, syntyAppearances.Removed, characters.Removed, characterStates.Removed,
                gameSessions.Removed, gameEvents.Removed, inventoryItems.Removed, marketItems.Removed, items.Removed,
                resources.Removed, statistics.Removed, skills.Removed, users.Removed, gameClients.Removed);

            foreach (var batch in CreateBatches(RavenNest.DataModels.EntityState.Deleted, deletedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            return queue;
        }

        private ICollection<EntityStoreItems> CreateBatches(RavenNest.DataModels.EntityState state, ICollection<EntityChangeSet> items, int batchSize)
        {
            if (items == null || items.Count == 0) return new List<EntityStoreItems>();
            var batches = (int)Math.Floor(items.Count / (float)batchSize) + 1;
            var batchList = new List<EntityStoreItems>(batches);
            for (var i = 0; i < batchList.Count; ++i)
            {
                batchList[i] = new EntityStoreItems(state, items.Skip(i * batchSize).Take(batchSize).Select(x => x.Entity));
            }
            return batchList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ICollection<EntityChangeSet> JoinChangeSets(params ICollection<EntityChangeSet>[] changesets)
        {
            return changesets.SelectMany(x => x).OrderBy(x => x.LastModified).ToList();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public interface IGameData
    {
        GameClient Client { get; }

        #region Find
        User FindUser(Func<User, bool> predicate);
        Character FindCharacter(Func<Character, bool> predicate);
        GameSession FindSession(Func<GameSession, bool> predicate);
        User FindUser(string userIdOrUsername);

        /// <summary>
        /// Find player items by predicate
        /// </summary>
        /// <param name="id"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> FindPlayerItems(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem FindPlayerItem(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        #endregion

        #region Get
        User GetUser(Guid userId);
        IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate);
        IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession);
        User GetUser(string twitchUserId);
        int GetMarketItemCount();
        int GetNextGameEventRevision(Guid id);
        GameSession GetSession(Guid sessionId);
        GameSession GetUserSession(Guid userId);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(GameSession gameSession);
        IReadOnlyList<DataModels.Item> GetItems();
        DataModels.Item GetItem(Guid id);
        IReadOnlyList<DataModels.InventoryItem> GetInventoryItems(Guid characterId, Guid itemId);

        DataModels.InventoryItem GetInventoryItem(Guid characterId, Guid itemId);
        DataModels.InventoryItem GetEquippedItem(Guid characterId, Guid itemId);
        Character GetCharacter(Guid characterId);
        Character GetCharacterByUserId(Guid userId);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(Guid itemId);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(int skip, int take);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(Guid sessionId);
        /// <summary>
        /// Gets all player items, this includes both equipped and items in the inventory.
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> GetAllPlayerItems(Guid characterId);

        /// <summary>
        /// Gets all equipped player items
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> GetEquippedItems(Guid characterId);

        #endregion

        #region Create
        GameSession CreateSession(Guid userId, bool isLocal);
        DataModels.GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data);
        #endregion

        #region Add
        void Add(DataModels.Item entity);
        void Add(DataModels.CharacterState state);
        void Add(DataModels.SyntyAppearance syntyAppearance);
        void Add(DataModels.Statistics statistics);
        void Add(DataModels.Skills skills);
        void Add(DataModels.Appearance appearance);
        void Add(DataModels.Resources resources);
        void Add(Character character);
        void Add(User user);
        void Add(DataModels.InventoryItem inventoryItem);
        void Add(GameSession gameSession);
        void Add(DataModels.MarketItem marketItem);
        void Add(DataModels.GameEvent permissionEvent);

        #endregion

        #region Update
        void Update(DataModels.CharacterState state);
        void Update(DataModels.Resources resources);
        void Update(DataModels.InventoryItem itemToEquip);
        void Update(DataModels.Skills skills);
        void Update(DataModels.SyntyAppearance syntyAppearance);
        void Update(DataModels.Appearance appearance);
        void Update(DataModels.MarketItem marketItem);
        void Update(GameSession session);
        void Update(Character character);
        void UpdateRange(IEnumerable<DataModels.InventoryItem> weapons);

        #endregion

        #region Remove
        void Remove(DataModels.MarketItem marketItem);
        void Remove(DataModels.InventoryItem invItem);
        void RemoveRange(IReadOnlyList<DataModels.InventoryItem> itemsToSell);
        Resources GetResources(Guid resourcesId);
        Resources GetResourcesByCharacterId(Guid sellerCharacterId);
        DataModels.Statistics GetStatistics(Guid statisticsId);
        void Update(Statistics characterStatistics);
        SyntyAppearance GetAppearance(Guid? syntyAppearanceId);
        Skills GetSkills(Guid skillsId);
        DataModels.CharacterState GetState(Guid? stateId);
        #endregion
    }

    public class GameData : IGameData
    {
        private readonly IRavenfallDbContextProvider db;

        public GameData(IRavenfallDbContextProvider db)
        {
            this.db = db;
            LoadData();
        }

        private void LoadData()
        {
            using (var ctx = this.db.Get())
            {
                ctx.Appearance.ToList();
                //SyntyAppearance
                //Character
                //CharacterState
                //CharacterSession
                //GameSession
                //GameEvent
                //InventoryItem
                //MarketItem
                //Item
                //Resources
                //Statistics
                //Skills
                //User
                //ServerLogs
                //GameClient
            }
        }

        public GameClient Client => throw new NotImplementedException();

        public void Add(Item entity)
        {
            throw new NotImplementedException();
        }

        public void Add(CharacterState state)
        {
            throw new NotImplementedException();
        }

        public void Add(SyntyAppearance syntyAppearance)
        {
            throw new NotImplementedException();
        }

        public void Add(Statistics statistics)
        {
            throw new NotImplementedException();
        }

        public void Add(Skills skills)
        {
            throw new NotImplementedException();
        }

        public void Add(Appearance appearance)
        {
            throw new NotImplementedException();
        }

        public void Add(Resources resources)
        {
            throw new NotImplementedException();
        }

        public void Add(Character character)
        {
            throw new NotImplementedException();
        }

        public void Add(User user)
        {
            throw new NotImplementedException();
        }

        public void Add(InventoryItem inventoryItem)
        {
            throw new NotImplementedException();
        }

        public void Add(GameSession gameSession)
        {
            throw new NotImplementedException();
        }

        public void Add(MarketItem marketItem)
        {
            throw new NotImplementedException();
        }

        public void Add(GameEvent permissionEvent)
        {
            throw new NotImplementedException();
        }

        public GameSession CreateSession(Guid userId, bool isLocal)
        {
            throw new NotImplementedException();
        }

        public GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data)
        {
            throw new NotImplementedException();
        }

        public Character FindCharacter(Func<Character, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public InventoryItem FindPlayerItem(Guid id, Func<InventoryItem, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<InventoryItem> FindPlayerItems(Guid id, Func<InventoryItem, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public GameSession FindSession(Func<GameSession, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public User FindUser(Func<User, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public User FindUser(string userIdOrUsername)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<InventoryItem> GetAllPlayerItems(Guid characterId)
        {
            throw new NotImplementedException();
        }

        public Character GetCharacter(Guid characterId)
        {
            throw new NotImplementedException();
        }

        public Character GetCharacterByUserId(Guid userId)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public InventoryItem GetEquippedItem(Guid characterId, Guid itemId)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<InventoryItem> GetEquippedItems(Guid characterId)
        {
            throw new NotImplementedException();
        }

        public InventoryItem GetInventoryItem(Guid characterId, Guid itemId)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<InventoryItem> GetInventoryItems(Guid characterId, Guid itemId)
        {
            throw new NotImplementedException();
        }

        public Item GetItem(Guid id)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<Item> GetItems()
        {
            throw new NotImplementedException();
        }

        public int GetMarketItemCount()
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<MarketItem> GetMarketItems(Guid itemId)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<MarketItem> GetMarketItems(int skip, int take)
        {
            throw new NotImplementedException();
        }

        public int GetNextGameEventRevision(Guid id)
        {
            throw new NotImplementedException();
        }

        public GameSession GetSession(Guid sessionId)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<GameEvent> GetSessionEvents(GameSession gameSession)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<GameEvent> GetSessionEvents(Guid sessionId)
        {
            throw new NotImplementedException();
        }

        public User GetUser(Guid userId)
        {
            throw new NotImplementedException();
        }

        public User GetUser(string twitchUserId)
        {
            throw new NotImplementedException();
        }

        public GameSession GetUserSession(Guid userId)
        {
            throw new NotImplementedException();
        }

        public void Remove(MarketItem marketItem)
        {
            throw new NotImplementedException();
        }

        public void Remove(InventoryItem invItem)
        {
            throw new NotImplementedException();
        }

        public void RemoveRange(IReadOnlyList<InventoryItem> itemsToSell)
        {
            throw new NotImplementedException();
        }

        public void Update(CharacterState state)
        {
            throw new NotImplementedException();
        }

        public void Update(Resources resources)
        {
            throw new NotImplementedException();
        }

        public void Update(InventoryItem itemToEquip)
        {
            throw new NotImplementedException();
        }

        public void Update(Skills skills)
        {
            throw new NotImplementedException();
        }

        public void Update(SyntyAppearance syntyAppearance)
        {
            throw new NotImplementedException();
        }

        public void Update(Appearance appearance)
        {
            throw new NotImplementedException();
        }

        public void Update(MarketItem marketItem)
        {
            throw new NotImplementedException();
        }

        public void Update(GameSession session)
        {
            throw new NotImplementedException();
        }

        public void Update(Character character)
        {
            throw new NotImplementedException();
        }

        public void UpdateRange(IEnumerable<InventoryItem> weapons)
        {
            throw new NotImplementedException();
        }

        public Resources GetResources(Guid resourcesId)
        {
            throw new NotImplementedException();
        }

        public Resources GetResourcesByCharacterId(Guid sellerCharacterId)
        {
            throw new NotImplementedException();
        }

        public Statistics GetStatistics(Guid statisticsId)
        {
            throw new NotImplementedException();
        }

        public void Update(Statistics characterStatistics)
        {
            throw new NotImplementedException();
        }

        public SyntyAppearance GetAppearance(Guid? syntyAppearanceId)
        {
            throw new NotImplementedException();
        }

        public Skills GetSkills(Guid skillsId)
        {
            throw new NotImplementedException();
        }

        public CharacterState GetState(Guid? stateId)
        {
            throw new NotImplementedException();
        }
    }
}

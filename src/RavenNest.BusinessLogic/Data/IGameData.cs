using System;
using System.Collections.Generic;
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
        /// <param name="characterId"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> FindPlayerItems(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem FindPlayerItem(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        #endregion

        #region Get
        User GetUser(Guid userId);
        IReadOnlyList<DataModels.Character> GetCharacters(Func<Character, bool> predicate);
        IReadOnlyList<DataModels.Character> GetSessionCharacters(GameSession currentSession);
        User GetUser(string twitchUserId);
        int GetMarketItemCount();
        int GetNextGameEventRevision(Guid sessionId);
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
        GameSession CreateSession(Guid userId);
        DataModels.GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data);
        #endregion

        #region Add
        void Add(DataModels.Item entity);
        void Add(DataModels.CharacterState entity);
        void Add(DataModels.SyntyAppearance entity);
        void Add(DataModels.Statistics entity);
        void Add(DataModels.Skills entity);
        void Add(DataModels.Appearance entity);
        void Add(DataModels.Resources entity);
        void Add(Character entity);
        void Add(User entity);
        void Add(DataModels.InventoryItem entity);
        void Add(GameSession entity);
        void Add(DataModels.MarketItem entity);
        void Add(DataModels.GameEvent entity);

        /// <summary>
        ///     Force save the current state to the database.
        /// </summary>
        void Flush();

        #endregion

        //#region Update
        //void Update(DataModels.CharacterState state);
        //void Update(DataModels.Resources resources);
        //void Update(DataModels.InventoryItem itemToEquip);
        //void Update(DataModels.Skills skills);
        //void Update(DataModels.SyntyAppearance syntyAppearance);
        //void Update(DataModels.Appearance appearance);
        //void Update(DataModels.MarketItem marketItem);
        //void Update(GameSession session);
        //void Update(Character character);
        //void UpdateRange(IEnumerable<DataModels.InventoryItem> weapons);
        //#endregion

        #region Remove
        void Remove(DataModels.MarketItem marketItem);
        void Remove(DataModels.InventoryItem invItem);
        void RemoveRange(IReadOnlyList<DataModels.InventoryItem> items);
        Resources GetResources(Guid resourcesId);
        Resources GetResourcesByCharacterId(Guid sellerCharacterId);
        DataModels.Statistics GetStatistics(Guid statisticsId);
        SyntyAppearance GetAppearance(Guid? syntyAppearanceId);
        Skills GetSkills(Guid skillsId);
        DataModels.CharacterState GetState(Guid? stateId);
        #endregion

        IReadOnlyList<GameSession> GetActiveSessions();
    }
}

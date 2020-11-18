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
        DataModels.GameSession FindSession(Func<DataModels.GameSession, bool> predicate);
        User FindUser(string userIdOrUsername);
        DataModels.Village GetVillageBySession(DataModels.GameSession session);
        DataModels.Village GetOrCreateVillageBySession(DataModels.GameSession session);
        IReadOnlyList<DataModels.VillageHouse> GetOrCreateVillageHouses(DataModels.Village village);
        Character GetCharacterByName(string username, string identifier);

        /// <summary>
        /// Find player items by predicate
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> FindPlayerItems(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem FindPlayerItem(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        UserPatreon GetPatreonUser(long patreonId);
        UserPatreon GetPatreonUser(Guid userId);
        DataModels.GameSession GetSessionByUserId(string userId);

        #endregion

        #region Get
        User GetUser(Guid userId);
        IReadOnlyList<DataModels.User> GetUsers();
        IReadOnlyList<DataModels.Character> GetCharacters(Func<Character, bool> predicate);
        IReadOnlyList<DataModels.Character> GetCharacters();
        IReadOnlyList<DataModels.Character> GetSessionCharacters(DataModels.GameSession currentSession);
        User GetUser(string twitchUserId);
        UserLoyalty GetUserLoyalty(Guid userId);
        int GetMarketItemCount();
        int GetNextGameEventRevision(Guid sessionId);
        DataModels.GameSession GetSession(Guid sessionId, bool updateSession = true);
        DataModels.GameSession GetUserSession(Guid userId, bool updateSession = true);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(DataModels.GameSession gameSession);
        IReadOnlyList<DataModels.GameEvent> GetUserEvents(Guid userId);
        IReadOnlyList<DataModels.Item> GetItems();
        DataModels.Item GetItem(Guid id);
        IReadOnlyList<DataModels.InventoryItem> GetInventoryItems(Guid characterId, Guid itemId);

        IReadOnlyList<DataModels.InventoryItem> GetInventoryItems(Guid characterId);

        DataModels.InventoryItem GetInventoryItem(Guid characterId, Guid itemId);
        DataModels.InventoryItem GetEquippedItem(Guid characterId, Guid itemId);
        Character GetCharacter(Guid characterId);
        Character GetCharacterByUserId(Guid userId, string identifier);
        Character GetCharacterByUserId(string twitchUserId, string identifier);
        Character GetCharacterBySession(Guid sessionId, string userId);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(Guid itemId, string tag = null);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(int skip, int take);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(Guid sessionId);
        ExpMultiplierEvent GetActiveExpMultiplierEvent();

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
        DataModels.GameSession CreateSession(Guid userId);
        DataModels.GameEvent CreateSessionEvent<T>(GameEventType type, DataModels.GameSession session, T data);
        #endregion

        #region Add
        void Add(CharacterClanMembership ev);
        void Add(ClanRole ev);
        void Add(Clan ev);

        void Add(UserLoyalty loyalty);
        void Add(UserLoyaltyRank loyaltyRank);
        void Add(UserLoyaltyReward loyaltyRankReward);

        void Add(UserPatreon pat);
        IReadOnlyList<CharacterClanMembership> GetClanMemberships(Guid clanId);
        void Add(ExpMultiplierEvent ev);
        void Add(CharacterSessionActivity ev);
        void Add(DataModels.Item entity);
        void Add(DataModels.ItemCraftingRequirement entity);
        void Add(DataModels.CharacterState entity);
        void Add(DataModels.SyntyAppearance entity);
        void Add(DataModels.Statistics entity);
        void Add(DataModels.Skills entity);
        void Add(DataModels.Appearance entity);
        void Add(DataModels.Resources entity);
        void Add(Character entity);
        void Add(User entity);
        void Add(DataModels.InventoryItem entity);
        void Add(DataModels.GameSession entity);
        void Add(DataModels.MarketItem entity);
        void Add(DataModels.GameEvent entity);
        void Add(DataModels.Village village);

        /// <summary>
        ///     Force save the current state to the database.
        /// </summary>
        void Flush();

        #endregion

        #region Remove
        void Remove(CharacterClanMembership ev);
        void Remove(ClanRole ev);
        void Remove(Clan ev);
        void Remove(CharacterSessionActivity ev);
        void Remove(DataModels.GameEvent gameEvent);
        void Remove(DataModels.ItemCraftingRequirement craftingRequirement);
        void Remove(DataModels.User user);
        void Remove(DataModels.Skills skills);
        void Remove(DataModels.Statistics statistics);
        void Remove(DataModels.Character character);
        void Remove(DataModels.Resources resources);
        void Remove(DataModels.MarketItem marketItem);
        void Remove(DataModels.InventoryItem invItem);
        void RemoveRange(IReadOnlyList<DataModels.InventoryItem> items);
        DataModels.Resources GetResources(Guid resourcesId);
        DataModels.Resources GetResourcesByCharacterId(Guid sellerCharacterId);
        DataModels.Statistics GetStatistics(Guid statisticsId);
        DataModels.Clan GetClan(Guid clanId);
        DataModels.Clan GetClanByUser(Guid userId);
        DataModels.ClanRole GetClanRole(Guid roleId);
        IReadOnlyList<DataModels.ClanRole> GetClanRoles(Guid clanId);
        DataModels.CharacterClanMembership GetClanMembership(Guid characterId);

        DataModels.SyntyAppearance GetAppearance(Guid? syntyAppearanceId);
        DataModels.Skills GetSkills(Guid skillsId);
        DataModels.CharacterState GetState(Guid? stateId);
        #endregion

        IReadOnlyList<DataModels.GameSession> GetActiveSessions();
        IReadOnlyList<DataModels.ItemCraftingRequirement> GetCraftingRequirements(Guid itemId);
        CharacterSessionState GetCharacterSessionState(Guid sessionId, Guid characterId);
        SessionState GetSessionState(Guid sessionId);

        DataModels.InventoryItem GetEquippedItem(Guid id, DataModels.ItemCategory category);

        object SyncLock { get; }
        bool InitializedSuccessful { get; }

        CharacterSessionActivity GetSessionActivity(Guid id, Guid characterId);
    }
}

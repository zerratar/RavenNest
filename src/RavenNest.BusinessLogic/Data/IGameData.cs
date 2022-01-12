using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public interface IGameData
    {
        byte[] GetCompressedEntities();

        GameClient Client { get; }

        #region Find
        User FindUser(Func<User, bool> predicate);
        Character FindCharacter(Func<Character, bool> predicate);
        DataModels.GameSession FindSession(Func<DataModels.GameSession, bool> predicate);
        IReadOnlyList<DataModels.GameSession> FindSessions(Func<DataModels.GameSession, bool> predicate);
        User FindUser(string userIdOrUsername);
        DataModels.Village GetVillage(Guid villageId);
        DataModels.Village GetVillageByUserId(Guid userId);
        DataModels.Village GetVillageBySession(DataModels.GameSession session);
        DataModels.Village GetOrCreateVillageBySession(DataModels.GameSession session);
        IReadOnlyList<DataModels.Village> GetVillages();
        IReadOnlyList<DataModels.VillageHouse> GetOrCreateVillageHouses(DataModels.Village village);
        IReadOnlyList<DataModels.VillageHouse> GetVillageHouses(DataModels.Village village);

        Character GetCharacterByName(string username, string identifier);
        IReadOnlyList<UserLoyaltyReward> GetLoyaltyRewards();
        IReadOnlyList<Agreements> GetAllAgreements();

        /// <summary>
        /// Find player items by predicate
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IReadOnlyList<DataModels.InventoryItem> FindPlayerItems(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem FindPlayerItem(Guid characterId, Func<DataModels.InventoryItem, bool> predicate);
        UserPatreon GetPatreonUser(long patreonId);
        UserNotification GetNotification(Guid notificationId);
        UserPatreon GetPatreonUser(Guid userId);
        IReadOnlyList<Skill> GetSkills();
        DataModels.GameSession GetJoinedSessionByUserId(string userId);

        void SetUserProperty(Guid userId, string propertyKey, string propertyValue);
        string GetUserProperty(Guid userId, string propertyKey, string defaultPropertyValue = null);

        DataModels.GameSession GetOwnedSessionByUserId(string userId);
        DataModels.GameSession GetSessionByCharacterId(Guid characterId);
        CharacterClanInvite GetClanInvite(Guid inviteId);
        IReadOnlyList<CharacterClanInvite> GetClanInvitesByCharacter(Guid characterId);
        IReadOnlyList<CharacterClanInvite> GetClanInvitesSent(Guid userId);
        IReadOnlyList<CharacterClanInvite> GetClanInvites(Guid clanId);

        /// <summary>
        /// Clears all the character session states available for any previous or current session of this user.
        /// </summary>
        /// <param name="userId"></param>
        void ClearAllCharacterSessionStates(Guid userId);
        void ClearCharacterSessionStates(Guid sessionId);

        #endregion

        #region Get
        User GetUser(Guid userId);
        IReadOnlyList<DataModels.User> GetUsers();
        IReadOnlyList<DataModels.Character> GetCharacters(Func<Character, bool> predicate);
        IReadOnlyList<DataModels.Character> GetCharacters();
        IReadOnlyList<DataModels.Character> GetSessionCharacters(DataModels.GameSession currentSession, bool activeSessionOnly = true);

        ICollection<DataModels.RedeemableItem> GetRedeemableItems();
        RedeemableItem GetRedeemableItemByItemId(Guid itemId);
        User GetUserByTwitchId(string twitchUserId);
        User GetUserByUsername(string username);
        UserLoyalty GetUserLoyalty(Guid userId, Guid streamerUserId);
        IReadOnlyList<UserLoyalty> GetUserLoyalties(Guid userId);
        IReadOnlyList<UserLoyalty> GetStreamerLoyalties(Guid streamerUserId);
        int GetMarketItemCount();
        int GetNextGameEventRevision(Guid sessionId);
        DataModels.GameSession GetSession(Guid sessionId, bool updateSession = true);
        DataModels.GameSession GetSessionByUserId(Guid userId, bool updateSession = true);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(DataModels.GameSession gameSession);
        IReadOnlyList<DataModels.GameEvent> GetUserEvents(Guid userId);
        IReadOnlyList<DataModels.ClanSkill> GetClanSkills(Guid id);
        IReadOnlyList<DataModels.ClanSkill> GetClanSkills();
        IReadOnlyList<DataModels.Item> GetItems();
        DataModels.Item GetItem(Guid id);
        DataModels.Skill GetSkill(Guid skillId);
        IReadOnlyList<DataModels.InventoryItem> GetInventoryItems(Guid characterId, Guid itemId);
        IReadOnlyList<DataModels.InventoryItem> GetInventoryItems(Guid characterId);

        DataModels.InventoryItem GetInventoryItem(Guid inventoryItemId);
        DataModels.InventoryItem GetInventoryItem(Guid characterId, Guid itemId);
        DataModels.InventoryItem GetEquippedItem(Guid characterId, Guid itemId);
        Character GetCharacter(Guid characterId);
        Character GetCharacterByUserId(Guid userId, string identifier = "0");
        Character GetCharacterByUserId(string twitchUserId, string identifier);
        Character GetCharacterBySession(Guid sessionId, string userId, bool updateSession = true);
        IReadOnlyList<Character> GetCharactersByUserId(Guid userId);
        IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions();
        IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(DateTime start, DateTime end);
        IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(Guid itemId, DateTime start, DateTime end);
        IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsBySeller(Guid seller, DateTime start, DateTime end);
        IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsByBuyer(Guid buyer, DateTime start, DateTime end);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(Guid itemId, string tag = null);
        DataModels.MarketItem GetMarketItem(Guid marketItemId);

        bool RemoveFromStash(UserBankItem bankItemScroll, int amount);
        UserBankItem GetStashItem(Guid userId, Guid itemId);
        IReadOnlyList<UserBankItem> GetUserBankItems(Guid id);
        IReadOnlyList<DataModels.MarketItem> GetMarketItems(int skip, int take);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(Guid sessionId);
        IReadOnlyList<DataModels.UserNotification> GetNotifications(Guid userId);

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
        void Add(VendorTransaction entity);
        void Add(DataModels.UserBankItem item);
        void Add(Agreements item);
        void Add(RedeemableItem item);
        void Add(ClanSkill entity);
        void Add(MarketItemTransaction transaction);
        void Add(UserNotification ev);
        void Add(CharacterClanMembership ev);
        void Add(CharacterClanInvite ev);
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

        void Remove(DataModels.UserBankItem item);
        void Remove(Agreements item);
        void Remove(RedeemableItem item);
        void Remove(UserNotification ev);
        void Remove(CharacterClanMembership ev);
        void Remove(CharacterClanInvite ev);
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
        DataModels.Skills GetCharacterSkills(Guid skillsId);
        DataModels.CharacterState GetCharacterState(Guid? stateId);
        #endregion

        IReadOnlyList<DataModels.GameSession> GetActiveSessions();
        IReadOnlyList<DataModels.GameSession> GetSessions();
        IReadOnlyList<DataModels.ItemCraftingRequirement> GetCraftingRequirements(Guid itemId);
        CharacterSessionState GetCharacterSessionState(Guid sessionId, Guid characterId);
        void ResetCharacterSessionState(Guid sessionId, Guid characterId);
        SessionState GetSessionState(Guid sessionId);

        DataModels.InventoryItem GetEquippedItem(Guid id, DataModels.ItemCategory category);

        object SyncLock { get; }
        bool InitializedSuccessful { get; }

        CharacterSessionActivity GetSessionActivity(Guid id, Guid characterId);
        IReadOnlyList<ItemAttribute> GetItemAttributes();
        IReadOnlyList<Clan> GetClans();
        UserBankItem GetUserBankItem(Guid id);
    }
}

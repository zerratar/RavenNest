using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerManager
    {
        Task<Player> CreatePlayerIfNotExists(string userId, string userName, string identifier);
        Task<Player> CreatePlayer(string userId, string userName, string identifier);
        Task<bool> RemovePlayerFromActiveSession(SessionToken token, Guid characterId);
        Task<bool> RemovePlayerFromActiveSession(DataModels.GameSession session, Guid characterId);
        Task<Player> AddPlayer(SessionToken token, Guid characterId);
        Task<PlayerJoinResult> AddPlayer(SessionToken token, PlayerJoinData playerJoinData);
        Task<PlayerJoinResult> AddPlayer(SessionToken token, string userId, string userName, string identifier = null);
        Task<PlayerJoinResult> AddPlayerByCharacterId(DataModels.GameSession session, Guid characterId, bool isGameRestore = false);
        Player GetPlayer(SessionToken sessionToken, string userId);
        Player GetPlayer(SessionToken sessionToken);
        Player GetPlayer(Guid characterId);
        Player GetPlayer(string userId, string identifier);
        Player GetPlayer(Guid userId, string identifier);
        WebsitePlayer GetWebsitePlayer(Guid characterId);
        WebsitePlayer GetWebsitePlayer(string userId, string identifier);
        WebsitePlayer GetWebsitePlayer(Guid userId, string identifier);
        WebsitePlayer GetWebsitePlayer(User user, Character character);
        IReadOnlyList<WebsitePlayer> GetWebsitePlayers(string userId);
        bool SendRemovePlayerFromSessionToGame(DataModels.Character character, DataModels.GameSession joiningSession = null);
        void UpdateUserLoyalty(SessionToken sessionToken, UserLoyaltyUpdate update);
        void UpdatePlayerActivity(SessionToken sessionToken, PlayerSessionActivity update);
        bool UpdatePlayerState(SessionToken sessionToken, CharacterStateUpdate update);

        bool UpdateStatistics(SessionToken token, string userId, double[] statistics, Guid? characterId = null);

        bool UpdateAppearance(SessionToken token, string userId, Models.SyntyAppearance appearance);
        bool UpdateAppearance(Guid characterId, Models.SyntyAppearance appearance);
        bool SendToCharacter(Guid characterId, Models.UserBankItem item, long amount);
        bool UpdateAppearance(string userId, string identifier, Models.SyntyAppearance appearance);
        bool ReturnMarketplaceItem(DataModels.MarketItem item);
        bool UpdateAppearance(AuthToken token, string userId, string identifier, Models.SyntyAppearance appearance);
        Task<bool> UpdatePlayerSkillAsync(Guid characterId, string skillName, int level, float levelProgress);
        bool UpdateExperience(SessionToken token, string userId, int[] level, double[] experience, Guid? characterId = null);
        bool UpdateExperience(SessionToken token, int skillIndex, int level, double experience, Guid characterId);
        bool UpdateResources(SessionToken token, string userId, double[] resources);

        //bool[] UpdateMany(SessionToken token, PlayerState[] states);
        bool LoyaltyGift(string gifterTwitchUserIdOrName, string streamerTwitchUserIdOrName, int bitsAmount, int subsAmount);
        void AddItem(Guid characterId, Guid itemId, int amount = 1);
        AddItemResult AddItem(SessionToken token, string userId, Guid itemId);
        AddItemResult CraftItem_Old(SessionToken token, string userId, Guid itemId, int amount = 1);
        ItemEnchantmentResult EnchantItem(SessionToken token, string userId, Guid inventoryItemId);
        CraftItemResult CraftItems(SessionToken token, string userId, Guid itemId, int amount = 1);

        long GiftItem(SessionToken token, string gifterUserId, string receiverUserId, Guid itemId, long amount);
        long VendorItem(SessionToken token, string userId, Guid itemId, long amount);
        bool EquipItemInstance(SessionToken token, string userId, Guid inventoryItemId);
        bool EquipItem(SessionToken token, string userId, Guid itemId);
        bool EquipItem(Guid characterId, Models.InventoryItem item);
        bool UnequipItemInstance(SessionToken token, string userId, Guid inventoryItemId);
        bool UnequipItem(SessionToken token, string userId, Guid itemId);
        bool UnequipItem(Guid characterId, Models.InventoryItem item);
        bool SendToStash(Guid characterId, Models.InventoryItem item, long amount);
        bool SendToCharacter(Guid characterId, Guid otherCharacterId, Models.InventoryItem item, long amount);
        bool EquipBestItems(SessionToken token, string userId);
        bool UnequipAllItems(SessionToken token, string userId);

        bool ToggleHelmet(SessionToken token, string userId);

        ItemCollection GetEquippedItems(SessionToken token, string userId);
        ItemCollection GetAllItems(SessionToken token, string userId);
        IReadOnlyList<Player> GetPlayers();
        IReadOnlyList<WebsiteAdminPlayer> GetFullPlayers();
        void EquipBestItems(DataModels.Character character);
        bool AcquiredUserLock(SessionToken token, DataModels.Character character);
        bool AddTokens(SessionToken sessionToken, string userId, int amount);
        RedeemItemResult RedeemItem(SessionToken sessionToken, Guid characterId, Guid itemId);
        int RedeemTokens(SessionToken sessionToken, string userId, int amount, bool exact);
        int GetHighscore(SessionToken sessionToken, Guid characterId, string skillName);
        void SendPlayerTaskToGame(DataModels.GameSession activeSession, Character character, string task, string taskArgument);
        void RemoveUserFromSessions(User user);
    }
}

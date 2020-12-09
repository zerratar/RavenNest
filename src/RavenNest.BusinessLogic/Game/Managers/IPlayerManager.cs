using System;
using System.Collections.Generic;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerManager
    {
        Player CreatePlayerIfNotExists(string userId, string userName, string identifier);
        Player CreatePlayer(string userId, string userName, string identifier);
        bool RemovePlayerFromActiveSession(SessionToken token, Guid characterId);
        PlayerJoinResult AddPlayer(SessionToken token, PlayerJoinData playerJoinData);
        PlayerJoinResult AddPlayer(SessionToken token, string userId, string userName, string identifier = null);
        Player AddPlayer(SessionToken token, Guid characterId);
        Player GetPlayer(SessionToken sessionToken, string userId);
        Player GetPlayer(SessionToken sessionToken);
        Player GetPlayer(Guid characterId);
        Player GetPlayer(string userId, string identifier);
        Player GetPlayer(Guid userId, string identifier);
        WebsitePlayer GetWebsitePlayer(Guid characterId);
        WebsitePlayer GetWebsitePlayer(string userId, string identifier);
        WebsitePlayer GetWebsitePlayer(Guid userId, string identifier);
        IReadOnlyList<WebsitePlayer> GetWebsitePlayers(string userId);
        void UpdateUserLoyalty(SessionToken sessionToken, UserLoyaltyUpdate update);
        void UpdatePlayerActivity(SessionToken sessionToken, PlayerSessionActivity update);
        bool UpdatePlayerState(SessionToken sessionToken, CharacterStateUpdate update);

        bool UpdateStatistics(SessionToken token, string userId, decimal[] statistics, Guid? characterId = null);

        bool UpdateAppearance(SessionToken token, string userId, Models.SyntyAppearance appearance);
        bool UpdateAppearance(Guid characterId, Models.SyntyAppearance appearance);
        bool UpdateAppearance(string userId, string identifier, Models.SyntyAppearance appearance);

        bool UpdateAppearance(AuthToken token, string userId, string identifier, Models.SyntyAppearance appearance);

        bool UpdateExperience(SessionToken token, string userId, int[] level, decimal[] experience, Guid? characterId = null);
        bool UpdateResources(SessionToken token, string userId, decimal[] resources);

        bool[] UpdateMany(SessionToken token, PlayerState[] states);

        AddItemResult AddItem(SessionToken token, string userId, Guid itemId);
        AddItemResult CraftItem(SessionToken token, string userId, Guid itemId);

        int GiftItem(SessionToken token, string gifterUserId, string receiverUserId, Guid itemId, int amount);
        int VendorItem(SessionToken token, string userId, Guid itemId, int amount);

        bool EquipItem(SessionToken token, string userId, Guid itemId);
        bool UnequipItem(SessionToken token, string userId, Guid itemId);
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
        int RedeemTokens(SessionToken sessionToken, string userId, int amount, bool exact);
        int GetHighscore(SessionToken sessionToken, Guid characterId, string skillName);
    }
}

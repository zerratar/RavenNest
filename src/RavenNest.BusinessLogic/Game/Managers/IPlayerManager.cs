﻿using System;
using System.Collections.Generic;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerManager
    {
        Player CreatePlayerIfNotExists(string userId, string userName);
        Player CreatePlayer(string userId, string userName);
        Player AddPlayer(SessionToken token, string userId, string userName);
        Player GetPlayer(SessionToken sessionToken, string userId);
        Player GetPlayer(SessionToken sessionToken);
        Player GetPlayer(string userId);
        Player GetGlobalPlayer(Guid userId);
        PlayerExtended GetPlayerExtended(string userId);
        PlayerExtended GetGlobalPlayerExtended(Guid userId);
        bool UpdatePlayerState(SessionToken sessionToken, CharacterStateUpdate update);

        bool UpdateStatistics(SessionToken token, string userId, decimal[] statistics);

        bool UpdateAppearance(SessionToken token, string userId, Models.SyntyAppearance appearance);
        bool UpdateAppearance(string userId, Models.SyntyAppearance appearance);
        bool UpdateAppearance(AuthToken token, string userId, Models.SyntyAppearance appearance);

        bool UpdateExperience(SessionToken token, string userId, decimal[] experience);
        bool UpdateResources(SessionToken token, string userId, decimal[] resources);

        bool[] UpdateMany(SessionToken token, PlayerState[] states);

        AddItemResult AddItem(SessionToken token, string userId, Guid itemId);
        AddItemResult CraftItem(SessionToken token, string userId, Guid itemId);

        int GiftItem(SessionToken token, string gifterUserId, string receiverUserId, Guid itemId, int amount);
        int VendorItem(SessionToken token, string userId, Guid itemId, int amount);

        bool EquipItem(SessionToken token, string userId, Guid itemId);
        bool UnEquipItem(SessionToken token, string userId, Guid itemId);

        bool ToggleHelmet(SessionToken token, string userId);

        ItemCollection GetEquippedItems(SessionToken token, string userId);
        ItemCollection GetAllItems(SessionToken token, string userId);
        IReadOnlyList<Player> GetPlayers();
        IReadOnlyList<PlayerFull> GetFullPlayers();
        void EquipBestItems(DataModels.Character character);
        bool AcquiredUserLock(SessionToken token, DataModels.Character character);
    }
}
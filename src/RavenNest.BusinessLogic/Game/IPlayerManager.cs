using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerManager
    {
        Task<Player> CreatePlayerIfNotExistsAsync(string userId, string userName);
        Task<Player> CreatePlayerAsync(string userId, string userName);
        Task<Player> AddPlayerAsync(SessionToken token, string userId, string userName);
        Task<Player> GetPlayerAsync(SessionToken sessionToken, string userId);
        Task<Player> GetPlayerAsync(SessionToken sessionToken);

        Task<Player> GetGlobalPlayerAsync(string userId);
        Task<Player> GetGlobalPlayerAsync(Guid userId);
        Task<bool> UpdatePlayerStateAsync(SessionToken sessionToken, CharacterStateUpdate update);
        Task<bool> KickPlayerAsync(SessionToken token, string userId);

        Task<bool> UpdateStatisticsAsync(SessionToken token, string userId, decimal[] statistics);
        //Task<bool> UpdateAppearanceAsync(SessionToken token, string userId, int[] appearance);
        //Task<bool> UpdateAppearanceAsync(string userId, int[] appearance);

        Task<bool> UpdateSyntyAppearanceAsync(SessionToken token, string userId, Models.SyntyAppearance appearance);

        Task<bool> UpdateExperienceAsync(SessionToken token, string userId, decimal[] experience);
        Task<bool> UpdateResourcesAsync(SessionToken token, string userId, decimal[] resources);

        Task<bool[]> UpdateManyAsync(SessionToken token, PlayerState[] states);

        Task<AddItemResult> AddItemAsync(SessionToken token, string userId, Guid itemId);
        Task<bool> GiftItemAsync(SessionToken token, string gifterUserId, string receiverUserId, Guid itemId);
        Task<bool> GiftResourcesAsync(SessionToken token, string giftUserId, string receiverUserId, string resource, long amount);

        Task<bool> EquipItemAsync(SessionToken token, string userId, Guid itemId);
        Task<bool> UnEquipItemAsync(SessionToken token, string userId, Guid itemId);

        Task<ItemCollection> GetEquippedItemsAsync(SessionToken token, string userId);
        Task<ItemCollection> GetAllItemsAsync(SessionToken token, string userId);
        Task<IReadOnlyList<Player>> GetPlayersAsync();
    }
}
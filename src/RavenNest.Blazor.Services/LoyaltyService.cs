﻿using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace RavenNest.Blazor.Services
{
    public class LoyaltyService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly PlayerManager playerManager;

        public LoyaltyService(
            GameData gameData,
            IAuthManager authManager,
            PlayerManager playerManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.authManager = authManager;
            this.playerManager = playerManager;
        }

        public double GetExperienceForLevel(long level)
        {
            return RavenNest.DataModels.UserLoyalty.GetExpForLevel(level);
        }

        public Task<bool> RedeemRewardAsync(
            Guid characterId,
            DataModels.UserLoyaltyReward reward,
            int amount)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(reward.RewardData) ||
                    !Guid.TryParse(reward.RewardData, out var itemId))
                    return false;

                var character = gameData.GetCharacter(characterId);
                if (character == null) return false;

                var user = gameData.GetUser(character.UserId);
                if (user == null) return false;

                if (amount < 1)
                    return false;

                var cost = (reward.Points ?? 0) * amount;
                var totalPoints = GetTotalLoyaltyPoints(user.Id);
                if (totalPoints < cost)
                    return false;

                var loyalties = gameData.GetUserLoyalties(user.Id);
                long leftToReduct = cost;
                foreach (var l in loyalties)
                {
                    var a = Math.Min(l.Points, leftToReduct);
                    l.Points -= a;
                    leftToReduct -= a;
                    if (leftToReduct <= 0) break;
                }

                // this is still risky, as the value may change from a different thread
                // accessing this value is not thread-safe. therefor this check may fail if
                // the user gets the same amount of loyalty points added as the cost of the reward
                // from a different thread.
                var totalPointsAfter = GetTotalLoyaltyPoints(user.Id);
                if (totalPointsAfter != totalPoints)
                {
                    playerManager.AddItem(characterId, itemId, amount);
                    return true;
                }

                return false;
            });
        }


        public Task<bool> RedeemRewardAsync(
            Guid streamerUserId,
            Guid characterId,
            DataModels.UserLoyaltyReward reward,
            int amount)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(reward.RewardData) ||
                    !Guid.TryParse(reward.RewardData, out var itemId))
                    return false;

                var streamer = gameData.GetUser(streamerUserId);
                if (streamer == null) return false;

                var character = gameData.GetCharacter(characterId);
                if (character == null) return false;

                var user = gameData.GetUser(character.UserId);
                if (user == null) return false;

                if (amount < 1)
                    return false;

                var cost = reward.Points * amount;
                var loyalty = gameData.GetUserLoyalty(user.Id, streamerUserId);
                if (loyalty.Points < cost)
                    return false;

                loyalty.Points -= cost ?? 0;
                playerManager.AddItem(characterId, itemId, amount);
                return true;
            });
        }

        public Task<IReadOnlyList<DataModels.UserLoyaltyReward>> GetLoyaltyRewardsAsync()
        {
            return Task.Run(() =>
            {
                return gameData.GetLoyaltyRewards();
            });
        }

        public Task<StreamerLoyaltyData> GetStreamerLoyaltyDataAsync(Guid userId)
        {
            return Task.Run(() =>
            {
                var session = GetSession();
                if (session == null) return new StreamerLoyaltyData();

                var user = gameData.GetUser(userId);
                if (user == null) return new StreamerLoyaltyData();

                var data = gameData.GetStreamerLoyalties(userId);
                var sessions = gameData.FindSessions(x => x.UserId == user.Id);

                if (sessions.Count == 0) return new StreamerLoyaltyData();

                var startTime = sessions.Min(x => x.Started);
                var stopTime = sessions.Max(x => x.Started);

                var stoppedSessions = sessions.AsList(x => x.Stopped != null);
                if (stoppedSessions.Count > 0)
                {
                    var lastStoppedSessionTime = stoppedSessions.Max(x => x.Stopped.Value);
                    if (lastStoppedSessionTime > stopTime)
                    {
                        stopTime = lastStoppedSessionTime;
                    }
                }

                var totalBitsCheered = 0L;
                var totalSubsGifted = 0L;
                var totalPlayerTime = TimeSpan.Zero;
                var totalSubscribers = 0;
                var loyalties = new List<PlayerLoyalty>();
                foreach (var d in data)
                {
                    var u = gameData.GetUser(d.UserId);
                    if (u == null || string.IsNullOrEmpty(u.UserName))
                        continue;

                    totalSubsGifted += d.GiftedSubs;
                    totalBitsCheered += d.CheeredBits;
                    if (d.IsSubscriber)
                        ++totalSubscribers;
                    if (TimeSpan.TryParse(d.Playtime, out var val))
                    {
                        totalPlayerTime += val;
                    }

                    var totalPlayTime = TimeSpan.Zero;
                    if (d.Playtime != null)
                        TimeSpan.TryParse(d.Playtime, out totalPlayTime);

                    var twitch = gameData.GetUserAccess(d.UserId, "twitch");

                    loyalties.Add(new PlayerLoyalty
                    {
                        TwitchUserId = twitch?.PlatformId,
                        UserName = u.UserName,
                        DisplayName = Utility.SanitizeUserName(u.DisplayName, u.UserName),
                        CheeredBits = d.CheeredBits,
                        Experience = d.Experience,
                        GiftedSubs = d.GiftedSubs,
                        IsModerator = d.IsModerator,
                        IsSubscriber = d.IsSubscriber,
                        IsVip = d.IsVip,
                        Level = d.Level,
                        Points = d.Points,
                        RankId = d.RankId,
                        TotalPlayTime = totalPlayTime
                    });
                }

                return new StreamerLoyaltyData
                {
                    SessionCount = sessions.Count,
                    TotalPlayerTime = totalPlayerTime,
                    TotalStreamTime = (stopTime - startTime),
                    FirstStream = startTime,
                    TotalBitsCheered = totalBitsCheered,
                    TotalSubsGifted = totalSubsGifted,
                    TotalSubscribers = totalSubscribers,
                    UserLoyalties = loyalties
                        .OrderByDescending(x => x.Level)
                        .ThenByDescending(x => x.TotalPlayTime)
                        .ToList()
                };
            });
        }

        public Task<UserLoyaltyData> GetUserLoyaltyDataAsync(Guid userId)
        {
            return Task.Run(() =>
            {
                var session = GetSession();
                if (session == null) return new UserLoyaltyData();

                var user = gameData.GetUser(userId);
                if (user == null) return new UserLoyaltyData();

                var loyalties = gameData.GetUserLoyalties(userId);
                var result = new List<StreamerLoyalty>();

                foreach (var l in loyalties)
                {
                    var streamer = gameData.GetUser(l.StreamerUserId);
                    if (streamer == null)
                        continue;

                    var u = gameData.GetUser(l.UserId);
                    if (u == null)
                        continue;

                    if (streamer.Id == user.Id)
                        continue; // ignore ourselves. So we as the streamer can't redeem on our own stream.

                    TimeSpan.TryParse(l.Playtime, out var totalPlayTime);

                    var twitch = gameData.GetUserAccess(l.StreamerUserId, "twitch");

                    result.Add(new StreamerLoyalty
                    {
                        GiftedSubs = l.GiftedSubs,
                        CheeredBits = l.CheeredBits,
                        Experience = l.Experience,
                        IsModerator = l.IsModerator,
                        IsSubscriber = l.IsSubscriber,
                        IsVip = l.IsVip,
                        RankId = l.RankId,
                        Level = l.Level,
                        Points = l.Points,
                        StreamerUserName = Utility.SanitizeUserName(streamer.UserName),
                        StreamerUserId = l.StreamerUserId,
                        StreamerDisplayName = Utility.SanitizeUserName(streamer.DisplayName, streamer.UserName),
                        StreamerTwitchUserId = twitch?.PlatformId,
                        TotalPlayTime = totalPlayTime
                    });
                }

                return new UserLoyaltyData
                {
                    Loyalties = result
                };
            });
        }

        public Task<bool> IsStreamerAsync()
        {
            return Task.Run(() =>
            {
                var session = GetSession();
                if (session == null) return false;

                var user = gameData.GetUser(session.UserId);
                if (user == null) return false;

                return gameData.FindSession(x => x.UserId == user.Id) != null;
            });
        }

        public long GetTotalLoyaltyPoints(Guid userId)
        {
            var totalPoints = 0l;
            var loyalties = gameData.GetUserLoyalties(userId);
            foreach (var l in loyalties)
            {
                if (userId == l.StreamerUserId)
                    continue;

                totalPoints += l.Points;
            }
            return totalPoints;
        }

    }

    public class PlayerLoyalty
    {
        public string UserName { get; set; }
        public string TwitchUserId { get; set; }
        public string DisplayName { get; set; }
        public Guid? RankId { get; set; }
        public long Level { get; set; }
        public double Experience { get; set; }
        public long GiftedSubs { get; set; }
        public long CheeredBits { get; set; }
        public long Points { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsModerator { get; set; }
        public bool IsVip { get; set; }
        public TimeSpan TotalPlayTime { get; set; }
    }


    public class StreamerLoyaltyData
    {
        public int SessionCount { get; set; }
        public TimeSpan TotalStreamTime { get; set; }
        public TimeSpan TotalPlayerTime { get; set; }
        public DateTime FirstStream { get; set; }
        public long TotalBitsCheered { get; set; }
        public long TotalSubsGifted { get; set; }
        public int TotalSubscribers { get; set; }

        public IReadOnlyList<PlayerLoyalty> UserLoyalties { get; set; }
    }

    public class StreamerLoyalty
    {
        public Guid StreamerUserId { get; set; }
        public string StreamerTwitchUserId { get; set; }
        public string StreamerDisplayName { get; set; }
        public string StreamerUserName { get; set; }

        public string Name => StreamerDisplayName ?? StreamerUserName ?? StreamerTwitchUserId;

        public Guid? RankId { get; set; }
        public long Level { get; set; }
        public double Experience { get; set; }
        public long GiftedSubs { get; set; }
        public long CheeredBits { get; set; }
        public long Points { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsModerator { get; set; }
        public bool IsVip { get; set; }
        public TimeSpan TotalPlayTime { get; set; }
    }
    public class UserLoyaltyData
    {
        public IReadOnlyList<StreamerLoyalty> Loyalties { get; set; }
    }
}

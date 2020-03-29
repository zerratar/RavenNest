using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Game
{
    public class AdminManager : IAdminManager
    {
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;

        public AdminManager(
            IPlayerManager playerManager,
            IGameData gameData)
        {
            this.playerManager = playerManager;
            this.gameData = gameData;
        }

        public PagedPlayerCollection GetPlayersPaged(int offset, int size, string sortOrder, string query)
        {
            var allPlayers = playerManager.GetPlayers();

            allPlayers = FilterByQuery(query, allPlayers);
            allPlayers = OrderBy(sortOrder, allPlayers);

            return new PagedPlayerCollection()
            {
                TotalSize = allPlayers.Count,
                Items = allPlayers.Skip(offset).Take(size).ToList()
            };
        }

        public PagedSessionCollection GetSessionsPaged(int offset, int size, string sortOrder, string query)
        {
            var activeSessions = gameData.GetActiveSessions();
            var lastActiveRange = DateTime.UtcNow.AddMinutes(-30);
            activeSessions = FilterByQuery(query, activeSessions.Where(x => x.Updated >= lastActiveRange));
            activeSessions = OrderBy(sortOrder, activeSessions);

            return new PagedSessionCollection()
            {
                TotalSize = activeSessions.Count,
                Items = activeSessions.Skip(offset).Take(size)
                    .Select(x => ModelMapper.Map(gameData, x))
                    .ToList()
            };
        }

        public bool UpdatePlayerSkill(string userId, string skill, decimal experience)
        {
            var character = this.gameData.GetCharacterByUserId(userId);
            if (character == null) return false;

            var skills = this.gameData.GetSkills(character.SkillsId);
            if (skills == null) return false;

            var playerSession = gameData.GetSessionByUserId(userId);
            if (playerSession == null) return true;

            SetValue(skills, skill, experience);

            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerExpUpdate,
                playerSession,
                new PlayerExpUpdate
                {
                    UserId = userId,
                    Skill = skill,
                    Experience = experience
                });

            gameData.Add(gameEvent);
            return true;
        }

        public bool UpdatePlayerName(string userid, string name)
        {
            var character = this.gameData.GetCharacterByUserId(userid);
            if (character == null) return false;
            character.Name = name;

            var playerSession = gameData.GetSessionByUserId(userid);
            if (playerSession == null) return true;

            var gameEvent = gameData.CreateSessionEvent(
                GameEventType.PlayerNameUpdate,
                playerSession,
                new PlayerNameUpdate
                {
                    UserId = userid,
                    Name = name
                });

            gameData.Add(gameEvent);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetValue<T, T2>(T obj, string property, T2 value)
        {
            var prop = typeof(T).GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(obj, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyList<T> OrderBy<T>(string sortOrder, IEnumerable<T> allPlayers)
        {
            if (!string.IsNullOrEmpty(sortOrder))
            {
                var sortProperty = typeof(T)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == sortOrder.Substring(1));

                var ascending = sortOrder[0] == '+';
                if (sortProperty != null)
                {
                    allPlayers = (ascending
                        ? allPlayers.OrderBy(x => sortProperty.GetValue(x))
                        : allPlayers.OrderByDescending(x => sortProperty.GetValue(x)));
                }
            }

            return allPlayers.ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IReadOnlyList<T> FilterByQuery<T>(string query, IEnumerable<T> allPlayers)
        {
            if (!string.IsNullOrEmpty(query) && query != "-")
            {
                var stringProps = typeof(T)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.PropertyType == typeof(string));

                allPlayers = allPlayers.Where(x => stringProps.Any(y =>
                {
                    var value = y.GetValue(x)?.ToString();
                    return !string.IsNullOrEmpty(value) && value.Contains(query);
                }));
            }

            return allPlayers.ToList();
        }
    }
}
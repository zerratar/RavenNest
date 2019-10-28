using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Data
{
    public interface IGameData
    {
        GameClient Client { get; }
        User FindUser(string userIdOrUsername);
        User GetUser(Guid userId);
        User FindUser(Func<User, bool> predicate);
        Character FindCharacter(Func<Character, bool> predicate);
        GameSession FindSession(Func<GameSession, bool> predicate);

        IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate);
        IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession);
        User GetUser(string twitchUserId);


        GameSession CreateSession(Guid userId, bool isLocal);
        DataModels.GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data);
        int GetNextGameEventRevision(Guid id);

        void Update(GameSession session);
        void Update(Character character); 
        void Add(GameSession gameSession);
        void Add(DataModels.GameEvent permissionEvent);
        GameSession GetSession(Guid sessionId);
        GameSession GetUserSession(Guid userId);
        IReadOnlyList<DataModels.GameEvent> GetSessionEvents(GameSession gameSession);
        IReadOnlyList<DataModels.Item> GetItems();
        DataModels.Item GetItem(Guid id);
        void Add(DataModels.Item entity);
        void Add(DataModels.CharacterState state);
        void Add(DataModels.SyntyAppearance syntyAppearance);
        void Add(DataModels.Statistics statistics);
        void Add(DataModels.Skills skills);
        void Add(DataModels.Appearance appearance);
        void Add(DataModels.Resources resources);
        void Add(Character character);
        void Update(DataModels.CharacterState state);
        void Add(User user);
        void Add(DataModels.InventoryItem inventoryItem);
        void Update(DataModels.Resources resources);
        void Update(DataModels.InventoryItem itemToEquip);
        void Update(DataModels.Skills skills);
        void Update(DataModels.SyntyAppearance syntyAppearance);
        void Update(DataModels.Appearance appearance);
        void UpdateRange(IEnumerable<DataModels.InventoryItem> weapons);

        IReadOnlyList<DataModels.InventoryItem> GetAllPlayerItems(Guid characterId);
        IReadOnlyList<DataModels.InventoryItem> GetEquippedItems(Guid characterId);
        IReadOnlyList<DataModels.InventoryItem> FindPlayerItems(Guid id, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem FindPlayerItem(Guid id, Func<DataModels.InventoryItem, bool> predicate);
        DataModels.InventoryItem GetInventoryItem(Guid characterId, Guid itemId); 
        DataModels.InventoryItem GetEquippedItem(Guid characterId, Guid itemId);
        void Remove(DataModels.InventoryItem invItem);
    }

    public class GameData : IGameData
    {
        public GameClient Client => throw new NotImplementedException();

        public GameSession CreateSession(Guid userId, bool isLocal)
        {
            //new GameSession
            //{
            //    Id = Guid.NewGuid(),
            //    UserId = userId,
            //    Status = (int)SessionStatus.Active,
            //    Started = DateTime.UtcNow,
            //    Local = isLocal
            //};
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

        public IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate)
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
    }
}

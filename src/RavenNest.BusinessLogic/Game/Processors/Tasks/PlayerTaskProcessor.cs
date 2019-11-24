using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class PlayerTaskProcessor : ITaskProcessor
    {
        protected readonly Random Random = new Random();
        protected static readonly Guid IngotId = Guid.Parse("69A4372F-482F-4AC1-898A-CAFCE809BF4C");
        protected static readonly Guid PlankId = Guid.Parse("EB112F4A-3B17-4DCB-94FE-E9E2C0D9BFAC");
        protected static readonly Guid RuneNuggetId = Guid.Parse("40781EB8-1EBF-4C0C-9A11-6E8033C9953C");
        protected static readonly Guid AdamantiteNuggetId = Guid.Parse("E32A6F17-653C-4AF3-A3A1-D0C6674FE4D5");
        protected static readonly Guid MithrilNuggetId = Guid.Parse("B3411B33-59F6-4443-A70C-6576B6EC74EC");
        protected static readonly Guid SteelNuggetId = Guid.Parse("EF674846-817E-41B7-B378-85E64D2CCF5D");
        protected static readonly Guid IronNuggetId = Guid.Parse("CC61E4A3-B00E-4FD4-9160-16A6466787E6");

        protected void IncrementItemStack(IGameData gameData, GameSession session, Character character, Guid itemId)
        {
            var items = gameData.GetInventoryItems(character.Id, itemId);
            if (items == null || items.Count == 0)
            {
                gameData.Add(CreateInventoryItem(character, itemId));
            }
            else
            {
                ++items.First().Amount;
            }
            var user = gameData.GetUser(character.UserId);
            gameData.Add(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, new ItemAdd
            {
                UserId = user.UserId,
                Amount = 1,
                ItemId = itemId
            }));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DataModels.InventoryItem CreateInventoryItem(Character character, Guid itemId)
        {
            return new DataModels.InventoryItem { Id = Guid.NewGuid(), Amount = 1, CharacterId = character.Id, Equipped = false, ItemId = itemId };
        }

        public abstract void Handle(IGameData gameData, GameSession session, Character character, DataModels.CharacterState state);
    }
}

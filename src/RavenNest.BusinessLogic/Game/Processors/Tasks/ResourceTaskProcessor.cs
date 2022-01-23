using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public static class ItemDropRateSettings
    {
        public static int ResourceGatherInterval = 10;
        public static double DropChanceIncrement = 0.00025;
        public static double InitDropChance = 0.33;
    }

    public abstract class ResourceTaskProcessor : PlayerTaskProcessor
    {
        public static readonly Guid IngotId = Guid.Parse("69A4372F-482F-4AC1-898A-CAFCE809BF4C");
        public static readonly Guid PlankId = Guid.Parse("EB112F4A-3B17-4DCB-94FE-E9E2C0D9BFAC");

        public static int OrePerIngot = 10;
        public static int WoodPerPlank = 10;
        public static readonly IReadOnlyList<ResourceDrop> DroppableResources;

        static ResourceTaskProcessor()
        {
            DroppableResources = new List<ResourceDrop>()
            {
                new ResourceDrop(Guid.Parse("49D53A1E-55F7-4537-9A5B-0560B1C0F465"), "Ethereum", 0.003, 280),
                new ResourceDrop(Guid.Parse("BA6ED0AD-2FE6-46BF-9A99-5528657FF40E"), "Lionite", 0.005, 240),
                new ResourceDrop(Guid.Parse("17c3f9b1-57d6-4219-bbc7-9e929757babf"), "Phantom Core", 0.01, 200),
                new ResourceDrop(Guid.Parse("f9b7e6a3-4e4a-4e4a-b79d-42a3cf2a16c8"), "Abraxas Spirit", 0.02, 170),
                new ResourceDrop(Guid.Parse("0dc620c2-b726-4928-9f1c-fcf61aaa2542"), "Dragon Scale", 0.025, 130),
                new ResourceDrop(Guid.Parse("40781EB8-1EBF-4C0C-9A11-6E8033C9953C"), "Rune Nugget", 0.075, 70),
                new ResourceDrop(Guid.Parse("E32A6F17-653C-4AF3-A3A1-D0C6674FE4D5"), "Adamantite Nugget", 0.1, 50),
                new ResourceDrop(Guid.Parse("FEE5E07E-4397-44A9-9E3A-ED0465CE29FC"), "Gold Nugget", 0.135, 30),
                new ResourceDrop(Guid.Parse("B3411B33-59F6-4443-A70C-6576B6EC74EC"), "Mithril Nugget", 0.135, 30),
                new ResourceDrop(Guid.Parse("F5A6063F-CC99-48BF-BC79-F764CD87373A"), "Ruby", 0.135, 25),
                new ResourceDrop(Guid.Parse("48C94F6C-6119-48A2-88EA-F7649F816DA4"), "Emerald", 0.135, 20),
                new ResourceDrop(Guid.Parse("723A48A0-E3CB-4EBD-9966-EE8323B11DC0"), "Sapphire", 0.15, 10),
                new ResourceDrop(Guid.Parse("EF674846-817E-41B7-B378-85E64D2CCF5D"), "Steel Nugget", 0.185, 10),
                new ResourceDrop(Guid.Parse("CC61E4A3-B00E-4FD4-9160-16A6466787E6"), "Iron Nugget", 0.2, 1),
            };
        }

        protected void UpdateResourceGain(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            Action<DataModels.Resources> onUpdate)
        {

            if (!integrityChecker.VerifyPlayer(session.Id, character.Id, 0))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ItemDropRateSettings.ResourceGatherInterval))
            {
                session.Updated = DateTime.UtcNow;
                var resources = gameData.GetResources(character.ResourcesId);
                var oldWood = resources.Wood;
                var oldWheat = resources.Wheat;
                var oldFish = resources.Fish;
                var oldOre = resources.Ore;
                var oldCoins = resources.Coins;

                state.LastTaskUpdate = DateTime.UtcNow;
                onUpdate?.Invoke(resources);

                if (oldCoins != resources.Coins ||
                    oldWood != resources.Wood ||
                    oldWheat != resources.Wheat ||
                    oldFish != resources.Fish ||
                    oldOre != resources.Ore)
                    UpdateResources(gameData, session, character, resources);
            }
        }

        protected DataModels.Resources GetVillageResources(IGameData gameData, DataModels.GameSession session)
        {
            DataModels.Resources resx = null;
            var village = gameData.GetVillageBySession(session);
            if (village != null)
            {
                resx = gameData.GetResources(village.ResourcesId);
            }
            return resx;
        }

        protected void UpdateResources(
            IGameData gameData,
            DataModels.GameSession session,
            Character character,
            DataModels.Resources resources)
        {
            if (resources == null)
            {
                resources = new DataModels.Resources
                {
                    Id = Guid.NewGuid(),
                };
                gameData.Add(resources);
                character.ResourcesId = resources.Id;
            }

            var user = gameData.GetUser(character.UserId);
            var gameEvent = gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session,
                new ResourceUpdate
                {
                    UserId = user.UserId,
                    FishAmount = resources.Fish,
                    OreAmount = resources.Ore,
                    WheatAmount = resources.Wheat,
                    WoodAmount = resources.Wood,
                    CoinsAmount = resources.Coins
                });

            gameData.Add(gameEvent);
        }
    }

    public class ResourceDrop
    {
        public Guid Id { get; }
        public string Name { get; }
        public double DropChance { get; }
        public int SkillLevel { get; set; }
        public ResourceDrop(Guid id, string name, double dropChance, int skillLevel)
        {
            Id = id;
            Name = name;
            DropChance = dropChance;
            SkillLevel = skillLevel;
        }

        public double GetDropChance(int playerSkillLevel)
        {
            return DropChance + ((playerSkillLevel - SkillLevel) * ItemDropRateSettings.DropChanceIncrement);
        }
    }
}

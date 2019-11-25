﻿using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public abstract class ResourceTaskProcessor : PlayerTaskProcessor
    {
        protected static readonly Guid IngotId = Guid.Parse("69A4372F-482F-4AC1-898A-CAFCE809BF4C");
        protected static readonly Guid PlankId = Guid.Parse("EB112F4A-3B17-4DCB-94FE-E9E2C0D9BFAC");

        protected const int ResourceGatherInterval = 6;
        protected const double DropChanceIncrement = 0.02;

        protected const int OrePerIngot = 10;
        protected const int WoodPerPlank = 10;

        protected static readonly IReadOnlyList<ResourceDrop> DroppableResources;

        static ResourceTaskProcessor()
        {
            DroppableResources = new List<ResourceDrop>()
            {
                new ResourceDrop(Guid.Parse("40781EB8-1EBF-4C0C-9A11-6E8033C9953C"), "Rune Nugget", 0.01, 70),
                new ResourceDrop(Guid.Parse("E32A6F17-653C-4AF3-A3A1-D0C6674FE4D5"), "Adamantite Nugget", 0.05, 50),
                new ResourceDrop(Guid.Parse("B3411B33-59F6-4443-A70C-6576B6EC74EC"), "Mithril Nugget", 0.125, 30),
                new ResourceDrop(Guid.Parse("EF674846-817E-41B7-B378-85E64D2CCF5D"), "Steel Nugget", 0.2, 10),
                new ResourceDrop(Guid.Parse("CC61E4A3-B00E-4FD4-9160-16A6466787E6"), "Iron Nugget", 0.25, 1),

                new ResourceDrop(Guid.Parse("FEE5E07E-4397-44A9-9E3A-ED0465CE29FC"), "Gold Nugget", 0.125, 30),
                new ResourceDrop(Guid.Parse("F5A6063F-CC99-48BF-BC79-F764CD87373A"), "Ruby", 0.125, 25),
                new ResourceDrop(Guid.Parse("48C94F6C-6119-48A2-88EA-F7649F816DA4"), "Emerald", 0.225, 20),
                new ResourceDrop(Guid.Parse("723A48A0-E3CB-4EBD-9966-EE8323B11DC0"), "Sapphire", 0.325, 10),
            };
        }

        protected void UpdateResourceGain(IGameData gameData, GameSession session, Character character, Action<DataModels.Resources> onUpdate)
        {
            var now = DateTime.UtcNow;
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ResourceGatherInterval))
            {
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

        protected void UpdateResources(IGameData gameData, GameSession session, Character character, DataModels.Resources resources)
        {
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

        protected class ResourceDrop
        {
            public Guid Id { get; }
            public string Name { get; }
            public double DropChance { get; }
            public int SkillLevel { get; set; }
            public ResourceDrop(Guid id, string name, double dropChance, int miningLevel)
            {
                Id = id;
                Name = name;
                DropChance = dropChance;
                SkillLevel = miningLevel;
            }

            public double GetDropChance(int playerSkillLevel)
            {
                return (playerSkillLevel - SkillLevel) * DropChanceIncrement;
            }
        }
    }
}

﻿using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class QuestProcessor : PlayerTaskProcessor
    {
        public override void Process(ILogger logger, GameData gameData, PlayerInventory inventory, GameSession session, Character character, CharacterState state)
        {
            if (character == null || state == null)
            {
                StreamerProcess(logger, gameData, session, inventory);
                return;
            }

        }

        private void StreamerProcess(ILogger logger, GameData gameData, GameSession session, PlayerInventory inventory)
        {

        }
    }
}

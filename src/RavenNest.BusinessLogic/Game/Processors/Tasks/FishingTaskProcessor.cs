using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FishingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Fishing));

        /// <summary>The island a player at anchor on their own boat is on, as far as the server knows (Island.Sea).</summary>
        public const string AtSea = "Sea";

        /// <summary>Own boats' guests, for the owner's cut of their fish (set by BoatManager when it is made).</summary>
        internal static BoatManager Boats;

        /// <summary>Fish only caught at sea (decided 2026-10-26); never from an island's fishing spots.</summary>
        public static readonly System.Collections.Generic.HashSet<string> SeaOnly = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Raw Mackerel", "Raw Tuna", "Raw Marlin", "Raw Anglerfish", "Raw Giant Squid", "Raw Sea Serpent", "Message in a Bottle",
        };

        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
            GameSession session,
            User user,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                if (state.InOnsen.GetValueOrDefault() || state.InDungeon.GetValueOrDefault() || state.InRaid || string.IsNullOrEmpty(state.Island))
                    return;

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                // out at sea on their own boat: no island to open or require a level, and the sea's own fish bite
                if (string.Equals(state.Island, AtSea, StringComparison.OrdinalIgnoreCase))
                {
                    var seaLevel = skills.FishingLevel + inventory.GetFishingBonus();
                    var village = GetVillageResources(gameData, session);
                    if (village != null) ++village.Fish;
                    session.Updated = DateTime.UtcNow;
                    QuestTracker.Gathered(character, RavenNest.Models.Skill.Fishing);
                    // a guest aboard someone's boat: their catch may bring the owner one too (the captain's cut)
                    if (Drops.TryDropItem(this, logger, gameData, inventory, session, character, seaLevel, state.TaskArgument))
                        Boats?.GuestCaught(this, logger, session, character);
                    return;
                }

                if (!TryGetIsland(state.Island, out var island))
                {
                    // island not found, this should not happen.
                    var sessionState = gameData.GetSessionState(session.Id);
                    logger.LogError($"[{sessionState?.ClientVersion}] <Fishing> Island not found: '{state.Island}' for user {user.UserName} ({user.Id}) in session {session.Id}.");
                    return;
                }

                var level = skills.FishingLevel + inventory.GetFishingBonus();
                if (!IslandOpen(gameData, session, island)) return; // the mangrove island, where it is not unlocked
                if (islandLevelRequirements[island][RavenNest.Models.Skill.Fishing] > level)
                    return;

                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Fish;
                }

                session.Updated = DateTime.UtcNow;


                QuestTracker.Gathered(character, RavenNest.Models.Skill.Fishing);
                Drops.TryDropItem(this, logger, gameData, inventory, session, character, level, state.TaskArgument, d => !SeaOnly.Contains(d.Name ?? ""));
            });
        }
    }
}

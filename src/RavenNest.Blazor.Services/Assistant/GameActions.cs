using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.Blazor.Services.Assistant
{
    /// <summary>
    ///     Telling the running game to do something with a character.
    /// </summary>
    /// <remarks>
    ///     These are the same calls the Twitch overlay extension makes, through the same
    ///     PlayerManager methods, so this is not a new capability: it is the extension's buttons
    ///     reachable from somewhere else. What the extension gets from a broadcaster id, this gets
    ///     from the session the character is already in.
    ///
    ///     <para>
    ///     Every one of them enqueues an event for the game client rather than changing anything
    ///     here. So a character that is not in a running stream has nowhere to send it, and that is
    ///     said plainly rather than reported as success: the honest failure of this whole surface is
    ///     silence, where a message is sent to nobody and the player waits for something that will
    ///     never happen.
    ///     </para>
    ///
    ///     <para>
    ///     The character is always resolved from the signed in user before anything is sent, so
    ///     nothing here can be pointed at somebody else's character.
    ///     </para>
    /// </remarks>
    public class GameActions
    {
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;

        public GameActions(GameData gameData, PlayerManager playerManager)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
        }

        /// <summary>
        ///     The tasks a character can be set to, and what a player might call them.
        /// </summary>
        /// <remarks>
        ///     A closed list because the game takes a string and would accept anything, and a typo
        ///     would be enqueued, delivered, ignored, and reported to the player as done. The
        ///     synonyms are here because somebody says "melee" or "fishing" rather than the task's
        ///     internal name.
        /// </remarks>
        private static readonly Dictionary<string, (string Task, string Argument)> Tasks =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["attack"] = ("Fighting", "Attack"),
                ["defense"] = ("Fighting", "Defense"),
                ["defence"] = ("Fighting", "Defense"),
                ["strength"] = ("Fighting", "Strength"),
                ["health"] = ("Fighting", "Health"),
                ["all"] = ("Fighting", "All"),
                ["melee"] = ("Fighting", "All"),
                ["magic"] = ("Fighting", "Magic"),
                ["ranged"] = ("Fighting", "Ranged"),
                ["range"] = ("Fighting", "Ranged"),
                ["healing"] = ("Fighting", "Healing"),
                ["heal"] = ("Fighting", "Healing"),

                ["mining"] = ("Mining", null),
                ["fishing"] = ("Fishing", null),
                ["woodcutting"] = ("Woodcutting", null),
                ["chopping"] = ("Woodcutting", null),
                ["farming"] = ("Farming", null),
                ["crafting"] = ("Crafting", null),
                ["cooking"] = ("Cooking", null),
                ["alchemy"] = ("Alchemy", null),
                ["gathering"] = ("Gathering", null),
            };

        public static IEnumerable<string> TrainableNames => Tasks.Keys;

        /// <summary>
        ///     The islands, as the game names them.
        /// </summary>
        private static readonly string[] Islands =
        {
            "Home", "Away", "Ironhill", "Kyo", "Heim", "Atria", "Eldara"
        };

        public static IEnumerable<string> IslandNames => Islands;

        /// <summary>
        ///     What a character can be told to do, or why it cannot be told anything.
        /// </summary>
        public GameActionResult Run(Guid characterId, Guid userId, GameActionKind kind, string argument)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return GameActionResult.Failed("That character no longer exists.");

            // Resolved against the signed in user, not taken on trust.
            if (character.UserId != userId)
            {
                return GameActionResult.Failed("That is not your character.");
            }

            var session = gameData.GetSessionByCharacterId(characterId);
            if (session == null)
            {
                return GameActionResult.Failed(
                    character.Name + " is not in a running stream, so there is nothing to tell. " +
                    "These only work while a streamer has the game open and the character has joined.");
            }

            switch (kind)
            {
                case GameActionKind.Train:
                    {
                        if (string.IsNullOrWhiteSpace(argument) || !Tasks.TryGetValue(argument.Trim(), out var task))
                        {
                            return GameActionResult.Failed(
                                "There is nothing called '" + argument + "' to train. It can be one of: " +
                                string.Join(", ", Tasks.Keys.Take(12)) + ", and others.");
                        }

                        playerManager.SendPlayerTaskToGame(session, character, task.Task, task.Argument);
                        return GameActionResult.Sent(character.Name + " has been told to train " + argument.Trim() + ".");
                    }

                case GameActionKind.Travel:
                    {
                        var island = Islands.FirstOrDefault(x =>
                            string.Equals(x, (argument ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

                        if (island == null)
                        {
                            return GameActionResult.Failed(
                                "There is no island called '" + argument + "'. They are: " +
                                string.Join(", ", Islands) + ".");
                        }

                        playerManager.SendPlayerTravelToGame(session, character, island);
                        return GameActionResult.Sent(character.Name + " is sailing to " + island + ".");
                    }

                case GameActionKind.Rest:
                    playerManager.SendPlayerEnterOnsenToGame(session, character);
                    return GameActionResult.Sent(character.Name + " has been sent to rest in the onsen.");

                case GameActionKind.StopResting:
                    playerManager.SendPlayerExitOnsenToGame(session, character);
                    return GameActionResult.Sent(character.Name + " has been told to leave the onsen.");

                case GameActionKind.JoinRaid:
                    playerManager.SendRaidJoinToGame(session, character);
                    return GameActionResult.Sent(character.Name + " has been told to join the raid. " +
                                                 "If there is no raid running, nothing will happen.");

                case GameActionKind.JoinDungeon:
                    playerManager.SendDungeonJoinToGame(session, character);
                    return GameActionResult.Sent(character.Name + " has been told to join the dungeon. " +
                                                 "If there is no dungeon running, nothing will happen.");

                default:
                    return GameActionResult.Failed("That is not something that can be done.");
            }
        }

        /// <summary>
        ///     What the person is asked to agree to, written from the arguments rather than from
        ///     anything the assistant said about them.
        /// </summary>
        public string Describe(Guid characterId, GameActionKind kind, string argument)
        {
            var name = gameData.GetCharacter(characterId)?.Name ?? "your character";

            return kind switch
            {
                GameActionKind.Train => "Change what " + name + " is training to " + (argument ?? "?") + ".",
                GameActionKind.Travel => "Send " + name + " to " + (argument ?? "?") + ".",
                GameActionKind.Rest => "Send " + name + " to rest in the onsen. It stops training while there.",
                GameActionKind.StopResting => "Take " + name + " out of the onsen.",
                GameActionKind.JoinRaid => "Have " + name + " join the raid.",
                GameActionKind.JoinDungeon => "Have " + name + " join the dungeon.",
                _ => "Do something with " + name + "."
            };
        }
    }

    public enum GameActionKind
    {
        Train,
        Travel,
        Rest,
        StopResting,
        JoinRaid,
        JoinDungeon
    }

    public sealed class GameActionResult
    {
        private GameActionResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public bool Ok { get; }

        public string Message { get; }

        /// <summary>
        ///     Sent to the game, which is not the same as done.
        /// </summary>
        /// <remarks>
        ///     Everything here enqueues an event for the game client to act on. The wording says
        ///     "has been told to" rather than "is now", because the client decides what actually
        ///     happens and reporting an instruction as an outcome is how somebody ends up staring
        ///     at a character that did not move.
        /// </remarks>
        public static GameActionResult Sent(string message) => new GameActionResult(true, message);

        public static GameActionResult Failed(string message) => new GameActionResult(false, message);
    }
}

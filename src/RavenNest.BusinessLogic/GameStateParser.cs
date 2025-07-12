using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using System;

namespace RavenNest.BusinessLogic
{
    public class GameStateParser
    {
        public static GameStateFile Parse(string gsPath, string xpPath, string psPath)
        {
            var result = new GameStateFile();

            if (System.IO.File.Exists(gsPath))
            {
                int pos = 0;
                var gsData = System.IO.File.ReadAllBytes(gsPath).AsSpan();
                result.GameState = DeltaServer.ParseGameState(gsData, ref pos);
            }

            if (System.IO.File.Exists(xpPath))
            {
                int pos = 0;
                var xpData = System.IO.File.ReadAllBytes(xpPath).AsSpan();
                result.ExperienceState = DeltaServer.ParseExperience(xpData, ref pos);
            }

            if (System.IO.File.Exists(psPath))
            {
                int pos = 0;
                var psData = System.IO.File.ReadAllBytes(psPath).AsSpan();
                result.PlayerState = DeltaServer.ParsePlayerState(psData, ref pos);
            }

            return result;
        }

        public static GameStateFile Parse(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new System.IO.FileNotFoundException("Game state file not found.", filePath);
            }

            // Read the file and parse its contents
            var fileData = System.IO.File.ReadAllBytes(filePath).AsSpan();

            return Parse(fileData);
        }

        public static GameStateFile Parse(ReadOnlySpan<byte> fileData)
        {
            if (fileData.Length == 0)
            {
                throw new ArgumentException("File data cannot be null or empty.", nameof(fileData));
            }
            // Implement the logic to parse the game state file from byte array
            // This is a placeholder implementation
            var pos = 0;

            // 1. Game State
            var gs = DeltaServer.ParseGameState(fileData, ref pos);

            // 2. Experience State
            var xp = DeltaServer.ParseExperience(fileData, ref pos);

            // 3. Player State
            var ps = DeltaServer.ParsePlayerState(fileData, ref pos);

            return new GameStateFile
            {
                PlayerState = ps,
                ExperienceState = xp,
                GameState = gs
            };
        }
    }
}

using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     The boat designer (/boats; the game's docs/own-boats.md, "Customizing boats", phase 4): the signed-in
    ///     player's boats on all their characters and their looks. The same rules as !boat in the game: only parts
    ///     the player owns, nothing bought.
    /// </summary>
    public class BoatService : RavenNestService
    {
        private readonly BoatManager boats;
        private readonly PlayerManager players;
        private readonly GameData gameData;

        public BoatService(
            BoatManager boats,
            PlayerManager players,
            GameData gameData,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.boats = boats;
            this.players = players;
            this.gameData = gameData;
        }

        /// <summary>
        /// Equips a boat on its character (the one the player sails; any other boat they had equipped is put away), or
        /// puts it away so they take the ferry. The same as !equip and !unequip in chat. Null when it worked, else why not.
        /// </summary>
        public string SetEquipped(Guid characterId, Guid inventoryItemId, bool equip)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return "notsignedin";
            var character = gameData.GetCharacter(characterId);
            if (character == null || character.UserId != session.UserId) return "nocharacter";
            var stack = gameData.GetInventoryItem(inventoryItemId);
            if (stack == null || stack.CharacterId != characterId) return "noboat";
            var item = new RavenNest.Models.InventoryItem { Id = inventoryItemId, ItemId = stack.ItemId };
            var ok = equip ? players.EquipItem(characterId, item) : players.UnequipItem(characterId, item);
            return ok ? null : "failed";
        }

        /// <summary>What the page shows under the boat: speed range (m/s, by Sailing level), seats and the Sailing level to build it.</summary>
        public static BoatFacts Facts(string boat) => new BoatFacts
        {
            Seats = BoatManager.Seats(boat),
            Sailing = BoatManager.SailingNeeded(boat),
            // the game's SailingSpeeds: the slowest at Sailing 1, the fastest it reaches with level (capped)
            SpeedMin = boat switch { "Galleon" => 28, "Brigantine" => 24, "Sloop" => 20, "Fishing Boat" => 16, _ => 12 },
            SpeedMax = boat switch { "Galleon" => 70, "Brigantine" => 60, "Sloop" => 50, "Fishing Boat" => 36, _ => 24 },
        };

        /// <summary>Every boat the player's characters carry, or null when nobody is signed in.</summary>
        public IReadOnlyList<BoatLookResult> GetBoats()
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return null;
            return boats.GetBoats(session.UserId);
        }

        public BoatLookResult SetLook(Guid characterId, Guid inventoryItemId, string slot, int variant)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return new BoatLookResult { Error = "notsignedin" };
            return boats.SetLook(session.UserId, characterId, inventoryItemId, slot, variant);
        }
    }

    public class BoatFacts
    {
        public int Seats { get; set; }
        public int Sailing { get; set; }
        public int SpeedMin { get; set; }
        public int SpeedMax { get; set; }
    }
}

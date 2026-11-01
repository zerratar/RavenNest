using Microsoft.AspNetCore.Http;
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

        public BoatService(
            BoatManager boats,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.boats = boats;
        }

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
}

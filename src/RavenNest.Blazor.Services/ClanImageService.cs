using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.ClanImages;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     The clan page's Look tab: the owner's logo and cape images and the cape shapes. Uploads go
    ///     through api/clan/images/{kind}, as the editor sends the finished image from the browser.
    /// </summary>
    public class ClanImageService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly ClanImageManager images;

        public ClanImageService(
            GameData gameData,
            ClanImageManager images,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.images = images;
        }

        /// <summary>What the signed-in owner's clan looks like now, or null when they own no clan.</summary>
        public ClanLookState GetState()
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return null;
            var user = gameData.GetUser(session.UserId);
            var clan = user != null ? gameData.GetClanByOwner(user.Id) : null;
            if (clan == null) return null;

            var twitch = gameData.GetUserAccess(user.Id, "twitch");
            var all = gameData.GetClanImagesByClan(clan.Id);
            return new ClanLookState
            {
                ClanId = clan.Id,
                ClanName = clan.Name,
                OwnerTwitchId = twitch?.PlatformId,
                CanCustomize = images.CanCustomize(user),
                RequireReview = user.IsAdmin != true && (images.RequireReview || user.ClanImagesNeedReview == true),
                CapeStyle = clan.CapeStyle,
                Logo = images.GetCurrent(clan.Id, ClanImageKind.Logo, ClanImageState.Live),
                LogoWaiting = images.GetCurrent(clan.Id, ClanImageKind.Logo, ClanImageState.Waiting),
                Cape = images.GetCurrent(clan.Id, ClanImageKind.Cape, ClanImageState.Live),
                CapeWaiting = images.GetCurrent(clan.Id, ClanImageKind.Cape, ClanImageState.Waiting),
                LogoTurnedDown = LastTurnedDown(all, ClanImageKind.Logo),
                CapeTurnedDown = LastTurnedDown(all, ClanImageKind.Cape),
                Roles = gameData.GetClanRoles(clan.Id)
                    .OrderByDescending(x => x.Level)
                    .Select(x => new ClanLookRole { Id = x.Id, Name = x.Name, Level = x.Level, CapeStyle = x.CapeStyle })
                    .ToList(),
            };
        }

        public ClanImageResult SetCapeStyle(int style, IReadOnlyDictionary<Guid, int?> roleStyles)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return new ClanImageResult { Error = "You are not signed in." };
            return images.SetCapeStyle(session.UserId, style, roleStyles);
        }

        public ClanImageResult UseDefault(ClanImageKind kind)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return new ClanImageResult { Error = "You are not signed in." };
            return images.UseDefault(session.UserId, kind);
        }

        // the newest upload of a kind was rejected or blocked: the owner is told it was not used
        private static bool LastTurnedDown(IReadOnlyList<DataModels.ClanImage> all, ClanImageKind kind)
        {
            var last = all.Where(x => x.Kind == (int)kind).OrderByDescending(x => x.Created).FirstOrDefault();
            return last != null && (last.State == (int)ClanImageState.Rejected || last.State == (int)ClanImageState.Blocked);
        }
    }

    public class ClanLookState
    {
        public Guid ClanId { get; set; }
        public string ClanName { get; set; }
        public string OwnerTwitchId { get; set; }
        public bool CanCustomize { get; set; }
        public bool RequireReview { get; set; }
        public int CapeStyle { get; set; }
        public ClanImageInfo Logo { get; set; }
        public ClanImageInfo LogoWaiting { get; set; }
        public ClanImageInfo Cape { get; set; }
        public ClanImageInfo CapeWaiting { get; set; }
        public bool LogoTurnedDown { get; set; }
        public bool CapeTurnedDown { get; set; }
        public List<ClanLookRole> Roles { get; set; } = new List<ClanLookRole>();
    }

    public class ClanLookRole
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int? CapeStyle { get; set; }
    }
}

using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Settings;
using RavenNest.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace RavenNest.BusinessLogic.Game.ClanImages
{
    /// <summary>
    ///     Clan owners' own logo and cape images: uploads, review, blocking and the cape shapes.
    ///     See docs/clan-images-design.md in the game repository.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The website's editor sends the image already cropped, rotated and at its final size, so this
    ///     only checks it and re-encodes it. Re-encoding drops metadata and anything hidden after the image
    ///     data, and means what is served is always a PNG made here.
    ///     </para>
    ///     <para>
    ///     An upload is used at once unless review is required, by the server setting for everyone or by
    ///     the owner's own flag, which blocking one of their images sets. A waiting upload leaves the
    ///     clan's current image in place until an admin decides.
    ///     </para>
    /// </remarks>
    public class ClanImageManager
    {
        public const int LogoSize = 512;
        public const int CapeWidth = 512;
        public const int CapeHeight = 1024;
        public const int MinSize = 128;
        public const int MaxUploadBytes = 4 * 1024 * 1024;
        public const int StyleCount = 4;
        private static readonly TimeSpan UploadInterval = TimeSpan.FromSeconds(60);

        // what the cape shows where the image is transparent: the shader has no transparency
        private static readonly SKColor CapeCloth = new SKColor(0x2B, 0x2A, 0x33);

        public static readonly string FolderPath = Path.Combine(FolderPaths.GeneratedDataPath, "clan-images");

        private readonly GameData gameData;
        private readonly IServerSettingsProvider settings;
        private readonly ILogger<ClanImageManager> logger;
        private readonly object mutex = new object();

        public ClanImageManager(GameData gameData, IServerSettingsProvider settings, ILogger<ClanImageManager> logger)
        {
            this.gameData = gameData;
            this.settings = settings;
            this.logger = logger;
        }

        public bool RequireReview => settings.GetToggle(ServerSettingsRegistry.ClanImagesRequireReview);

        #region The owner

        public ClanImageResult Upload(Guid userId, ClanImageKind kind, byte[] data)
        {
            var user = gameData.GetUser(userId);
            var clan = user != null ? gameData.GetClanByOwner(user.Id) : null;
            if (clan == null) return Fail("Only a clan owner can change the clan's images.");
            if (!CanCustomize(user)) return Fail("Your own clan images are a patron feature. Your current images stay.");
            if (data == null || data.Length == 0) return Fail("No image was sent.");
            if (data.Length > MaxUploadBytes) return Fail("That image is too large.");

            lock (mutex)
            {
                var last = gameData.GetClanImagesByClan(clan.Id).OrderByDescending(x => x.Created).FirstOrDefault();
                if (last != null && DateTime.UtcNow - last.Created < UploadInterval)
                    return Fail("Please wait a minute between uploads.");

                byte[] png;
                int width, height;
                try
                {
                    png = Reencode(kind, data, out width, out height);
                }
                catch (InvalidDataException exc)
                {
                    return Fail(exc.Message);
                }
                catch (Exception exc)
                {
                    logger.LogError("Could not read a clan image from " + user.UserName + ": " + exc);
                    return Fail("That image could not be read.");
                }

                var hash = Convert.ToHexString(SHA256.HashData(png));
                var current = Current(clan.Id, kind);
                if (current != null && current.Hash == hash && (ClanImageState)current.State is ClanImageState.Live or ClanImageState.Waiting)
                    return new ClanImageResult { Ok = true, Waiting = (ClanImageState)current.State == ClanImageState.Waiting, ImageId = current.Id };

                var waits = NeedsReview(user);
                var image = new DataModels.ClanImage
                {
                    Id = Guid.NewGuid(),
                    ClanId = clan.Id,
                    UserId = user.Id,
                    Kind = (int)kind,
                    State = (int)(waits ? ClanImageState.Waiting : ClanImageState.Live),
                    Created = DateTime.UtcNow,
                    Hash = hash,
                    Width = width,
                    Height = height,
                    Size = png.Length,
                };

                Directory.CreateDirectory(FolderPath);
                File.WriteAllBytes(PathOf(image.Id), png);

                // one waiting image per kind: a newer upload takes the place of the one still waiting
                foreach (var old in gameData.GetClanImagesByClan(clan.Id).Where(x => x.Kind == (int)kind && x.State == (int)ClanImageState.Waiting))
                    old.State = (int)ClanImageState.Removed;

                gameData.Add(image);
                if (!waits) GoLive(clan, image);
                return new ClanImageResult { Ok = true, Waiting = waits, ImageId = image.Id };
            }
        }

        /// <summary>Back to the default: the owner's Twitch picture, or the logo on a cloth colour for the cape.</summary>
        public ClanImageResult UseDefault(Guid userId, ClanImageKind kind)
        {
            var clan = gameData.GetClanByOwner(userId);
            if (clan == null) return Fail("Only a clan owner can change the clan's images.");

            lock (mutex)
            {
                foreach (var image in gameData.GetClanImagesByClan(clan.Id).Where(x => x.Kind == (int)kind))
                {
                    if ((ClanImageState)image.State is ClanImageState.Live or ClanImageState.Waiting)
                        image.State = (int)ClanImageState.Removed;
                }

                SetImageId(clan, kind, null);
            }

            SendLookChanged(clan);
            return new ClanImageResult { Ok = true };
        }

        /// <summary>The clan's cape shape, and the roles that wear another one (an empty style is the clan's).</summary>
        public ClanImageResult SetCapeStyle(Guid userId, int style, IReadOnlyDictionary<Guid, int?> roleStyles)
        {
            var clan = gameData.GetClanByOwner(userId);
            if (clan == null) return Fail("Only a clan owner can change the clan's capes.");
            if (style < 0 || style >= StyleCount) return Fail("Unknown cape shape.");

            clan.CapeStyle = style;
            if (roleStyles != null)
            {
                var roles = gameData.GetClanRoles(clan.Id);
                foreach (var (roleId, roleStyle) in roleStyles)
                {
                    var role = roles.FirstOrDefault(x => x.Id == roleId);
                    if (role == null) continue;
                    role.CapeStyle = roleStyle is >= 0 and < StyleCount && roleStyle != style ? roleStyle : null;
                }
            }

            SendLookChanged(clan);
            return new ClanImageResult { Ok = true };
        }

        /// <summary>Whether this user may upload their own clan images: patrons, and the staff.</summary>
        public bool CanCustomize(DataModels.User user)
            => user != null && ((user.PatreonTier ?? 0) > 0 || user.IsAdmin == true || user.IsModerator == true);

        /// <summary>The clan's current live or waiting image of a kind, for the clan page.</summary>
        public ClanImageInfo GetCurrent(Guid clanId, ClanImageKind kind, ClanImageState state)
        {
            var image = gameData.GetClanImagesByClan(clanId)
                .Where(x => x.Kind == (int)kind && x.State == (int)state)
                .OrderByDescending(x => x.Created).FirstOrDefault();
            return Map(image);
        }

        #endregion

        #region What everyone sees

        public ClanLook GetLook(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            return clan == null ? null : LookOf(clan);
        }

        /// <summary>A live image, for anyone; any other state only for the staff and the clan's owner.</summary>
        public byte[] GetImage(Guid imageId, DataModels.User viewer)
        {
            var image = gameData.GetClanImage(imageId);
            if (image == null || image.State == (int)ClanImageState.Blocked) return null;
            if (image.State != (int)ClanImageState.Live && !CanSeeAll(viewer, image)) return null;
            return Read(image.Id);
        }

        public bool IsLive(Guid imageId) => gameData.GetClanImage(imageId)?.State == (int)ClanImageState.Live;

        public byte[] GetLiveCape(Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            return clan?.CapeImageId != null ? Read(clan.CapeImageId.Value) : null;
        }

        /// <summary>
        ///     The live logo of the clan this Twitch user owns, for the logo addresses the game has always
        ///     used (api/clan/logo/{twitchId}), so even older game clients show it.
        /// </summary>
        public byte[] GetLiveLogoByOwnerTwitchId(string twitchUserId)
        {
            if (string.IsNullOrWhiteSpace(twitchUserId)) return null;
            var id = twitchUserId.Contains('_') ? twitchUserId.Split('_')[1] : twitchUserId;
            var user = gameData.GetUserByTwitchId(id);
            var clan = user != null ? gameData.GetClanByOwner(user.Id) : null;
            return clan?.LogoImageId != null ? Read(clan.LogoImageId.Value) : null;
        }

        #endregion

        #region The admin page

        public int GetWaitingCount() => gameData.GetAllClanImages().Count(x => x.State == (int)ClanImageState.Waiting);

        /// <summary>Everyone who has uploaded, waiting ones first, then the latest upload first.</summary>
        public ClanImagePage<ClanImageUploader> GetUploaders(int page, int pageSize, string search = null)
        {
            var rows = new List<ClanImageUploader>();
            foreach (var group in gameData.GetAllClanImages().GroupBy(x => x.UserId))
            {
                var user = gameData.GetUser(group.Key);
                var clan = gameData.GetClanByOwner(group.Key) ?? gameData.GetClan(group.First().ClanId);
                var row = new ClanImageUploader
                {
                    UserId = group.Key,
                    UserName = user?.DisplayName ?? user?.UserName ?? group.Key.ToString(),
                    ClanId = clan?.Id,
                    ClanName = clan?.Name,
                    WaitingCount = group.Count(x => x.State == (int)ClanImageState.Waiting),
                    Uploads = group.Count(),
                    LastUpload = group.Max(x => x.Created),
                    NeedsReview = user?.ClanImagesNeedReview == true,
                    Logo = Map(Shown(group, ClanImageKind.Logo)),
                    Cape = Map(Shown(group, ClanImageKind.Cape)),
                };

                if (!string.IsNullOrWhiteSpace(search)
                    && (row.UserName?.Contains(search, StringComparison.OrdinalIgnoreCase) != true)
                    && (row.ClanName?.Contains(search, StringComparison.OrdinalIgnoreCase) != true))
                    continue;

                rows.Add(row);
            }

            var ordered = rows.OrderByDescending(x => x.WaitingCount > 0).ThenByDescending(x => x.LastUpload).ToList();
            return PageOf(ordered, page, pageSize);
        }

        public ClanImagePage<ClanImageInfo> GetHistory(Guid userId, int page, int pageSize)
        {
            var items = gameData.GetClanImagesByUser(userId).OrderByDescending(x => x.Created).Select(Map).ToList();
            return PageOf(items, page, pageSize);
        }

        public ClanImageResult Accept(Guid imageId, Guid adminUserId)
        {
            lock (mutex)
            {
                var image = gameData.GetClanImage(imageId);
                if (image == null || image.State != (int)ClanImageState.Waiting) return Fail("That image is not waiting for review.");
                var clan = gameData.GetClan(image.ClanId);
                if (clan == null) return Fail("The clan is gone.");

                Reviewed(image, adminUserId);
                GoLive(clan, image);
                return new ClanImageResult { Ok = true, ImageId = image.Id };
            }
        }

        public ClanImageResult Reject(Guid imageId, Guid adminUserId)
        {
            lock (mutex)
            {
                var image = gameData.GetClanImage(imageId);
                if (image == null || image.State != (int)ClanImageState.Waiting) return Fail("That image is not waiting for review.");
                image.State = (int)ClanImageState.Rejected;
                Reviewed(image, adminUserId);
                return new ClanImageResult { Ok = true, ImageId = image.Id };
            }
        }

        /// <summary>
        ///     Deletes the image, puts the clan back on the default for that kind when it was in use, and
        ///     makes every later upload of this owner wait for review.
        /// </summary>
        public ClanImageResult Block(Guid imageId, Guid adminUserId)
        {
            DataModels.Clan changed = null;
            lock (mutex)
            {
                var image = gameData.GetClanImage(imageId);
                if (image == null || image.State == (int)ClanImageState.Blocked) return Fail("That image is already gone.");

                var clan = gameData.GetClan(image.ClanId);
                var kind = (ClanImageKind)image.Kind;
                if (clan != null && ImageIdOf(clan, kind) == image.Id)
                {
                    SetImageId(clan, kind, null);
                    changed = clan;
                }

                image.State = (int)ClanImageState.Blocked;
                Reviewed(image, adminUserId);
                try { File.Delete(PathOf(image.Id)); }
                catch (Exception exc) { logger.LogError("Could not delete blocked clan image " + image.Id + ": " + exc.Message); }

                var user = gameData.GetUser(image.UserId);
                if (user != null) user.ClanImagesNeedReview = true;
            }

            if (changed != null) SendLookChanged(changed);
            return new ClanImageResult { Ok = true, ImageId = imageId };
        }

        public bool SetNeedsReview(Guid userId, bool needsReview)
        {
            var user = gameData.GetUser(userId);
            if (user == null) return false;
            user.ClanImagesNeedReview = needsReview ? true : null;
            return true;
        }

        #endregion

        private void GoLive(DataModels.Clan clan, DataModels.ClanImage image)
        {
            var kind = (ClanImageKind)image.Kind;
            foreach (var old in gameData.GetClanImagesByClan(clan.Id).Where(x => x.Kind == image.Kind && x.Id != image.Id && x.State == (int)ClanImageState.Live))
                old.State = (int)ClanImageState.Replaced;

            image.State = (int)ClanImageState.Live;
            SetImageId(clan, kind, image.Id);
            SendLookChanged(clan);
        }

        /// <summary>Tells every game with a member of this clan in it to load the clan's images and capes again.</summary>
        private void SendLookChanged(DataModels.Clan clan)
        {
            var look = LookOf(clan);
            var members = gameData.GetClanMemberships(clan.Id).Select(x => x.CharacterId).ToHashSet();
            foreach (var session in gameData.GetActiveSessions())
            {
                var hasMember = gameData.GetSessionCharacters(session).Any(x => members.Contains(x.Id))
                    || session.UserId == clan.UserId;
                if (hasMember)
                    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ClanLookChanged, session, look));
            }
        }

        private ClanLook LookOf(DataModels.Clan clan)
        {
            var look = new ClanLook
            {
                ClanId = clan.Id,
                LogoImageId = clan.LogoImageId,
                CapeImageId = clan.CapeImageId,
                CapeStyle = clan.CapeStyle,
            };

            foreach (var role in gameData.GetClanRoles(clan.Id))
            {
                if (role.CapeStyle != null && role.CapeStyle != clan.CapeStyle)
                    look.RoleCapeStyles[role.Id] = role.CapeStyle.Value;
            }

            return look;
        }

        private bool NeedsReview(DataModels.User user)
            => user.IsAdmin != true && (RequireReview || user.ClanImagesNeedReview == true);

        private bool CanSeeAll(DataModels.User viewer, DataModels.ClanImage image)
            => viewer != null && (viewer.IsAdmin == true || viewer.IsModerator == true || viewer.Id == image.UserId);

        /// <summary>
        ///     Checks the size the editor makes (a square logo, a 1:2 cape) and makes a fresh PNG of it. A cape
        ///     gets an opaque cloth colour behind it, as the cape shows the colour under transparent pixels.
        /// </summary>
        private static byte[] Reencode(ClanImageKind kind, byte[] data, out int width, out int height)
        {
            // a codec first: decoding something that is no image throws instead of returning nothing
            using (var codec = SKCodec.Create(new SKMemoryStream(data)))
            {
                if (codec == null) throw new InvalidDataException("That is not an image we can read.");
            }

            using var bitmap = SKBitmap.Decode(data) ?? throw new InvalidDataException("That is not an image we can read.");
            width = bitmap.Width;
            height = bitmap.Height;

            var ok = kind == ClanImageKind.Logo
                ? width == height && width >= MinSize && width <= LogoSize * 2
                : height == width * 2 && width >= MinSize && width <= CapeWidth * 2;
            if (!ok)
                throw new InvalidDataException(kind == ClanImageKind.Logo
                    ? "A clan logo has to be square, " + MinSize + " to " + LogoSize * 2 + " pixels wide."
                    : "A cape image has to be twice as tall as it is wide, " + MinSize + " to " + CapeWidth * 2 + " pixels wide.");

            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(kind == ClanImageKind.Cape ? CapeCloth : SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0);
            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        private DataModels.ClanImage Current(Guid clanId, ClanImageKind kind)
            => gameData.GetClanImagesByClan(clanId)
                .Where(x => x.Kind == (int)kind && (x.State == (int)ClanImageState.Live || x.State == (int)ClanImageState.Waiting))
                .OrderByDescending(x => x.Created).FirstOrDefault();

        // what the admin row shows for a kind: the waiting one when there is one, otherwise the live one,
        // otherwise the latest
        private static DataModels.ClanImage Shown(IEnumerable<DataModels.ClanImage> images, ClanImageKind kind)
        {
            var ofKind = images.Where(x => x.Kind == (int)kind).OrderByDescending(x => x.Created).ToList();
            return ofKind.FirstOrDefault(x => x.State == (int)ClanImageState.Waiting)
                ?? ofKind.FirstOrDefault(x => x.State == (int)ClanImageState.Live)
                ?? ofKind.FirstOrDefault();
        }

        private void Reviewed(DataModels.ClanImage image, Guid adminUserId)
        {
            image.Reviewed = DateTime.UtcNow;
            image.ReviewedBy = adminUserId;
        }

        private static Guid? ImageIdOf(DataModels.Clan clan, ClanImageKind kind)
            => kind == ClanImageKind.Logo ? clan.LogoImageId : clan.CapeImageId;

        private static void SetImageId(DataModels.Clan clan, ClanImageKind kind, Guid? id)
        {
            if (kind == ClanImageKind.Logo) clan.LogoImageId = id;
            else clan.CapeImageId = id;
        }

        private ClanImageInfo Map(DataModels.ClanImage image)
        {
            if (image == null) return null;
            var reviewer = image.ReviewedBy != null ? gameData.GetUser(image.ReviewedBy.Value) : null;
            return new ClanImageInfo
            {
                Id = image.Id,
                ClanId = image.ClanId,
                UserId = image.UserId,
                Kind = (ClanImageKind)image.Kind,
                State = (ClanImageState)image.State,
                Created = image.Created,
                Reviewed = image.Reviewed,
                ReviewedBy = reviewer?.DisplayName ?? reviewer?.UserName,
                Width = image.Width,
                Height = image.Height,
                Size = image.Size,
            };
        }

        private static ClanImagePage<T> PageOf<T>(List<T> items, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(0, page);
            return new ClanImagePage<T>
            {
                Items = items.Skip(page * pageSize).Take(pageSize).ToList(),
                Total = items.Count,
                Page = page,
                PageSize = pageSize,
            };
        }

        private byte[] Read(Guid imageId)
        {
            var path = PathOf(imageId);
            try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
            catch (Exception exc)
            {
                logger.LogError("Could not read clan image " + imageId + ": " + exc.Message);
                return null;
            }
        }

        private static string PathOf(Guid imageId) => Path.Combine(FolderPath, imageId.ToString("N") + ".png");

        private static ClanImageResult Fail(string error) => new ClanImageResult { Ok = false, Error = error };
    }
}

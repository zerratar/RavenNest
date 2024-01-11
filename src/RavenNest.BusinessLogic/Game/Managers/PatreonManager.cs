using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Models.Patreon;
using RavenNest.BusinessLogic.Models.Patreon.API;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game
{
    public class PatreonManager : IPatreonManager
    {
        private readonly GameData gameData;
        private readonly ILogger<PatreonManager> logger;
        private readonly IHttpContextAccessor accessor;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly PatreonSettings patreon;
        private readonly HttpClient httpClient;

        //private Task campaignDetailsTask;
        private PatreonCampaign activeCampaign;
        private List<PatreonTier> fallbackTiers;

        public PatreonManager(
            GameData gameData,
            ILogger<PatreonManager> logger,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
        {
            this.gameData = gameData;
            this.logger = logger;
            this.accessor = accessor;
            this.sessionInfoProvider = sessionInfoProvider;

            var data = (this.gameData as GameData);
            this.patreon = data.Patreon;
            this.httpClient = new HttpClient();

            //this.campaignDetailsTask = EnsureCampaignDetailsAsync();
        }

        public void Unlink(SessionInfo session)
        {
            var user = gameData.GetUser(session.UserId);
            if (user == null) return;
            var patreonUser = gameData.GetPatreonUser(user.Id);
            if (patreonUser == null) return;
            patreonUser.AccessToken = null;
            patreonUser.RefreshToken = null;
            patreonUser.ProfilePicture = null;
            patreonUser.Scope = null;
            patreonUser.TokenType = null;
        }
        public async Task<UserPatreon> LinkAsync(SessionInfo session, string code)
        {
            var token = await GetAccessTokenUsingCodeAsync(code);
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
                return null;

            // The following is a super ugly hack, this is due to an issue in Patreon API
            // if the creator logs in, a new access token is generated and it overwrites the creator's token.
            // if this is using the creator account, the access token will be regenerated.
            // assign the new one if this is Zerratar.
            var isCreator = false;
            if (session.UserName.Equals("zerratar", StringComparison.OrdinalIgnoreCase))
            {
                isCreator = true;
                patreon.ExpiresIn = token.ExpiresIn;
                patreon.CreatorRefreshToken = token.RefreshToken;
                patreon.CreatorAccessToken = token.AccessToken;
                patreon.TokenType = token.TokenType;
                patreon.Scope = token.Scope;
                patreon.LastUpdate = DateTime.UtcNow;
            }

            var isNewUserPatreon = false;
            var user = gameData.GetUser(session.UserId);
            var patreonUser = gameData.GetPatreonUser(user.Id);
            if (patreonUser == null)
            {
                // check one more time but using patreon ID, in case we received one earlier from a webhook.
                // This will allow us to re-use those records.
                patreonUser = new UserPatreon
                {
                    Id = Guid.NewGuid(),
                    TwitchUserId = user.UserId,

                    UserId = user.Id,
                    Email = user.Email,
                    Created = DateTime.UtcNow
                };
                isNewUserPatreon = true;
            }

            patreonUser.AccessToken = token.AccessToken;
            patreonUser.RefreshToken = token.RefreshToken;
            patreonUser.Scope = token.Scope;
            patreonUser.ExpiresIn = token.ExpiresIn;
            patreonUser.TokenType = token.TokenType;
            patreonUser.Updated = DateTime.UtcNow;

            // get patreon data

            /*
                curl --request GET \
                  --url https://www.patreon.com/api/oauth2/api/current_user \
                  --header 'authorization: Bearer access_token'
             */

            if (string.IsNullOrEmpty(patreonUser.AccessToken))
            {
                return null;
            }
            try
            {
                var patreonData = await GetIdentityAsync(patreonUser);
                if (patreonData != null)
                {
                    // make sure we have campaign loaded.
                    //if (!campaignDetailsTask.IsCompleted)
                    //    await campaignDetailsTask;

                    await EnsureCampaignDetailsAsync();

                    if (long.TryParse(patreonData.Data.Id, out var patreonId))
                    {
                        if (isNewUserPatreon)
                        {
                            var existing = gameData.GetPatreonUser(patreonId);
                            if (existing != null)
                            {
                                Replace(ref patreonUser, existing);
                                isNewUserPatreon = false;
                            }
                        }
                        patreonUser.PatreonId = patreonId;
                    }

                    var firstName = patreonData.Data?.Attributes?.FirstName;
                    var lastName = patreonData.Data?.Attributes?.LastName;
                    var email = patreonData.Data?.Attributes?.Email;
                    var image = patreonData.Data?.Attributes?.ImageUrl?.ToString();

                    if (!string.IsNullOrEmpty(firstName))
                    {
                        patreonUser.FirstName = firstName;
                        patreonUser.FullName = firstName;
                    }

                    if (!string.IsNullOrEmpty(lastName))
                        patreonUser.FullName = firstName + " " + lastName;

                    if (!string.IsNullOrEmpty(email))
                        patreonUser.Email = email;

                    if (!string.IsNullOrEmpty(image))
                        patreonUser.ProfilePicture = image;

                    if (patreonData.Included != null)
                    {
                        var highestEntitledTier = GetHighestEntitledTier(patreonData);
                        if (highestEntitledTier != null)
                        {
                            var t = highestEntitledTier.Tier;
                            patreonUser.PledgeAmount = t.AmountCents;
                            patreonUser.PledgeTitle = t.Title;
                            patreonUser.Tier = t.Level;
                            user.PatreonTier = t.Level;
                        }
                        else if (!isCreator)
                        {
                            patreonUser.PledgeAmount = null;
                            patreonUser.PledgeTitle = null;
                            patreonUser.Tier = null;
                            user.PatreonTier = null;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Unable to process patreon data! " + exc);
            }

            // add this last, in case we found the user using patreon id
            // after we first created it. If so, then we don't want to add another one.
            if (isNewUserPatreon)
            {
                gameData.Add(patreonUser);
            }

            session.Patreon = ModelMapper.Map(patreonUser);

            await this.sessionInfoProvider.StoreAsync(session.SessionId);
            return patreonUser;
        }

        private static void Replace(ref UserPatreon patreonUser, UserPatreon existing)
        {
            existing.TwitchUserId = patreonUser.TwitchUserId;
            existing.UserId = patreonUser.UserId;
            existing.Email = patreonUser.Email;
            existing.AccessToken = patreonUser.AccessToken;
            existing.RefreshToken = patreonUser.RefreshToken;
            existing.Scope = patreonUser.Scope;
            existing.ExpiresIn = patreonUser.ExpiresIn;
            existing.TokenType = patreonUser.TokenType;
            existing.Updated = patreonUser.Updated;
            patreonUser = existing;
        }

        private PatreonMembership GetHighestEntitledTier(PatreonIdentity.Root patreonData)
        {
            PatreonTier tier = null;

            var tiers = new HashSet<string>();
            var memberships = new List<PatreonMembership>();

            foreach (var i in patreonData.Included)
            {
                var attr = i.Attributes;
                if (attr == null)
                {
                    continue;
                }

                var status = attr.PatronStatus;

                if (i.Type == "member")
                {
                    // if member, we gotta check if this is a good member
                    // we are member of..

                    var entitledTiers = i.Relationships.CurrentlyEntitledTiers;
                    if (entitledTiers != null && entitledTiers.Data != null)
                    {
                        memberships.AddRange(entitledTiers.Data
                            .Where(x => x.Type == "tier" && activeCampaign.Tiers.Any(y => y.Id == x.Id))
                            .Select(x => new PatreonMembership() { Tier = activeCampaign.Tiers.FirstOrDefault(y => y.Id == x.Id) }));

                        foreach (var m in memberships)
                            tiers.Add(m.Tier.Id);
                    }
                }

                if (i.Type == "tier")
                {
                    var t = activeCampaign.Tiers.FirstOrDefault(x => x.Id == i.Id);
                    if (t != null && tiers.Add(t.Id))
                    {
                        memberships.Add(new PatreonMembership { Tier = t });
                    }
                }

                //if (status == "active_patron")
                //{
                //    var targetTier = activeCampaign.Tiers.FirstOrDefault(x => x.AmountCents == attr.CurrentlyEntitledAmountCents);
                //    memberships.Add(new PatreonMembership(targetTier, status, attr.CurrentlyEntitledAmountCents));
                //}
            }

            return memberships.OrderByDescending(x => x.Tier.AmountCents).FirstOrDefault();
        }

        public async Task EnsureCampaignDetailsAsync()
        {
            // check if we need to update our access token.
            if (activeCampaign != null ||
                string.IsNullOrEmpty(patreon.CreatorAccessToken))
            {
                return;
            }

            await EnsureAccessTokenAsync();

            var result = await GetCampaignAsync();
            if (result == null)
            {
                // try again later
                return;
            }
            var campaign = result.Data[0];
            var tiers = new List<PatreonTier>();
            var sorted = result.Included.OrderBy(x => x.Attributes.AmountCents).ToArray();
            for (var i = 0; i < sorted.Length; ++i)
            {
                var tierData = result.Included[i];
                var name = tierData.Attributes.Title;
                var cents = tierData.Attributes.AmountCents;
                var id = tierData.Id;
                tiers.Add(new PatreonTier
                {
                    Id = id,
                    Title = name,
                    AmountCents = cents,
                    Level = i, // since we have steel, which does not include an actual boost in game // i + 1,
                });
            }

            this.activeCampaign = new PatreonCampaign
            {
                Id = campaign.Id,
                PatreonCount = campaign.Attributes.PatronCount,
                Tiers = tiers
            };
        }

        private async Task EnsureAccessTokenAsync()
        {
            var expires = patreon.LastUpdate;
            if (!string.IsNullOrEmpty(patreon.ExpiresIn))
            {
                expires = expires.AddSeconds(int.Parse(patreon.ExpiresIn));
            }
            //else
            //{
            //    expires = expires.AddDays(30);
            //    patreon.ExpiresIn = ((int)((expires - patreon.LastUpdate).TotalSeconds)).ToString();
            //}

            if (string.IsNullOrEmpty(patreon.TokenType))
            {
                patreon.TokenType = "Bearer";
            }

            if (DateTime.UtcNow >= expires)
            {
                await RefreshCreatorAccessTokenAsync();
            }
        }

        // needs to be requested every time, since memberships may have changed.
        // we do need to keep track on the campaign id though so we don't need to fetch which campaigns available every time.
        public async Task<PatreonMemberCollection.Root> GetCampaignMembersAsync()
        {
            var url = "https://www.patreon.com/api/oauth2/v2/campaigns/" + activeCampaign.Id + "/members" +
                "?fields" + WebUtility.UrlEncode("[member]") + "=currently_entitled_amount_cents,patron_status";
            var membersResponse = await GetCampaignMembersAsync(url);

            var nextPageUrl = membersResponse?.Links?.Next;//meta?.Pagination?.Cursors?.Next;
            while (!string.IsNullOrEmpty(nextPageUrl))
            {
                var nextResponse = await GetCampaignMembersAsync(nextPageUrl);
                membersResponse.Data.AddRange(nextResponse.Data);
                nextPageUrl = nextResponse?.Links?.Next;
            }

            return membersResponse;
        }

        //public async Task<PatreonMember.Root> GetCampaignMemberAsync(Guid id)
        //{
        //    try
        //    {
        //        var url = "https://www.patreon.com/api/oauth2/v2/members/" + id
        //            + "?fields" + WebUtility.UrlEncode("[member]") + "=currently_entitled_amount_cents,patron_status"
        //            + "&include=currently_entitled_tiers,user";
        //        var httpRequest = (HttpWebRequest)WebRequest.Create(url);
        //        httpRequest.Headers["authorization"] = patreon.TokenType + " " + patreon.CreatorAccessToken;
        //        using var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
        //        using var streamReader = new StreamReader(httpResponse.GetResponseStream());
        //        var json = await streamReader.ReadToEndAsync();
        //        return JsonConvert.DeserializeObject<PatreonMember.Root>(json);
        //    }
        //    catch (WebException webExc)
        //    {
        //        if (webExc.Response is HttpWebResponse r)
        //        {
        //            using var streamReader = new StreamReader(r.GetResponseStream());
        //            var responseString = await streamReader.ReadToEndAsync();
        //        }
        //        return null;
        //    }
        //}

        private async Task<PatreonMemberCollection.Root> GetCampaignMembersAsync(string url)
        {
            try
            {
                var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Headers["authorization"] = patreon.TokenType + " " + patreon.CreatorAccessToken;
                using var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using var streamReader = new StreamReader(httpResponse.GetResponseStream());
                var json = await streamReader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<PatreonMemberCollection.Root>(json);
            }
            catch (WebException webExc)
            {
                if (webExc.Response is HttpWebResponse r)
                {
                    using var streamReader = new StreamReader(r.GetResponseStream());
                    var responseString = await streamReader.ReadToEndAsync();
                }
                return null;
            }
        }

        public async Task<PatreonCampaigns.Root> GetCampaignAsync()
        {
            var url = "https://www.patreon.com/api/oauth2/v2/campaigns" +
                "?include=tiers" +
                "&fields" + WebUtility.UrlEncode("[tier]") + "=title,amount_cents" +
                "&fields" + WebUtility.UrlEncode("[campaign]") + "=created_at,creation_name,discord_server_id,image_small_url,image_url,is_charged_immediately,is_monthly,is_nsfw,main_video_embed,main_video_url,one_liner,one_liner,patron_count,pay_per_name,pledge_url,published_at,summary,thanks_embed,thanks_msg,thanks_video_url,has_rss,has_sent_rss_notify,rss_feed_title,rss_artwork_url,patron_count,discord_server_id,google_analytics_id";

            try
            {
                var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Headers["authorization"] = patreon.TokenType + " " + patreon.CreatorAccessToken;
                using var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using var streamReader = new StreamReader(httpResponse.GetResponseStream());
                var json = await streamReader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<PatreonCampaigns.Root>(json);
            }
            catch (WebException webExc)
            {
                if (webExc.Response is HttpWebResponse r)
                {
                    using var streamReader = new StreamReader(r.GetResponseStream());
                    var responseString = await streamReader.ReadToEndAsync();
                }
                return null;
            }
        }

        public static async Task<PatreonIdentity.Root> GetIdentityAsync(UserPatreon patreon)
        {
            var url = "https://www.patreon.com/api/oauth2/v2/identity" +
                "?include=memberships,memberships.currently_entitled_tiers" +
                //"&fields" + WebUtility.UrlEncode("[tier]") + "=currently_entitled_tiers" +
                "&fields" + WebUtility.UrlEncode("[member]") + "=currently_entitled_amount_cents,patron_status" +
                "&fields" + WebUtility.UrlEncode("[user]") + "=created,email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity";

            try
            {
                var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Headers["authorization"] = patreon.TokenType + " " + patreon.AccessToken;
                using var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using var streamReader = new StreamReader(httpResponse.GetResponseStream());
                var json = await streamReader.ReadToEndAsync();

                return Newtonsoft.Json.JsonConvert.DeserializeObject<PatreonIdentity.Root>(json);
            }
            catch (WebException exc)
            {
                if (exc.Response is HttpWebResponse r)
                {
                    using var streamReader = new StreamReader(r.GetResponseStream());
                    var responseString = await streamReader.ReadToEndAsync();
                    var now = DateTime.UtcNow;
                    File.WriteAllText(Path.Combine(FolderPaths.GeneratedData, "patreon-identity-request-error_" + now.ToString("yyyy-MM-dd_HHmmss") + ".log"),
                        "Exception: " + exc.ToString() + "\r\n\r\n" + responseString);

                }
                return null;
            }
        }

        public async Task<AccessTokenRefresh> GetAccessTokenUsingCodeAsync(string code)
        {
            try
            {
                var url = $"https://www.patreon.com/api/oauth2/token";

                //?grant_type=refresh_token&refresh_token={patreon.CreatorRefreshToken}&client_id={patreon.ClientId}&client_secret={patreon.ClientSecret}
                var dict = new Dictionary<string, string>
                {
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "client_id", patreon.ClientId },
                    { "client_secret", patreon.ClientSecret },
                    { "redirect_uri", GetRedirectUrl() }
                };
                using var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(dict));

                var contentResponse = await response.Content.ReadAsStringAsync();
                //response.EnsureSuccessStatusCode();

                return JsonConvert.DeserializeObject<AccessTokenRefresh>(contentResponse);
            }
            catch (WebException exc)
            {
                if (exc.Response is HttpWebResponse r)
                {
                    using var streamReader = new StreamReader(r.GetResponseStream());
                    var responseString = await streamReader.ReadToEndAsync();
                }
                return null;
            }
        }

        public async Task<bool> RefreshCreatorAccessTokenAsync()
        {
            try
            {
                var url = $"https://www.patreon.com/api/oauth2/token?grant_type=refresh_token&refresh_token={patreon.CreatorRefreshToken}&client_id={patreon.ClientId}&client_secret={patreon.ClientSecret}";
                using var response = await httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                var data = JsonConvert.DeserializeObject<AccessTokenRefresh>(await response.Content.ReadAsStringAsync());
                if (data == null) return false;
                if (!string.IsNullOrEmpty(data.AccessToken)) patreon.CreatorAccessToken = data.AccessToken;
                if (!string.IsNullOrEmpty(data.RefreshToken)) patreon.CreatorRefreshToken = data.RefreshToken;
                if (!string.IsNullOrEmpty(data.Scope)) patreon.Scope = data.Scope;
                if (!string.IsNullOrEmpty(data.ExpiresIn)) patreon.ExpiresIn = data.ExpiresIn;
                if (!string.IsNullOrEmpty(data.TokenType)) patreon.TokenType = data.TokenType;
                patreon.LastUpdate = DateTime.UtcNow;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> RefreshUserAccessTokenAsync(UserPatreon user)
        {
            try
            {
                var url = $"https://www.patreon.com/api/oauth2/token?grant_type=refresh_token&refresh_token={user.RefreshToken}&client_id={patreon.ClientId}&client_secret={patreon.ClientSecret}";
                using var response = await httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                var data = JsonConvert.DeserializeObject<AccessTokenRefresh>(await response.Content.ReadAsStringAsync());
                if (data == null) return false;
                if (!string.IsNullOrEmpty(data.AccessToken)) user.AccessToken = data.AccessToken;
                if (!string.IsNullOrEmpty(data.RefreshToken)) user.RefreshToken = data.RefreshToken;
                if (!string.IsNullOrEmpty(data.Scope)) user.Scope = data.Scope;
                if (!string.IsNullOrEmpty(data.ExpiresIn)) user.ExpiresIn = data.ExpiresIn;
                if (!string.IsNullOrEmpty(data.TokenType)) user.TokenType = data.TokenType;
                user.Updated = DateTime.UtcNow;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetRedirectUrl()
        {
            var context = accessor.HttpContext;
            if (context == null || context.Request == null || !context.Request.Host.HasValue)
            {
                return "https://www.ravenfall.stream/patreon/link";
            }

            return $"https://{context.Request.Host}/patreon/link";
        }

        public async Task<PatreonTier> GetTierByLevelAsync(int level)
        {
            await EnsureCampaignDetailsAsync();

            var tiers = GetAllTiers();

            foreach (var tier in tiers.OrderByDescending(x => x.Level))
            {
                if (level >= tier.Level)
                    return tier;
            }

            return null;
        }


        public async Task<PatreonTier> GetTierByCentsAsync(decimal pledgeAmountCents)
        {
            await EnsureCampaignDetailsAsync();
            var tiers = GetAllTiers();
            foreach (var tier in tiers.OrderByDescending(x => x.AmountCents))
            {
                if (pledgeAmountCents >= tier.AmountCents)
                    return tier;
            }

            return null;
        }


        private List<PatreonTier> GetAllTiers()
        {
            List<PatreonTier> tiers = activeCampaign?.Tiers;
            if (tiers == null)
            {
                if (fallbackTiers == null)
                    fallbackTiers = new List<PatreonTier>
                    {
                        new PatreonTier { AmountCents = 50, Level = 1, Title = "Mithril" },
                        new PatreonTier { AmountCents = 150, Level = 2, Title = "Rune" },
                        new PatreonTier { AmountCents = 300, Level = 3, Title = "Dragon" },
                        new PatreonTier { AmountCents = 500, Level = 4, Title = "Abraxas" },
                        new PatreonTier { AmountCents = 1000, Level = 5, Title = "Phantom" }
                    };

                tiers = fallbackTiers;
            }

            return tiers;
        }
    }

    public class PatreonMembership
    {
        public PatreonTier Tier;
        public string PatronStatus;
        public long? Cents;

        public PatreonMembership() { }
        public PatreonMembership(PatreonTier tier, string patronStatus, long? cents)
        {
            this.Tier = tier;
            this.PatronStatus = patronStatus;
            this.Cents = cents;
        }

        public override string ToString()
        {
            return "Status: " + PatronStatus + ", Tier: " + Tier?.Title + ", Cents: " + Cents;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using JsonApiSerializer.JsonApi;
using System.Reflection;
using RavenNest.PatreonAPI.Models;

namespace RavenNest.PatreonAPI
{
    public class PatreonClient : IDisposable
    {
        public const string SAFE_ROOT = "https://www.patreon.com/api/oauth2/v2/";
        public const string PUBLIC_ROOT = "https://www.patreon.com/api/";

        public static string CampaignURL(string campaignId) => SAFE_ROOT + $"campaigns/{campaignId}";
        public static string PledgesURL(string campaignId) => CampaignURL(campaignId) + "/pledges";
        public static string MembersURL(string campaignId) => CampaignURL(campaignId) + "/members";
        public static string MemberURL(string memberId) => SAFE_ROOT + $"members/{memberId}";

        public static string UserURL(string userId) => PUBLIC_ROOT + "user/" + userId;

        HttpClient httpClient;

        public PatreonClient(string accessToken)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        }

        static string GenerateFieldsAndIncludes(Type includes, params Type[] fields)
        {
            var str = new StringBuilder();

            foreach (var field in fields)
            {
                GenerateFields(field, str);
                str.Append("&");
            }

            GenerateIncludes(includes, str);

            return str.ToString();
        }

        static void GenerateFields(Type type, StringBuilder str)
        {
            str.Append("fields%5B");

            var name = type.Name.Replace("Attributes", "");

            for (int i = 0; i < name.Length; i++)
            {
                var ch = name[i];

                if (char.IsUpper(ch) && i != 0)
                    str.Append("_");

                str.Append(char.ToLower(ch));
            }

            str.Append("%5D=");

            GenerateFieldList(type, str);
        }

        static void GenerateIncludes(Type type, StringBuilder str)
        {
            str.Append($"include=");

            GenerateFieldList(type, str);
        }

        static void GenerateFieldList(Type type, StringBuilder str)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attributes = property.GetCustomAttributes(typeof(JsonPropertyAttribute), true);

                if (attributes == null || attributes.Length == 0)
                    continue;

                foreach (var attribute in attributes)
                {
                    var jsonPropertyName = ((JsonPropertyAttribute)attribute).PropertyName;
                    if (jsonPropertyName.Contains('/'))
                    {
                        jsonPropertyName = jsonPropertyName.Split('/')[0];
                    }

                    str.Append(jsonPropertyName);
                    str.Append(",");
                }
            }

            // remove the last comma
            str.Length -= 1;
        }

        public static string AppendQuery(string url, string query)
        {
            if (url.Contains("?"))
                url += "&" + query;
            else
                url += "?" + query;

            return url;
        }

        public async Task<HttpResponseMessage> GET(string url) => await httpClient.GetAsync(url);

        public async Task<T> GET<T>(string url, JsonSerializerSettings settings = null)
            where T : class
        {
            try
            {
                var response = await GET(url.Replace("[", "%5B").Replace("]", "%5D"));

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        settings = (settings ?? new JsonSerializerSettings());
                        return JsonConvert.DeserializeObject<T>(json, settings);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine(ex.ToString());
#endif
                    }
                }
            }
            catch (Exception exc)
            {

            }
            return null;
        }

        //public async Task<Campaign> GetCampaign(string campaignId)
        //{
        //    var url = CampaignURL(campaignId);

        //    url = AppendQuery(url, GenerateFieldsAndIncludes(typeof(CampaignRelationships),
        //        typeof(CampaignAttributes), typeof(UserAttributes), typeof(TierAttributes)));
        //    var document = await GET<DocumentRoot<Campaign>>(url).ConfigureAwait(false);

        //    return document.Data;
        //}

        //public async Task<List<Tier>> GetCampaignTiers(string campaignId)
        //{
        //    var campaign = await GetCampaign(campaignId).ConfigureAwait(false);

        //    return campaign.Relationships.Tiers;
        //}
        public async Task<List<PatreonMembersData>> GetCampaignMembers(string campaignId)
        {
            var list = new List<PatreonMembersData>();
            string next = MembersURL(campaignId);
            do
            {
                var url = next;

                url += "?include=currently_entitled_tiers,address&fields[member]=,email,full_name,is_follower,last_charge_date,last_charge_status,lifetime_support_cents,currently_entitled_amount_cents,patron_status&fields[tier]=amount_cents,created_at,description,discord_role_ids,edited_at,patron_count,published,published_at,requires_shipping,title,url&fields[address]=addressee,city,line_1,line_2,phone_number,postal_code,state";

                //url = AppendQuery(url, GenerateFieldsAndIncludes(
                //    typeof(MemberRelationships),
                //    typeof(MemberAttributes),
                //    typeof(UserAttributes)
                //    ));

                var document = await GET<PatreonMembersData>(url, PatreonMembersConverter.Settings).ConfigureAwait(false);
                list.Add(document);

                if (document.Links != null && !string.IsNullOrEmpty(document.Links.Next))
                    next = document.Links.Next;
                else
                    next = null;

            } while (next != null);

            return list;
        }

        //public async Task<User> GetUser(string id) => (await GET<UserData>(UserURL(id)).ConfigureAwait(false))?.User;

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}

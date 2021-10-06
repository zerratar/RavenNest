using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace RavenNest.PatreonAPI
{
    public class PatreonAPI
    {
        enum returnFormat
        {
            ApiReturnFormatJson = 0,
            ApiReturnFormatDictionary = 1
        }
        private String accessToken;
        private HttpClient patreonClient = new HttpClient() { BaseAddress = new Uri("https://api.patreon.com/oauth2/api/") };
        private returnFormat returnFormatType;

        public PatreonAPI(string accessToken)
        {
            this.accessToken = accessToken;
            patreonClient.DefaultRequestHeaders.Add("User-Agent", "PatreonSharp (" + System.Runtime.InteropServices.RuntimeInformation.OSDescription + "," + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture + ")");
            patreonClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        }


        public object fetchUser()
        {
            return getData("current_user");
        }

        public object fetchCampaignAndPatrons()
        {
            return getData("current_user/campaigns?include=rewards,creator,goals,pledges");
        }

        public object fetchCampaign()
        {
            return getData("current_user/campaigns?include=rewards,creator,goals");
        }
        public object fetchCampaign2()
        {
            return getData("current_user/campaigns?include=pledges");
        }


        public object fetchCampaignMembers(string campaignId, int pageSize, string cursor = null)
        {
            String url = "campaigns/" + campaignId + "/members";

            return getData(url);
        }

        public object fetchPageofPledges(string campaignId, int pageSize, string cursor = null)
        {
            String url = "campaigns/" + campaignId + "/pledges?page%5Bcount%5D=" + pageSize.ToString();

            if (!string.IsNullOrEmpty(cursor))
            {
                string escapedCursor = HttpUtility.UrlEncode(cursor);
                url = url + "&page%5Bcursor%5D=" + escapedCursor;
            }

            return getData(url);
        }

        public object getData(String suffix)
        {
            HttpRequestMessage request = new HttpRequestMessage
            {
                RequestUri = new Uri(suffix, UriKind.Relative),
                Method = HttpMethod.Get
            };

            var response = patreonClient.SendAsync(request);

            if (response.Result.StatusCode.GetHashCode() >= 500)
            {
                return response.Result.Content.ReadAsStringAsync().Result;
            }

            switch (returnFormatType)
            {
                case returnFormat.ApiReturnFormatJson:
                default:
                    return response.Result.Content.ReadAsStringAsync().Result;
                case returnFormat.ApiReturnFormatDictionary:
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Result.Content.ReadAsStringAsync().Result);
            }
        }
    }
}

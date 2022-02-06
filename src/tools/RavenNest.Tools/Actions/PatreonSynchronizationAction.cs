using RavenNest.PatreonAPI;
using Shinobytes.Console.Forms;

namespace RavenNest.Tools.Actions
{
    public class PatreonSynchronizationAction
    {

        const string CAMPAIGN_ID = "";
        const string ACCESS_TOKEN = "";

        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }

        public PatreonSynchronizationAction(ProgressBar toolProgress, TextBlock toolStatus)
        {
            this.ToolProgress = toolProgress;
            this.ToolStatus = toolStatus;
            //confirmDialog = new SaveConfirmationWindow();
        }

        public async void Apply()
        {
            var p = new PatreonAPI.PatreonAPI(ACCESS_TOKEN);
            //var data2 = p.GetMembers();
            var data = p.fetchCampaignMembers(CAMPAIGN_ID, 100);


            var patreon = new PatreonClient(ACCESS_TOKEN);
            //var pledges = await patreon.GetCampaignPledges(CAMPAIGN_ID);

            var members = await patreon.GetCampaignMembers(CAMPAIGN_ID);
            //var campaign = await patreon.GetCampaign(CAMPAIGN_ID);

        }
    }
}

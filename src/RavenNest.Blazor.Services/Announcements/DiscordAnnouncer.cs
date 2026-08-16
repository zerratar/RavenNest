using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Settings;

namespace RavenNest.Blazor.Services.Announcements
{
    /// <summary>
    ///     Pushes an announcement to Discord through a webhook.
    /// </summary>
    /// <remarks>
    ///     A webhook rather than the bot, because it needs nothing from the bot: no new command, no
    ///     deploy, no shared secret beyond the URL itself. Discord makes one per channel and posting
    ///     to it is a single request.
    ///
    ///     <para>
    ///     Nothing calls this automatically. It fires when an administrator presses the button on a
    ///     post that is already published, and the post records that it was sent so it cannot go
    ///     twice. An announcement reaches everybody at once and should never leave on a timer or as
    ///     a side effect of saving a draft.
    ///     </para>
    ///
    ///     <para>
    ///     The webhook comes from server settings, which means the admin panel or, failing that,
    ///     appsettings.json. With no URL set this does nothing and says so, so the feature is off
    ///     until somebody turns it on rather than half working.
    ///     </para>
    /// </remarks>
    public class DiscordAnnouncer
    {
        /// <summary>
        ///     One client for the lifetime of the process. IHttpClientFactory would be the usual
        ///     answer, but it lives in a package this project does not reference, and the pooling
        ///     it exists to provide is worth nothing here: this fires when a person presses a
        ///     button, a few times a month, at one host.
        /// </summary>
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly ILogger<DiscordAnnouncer> logger;
        private readonly IServerSettingsProvider settings;

        public DiscordAnnouncer(
            ILogger<DiscordAnnouncer> logger,
            IServerSettingsProvider settings)
        {
            this.logger = logger;
            this.settings = settings;
        }

        public bool IsConfigured => settings.IsSet(ServerSettingsRegistry.DiscordAnnouncementWebhook);

        public async Task<bool> PostAsync(Announcement post)
        {
            if (post == null) return false;

            var webhook = settings.GetString(ServerSettingsRegistry.DiscordAnnouncementWebhook);
            if (string.IsNullOrWhiteSpace(webhook))
            {
                logger.LogWarning("Discord announcement not sent: no webhook is set in server settings.");
                return false;
            }

            try
            {
                var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    // The embed carries the formatting; the content line is what shows in a
                    // notification preview, so it is the summary rather than the title alone.
                    embeds = new[]
                    {
                        new
                        {
                            title = post.Title,
                            description = Describe(post),
                            url = "https://www.ravenfall.stream/news/" + post.Slug,
                            color = ColourOf(post.Kind),
                            timestamp = (post.PublishedUtc ?? DateTime.UtcNow).ToString("o")
                        }
                    }
                });

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await Http.PostAsync(webhook, content);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError(
                        "Discord rejected the announcement '" + post.Slug + "': " +
                        (int)response.StatusCode + " " + response.ReasonPhrase);
                    return false;
                }

                return true;
            }
            catch (Exception exc)
            {
                logger.LogError("Could not send the announcement '" + post.Slug + "' to Discord: " + exc);
                return false;
            }
        }

        /// <summary>
        ///     The summary, plus the date the change lands when there is one. That date is the
        ///     reason most of these get posted, so it does not belong only on the website.
        /// </summary>
        private static string Describe(Announcement post)
        {
            var text = string.IsNullOrWhiteSpace(post.Summary) ? "" : post.Summary.Trim();

            if (post.EffectiveUtc != null)
            {
                var when = post.EffectiveUtc.Value;
                var lead = post.IsUpcoming ? "\n\n**Takes effect " : "\n\n**Took effect ";
                text += lead + when.ToString("d MMMM yyyy") + "**";
            }

            return text.Length == 0 ? "Read it on the website." : text;
        }

        private static int ColourOf(AnnouncementKind kind) => kind switch
        {
            AnnouncementKind.Update => 0x4CBF6A,
            AnnouncementKind.Notice => 0xD9A13A,
            AnnouncementKind.Alert => 0xE05A52,
            _ => 0xE8A33D
        };
    }
}

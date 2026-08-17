using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services.Assistant
{
    /// <summary>
    ///     One page of the site the assistant can point somebody at.
    /// </summary>
    public sealed class SitePage
    {
        public SitePage(string path, string name, string what, bool adminOnly = false)
        {
            Path = path;
            Name = name;
            What = what;
            AdminOnly = adminOnly;
        }

        public string Path { get; }

        /// <summary>What the button says.</summary>
        public string Name { get; }

        /// <summary>What you do there, for the model to match a question against.</summary>
        public string What { get; }

        public bool AdminOnly { get; }
    }

    /// <summary>
    ///     Where things are on the site.
    /// </summary>
    /// <remarks>
    ///     A list rather than something inferred from the router, because the router knows the paths
    ///     and nothing else. What makes this useful is the sentence saying what each page is for,
    ///     which is what a question gets matched against, and no amount of route scanning produces
    ///     that.
    ///
    ///     <para>
    ///     It has to be kept current by hand, and that is a real cost worth naming. The mitigation
    ///     is that a wrong entry is visible immediately: the button goes to the wrong place, which
    ///     somebody notices, unlike a wrong fact which just sounds plausible.
    ///     </para>
    /// </remarks>
    public static class SitePages
    {
        public static readonly IReadOnlyList<SitePage> All = new List<SitePage>
        {
            new SitePage("/dashboard", "Overview",
                "the dashboard front page, with a summary of your characters and what is happening"),

            new SitePage("/characters", "Characters",
                "all your characters, their skills, inventory, equipment, clan and state. This is " +
                "where you look at one character in detail, train, and see what it is carrying"),

            new SitePage("/stash", "Item stash",
                "your stash: everything you own that no character is carrying. Deposit into it and " +
                "take things out again, and send items to a character"),

            new SitePage("/clan", "Clan",
                "your clan: members, ranks and clan skills"),

            new SitePage("/clan/stash", "Clan stash",
                "the clan's shared bank. Put items in, take them out if your rank allows, see who " +
                "has taken what, and set each rank's daily allowance if you own the clan"),

            new SitePage("/clan-invites", "Clan invites",
                "invitations to join a clan, where you accept or decline them"),

            new SitePage("/marketplace", "Marketplace",
                "buying and selling items with other players"),

            new SitePage("/shop", "Vendor",
                "the vendor: buying back what other players have sold, and what it pays for things"),

            new SitePage("/items", "Item catalogue",
                "every item in the game with its stats and requirements"),

            new SitePage("/highscore", "Highscore",
                "the leaderboards for every skill"),

            new SitePage("/loyalty", "Loyalty",
                "your time and rewards in each streamer's channel"),

            new SitePage("/bot", "Stream and bot",
                "your own stream's settings, the bot, and how it behaves in your channel"),

            new SitePage("/town", "Town",
                "your town, its buildings and what they are assigned to"),

            new SitePage("/patreon", "Patreon",
                "your Patreon subscription and what it gives you"),

            new SitePage("/notifications", "Notifications",
                "things that have happened to you: sales, gifts, invitations"),

            new SitePage("/password", "Password",
                "setting the password used to log into the game client"),

            new SitePage("/admin", "Admin overview",
                "live server state: players in game, streams running, whether the bot is responding",
                adminOnly: true),

            new SitePage("/admin/settings", "Server settings",
                "API keys and server options, including the assistant and vendor pricing",
                adminOnly: true),

            new SitePage("/admin/knowledge", "Knowledge",
                "what the assistant knows, corrections waiting for review, and gaps",
                adminOnly: true),

            new SitePage("/admin/news", "News",
                "writing and publishing announcements",
                adminOnly: true),

            new SitePage("/admin/players", "Players",
                "searching and inspecting any player",
                adminOnly: true),

            new SitePage("/admin/economy", "Game economy",
                "marketplace and vendor activity, coins in circulation",
                adminOnly: true),
        };

        public static IEnumerable<SitePage> For(bool isAdministrator) =>
            All.Where(x => !x.AdminOnly || isAdministrator);

        /// <summary>
        ///     Finds the page a request names, by path or by name.
        /// </summary>
        /// <remarks>
        ///     Only ever returns a page from the list, so the assistant cannot send anybody to an
        ///     address it invented. A path that is not here does not resolve, which is the right
        ///     failure: a button leading nowhere is worse than being told to look for it.
        /// </remarks>
        public static SitePage Resolve(string wanted, bool isAdministrator)
        {
            if (string.IsNullOrWhiteSpace(wanted)) return null;

            var text = wanted.Trim().TrimEnd('/');
            var pages = For(isAdministrator).ToList();

            return pages.FirstOrDefault(x => string.Equals(x.Path, text, StringComparison.OrdinalIgnoreCase))
                ?? pages.FirstOrDefault(x => string.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase))
                ?? pages.FirstOrDefault(x => x.Path.EndsWith("/" + text.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
        }
    }
}

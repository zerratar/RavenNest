using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>
    ///     Regenerates the facts that come from the code, and checks the ones that describe it.
    /// </summary>
    /// <remarks>
    ///     Runs once at startup, after the game data is loaded. Two jobs, and they are the two
    ///     halves of keeping a knowledge base honest as the game changes underneath it.
    ///
    ///     <para>
    ///     Derived facts are replaced wholesale. They read their numbers from the code every time,
    ///     so they cannot describe a rule that has changed, and one that no longer generates is one
    ///     whose rule was removed and which should therefore disappear rather than linger.
    ///     </para>
    ///
    ///     <para>
    ///     Hand written facts cannot be regenerated, because nothing can write their prose. Instead
    ///     each one may declare which code values its wording depends on and what they were at the
    ///     time. Those are read again here and compared. A mismatch marks the fact stale and says
    ///     what changed; it does not rewrite it, because no machine can do that safely, and it does
    ///     not hide it, because a slightly out of date answer still beats nothing while somebody
    ///     gets round to it.
    ///     </para>
    /// </remarks>
    public class KnowledgeRefresher : IHostedService
    {
        /// <summary>
        ///     Long enough for the game data to have finished loading. The derived facts read from
        ///     it, and a count of zero items would be a fact that is merely wrong.
        /// </summary>
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

        private readonly FactService facts;
        private readonly WikiImporter wiki;
        private readonly GameData gameData;
        private readonly ILogger<KnowledgeRefresher> logger;

        private Timer timer;

        public KnowledgeRefresher(FactService facts, WikiImporter wiki, GameData gameData, ILogger<KnowledgeRefresher> logger)
        {
            this.facts = facts;
            this.wiki = wiki;
            this.gameData = gameData;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(_ => Refresh(), null, StartDelay, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        private void Refresh()
        {
            try
            {
                var generated = DerivedFacts.Generate(gameData);
                facts.ReplaceDerived(generated);
                logger.LogInformation("Regenerated " + generated.Count + " facts from the code.");

                CheckDependencies();

                // The wiki is somebody else's server, so it is fetched after the local work rather
                // than before it. A wiki that is down should not stop the derived facts refreshing.
                _ = ImportWikiAsync();
            }
            catch (Exception exc)
            {
                // A knowledge base that failed to refresh is out of date. One that took the server
                // down with it is worse.
                logger.LogError("Could not refresh the knowledge base: " + exc);
            }
        }

        private async Task ImportWikiAsync()
        {
            try
            {
                await wiki.ImportAsync();
            }
            catch (Exception exc)
            {
                logger.LogError("Could not import the wiki: " + exc);
            }
        }

        /// <summary>
        ///     Flags hand written facts whose wording describes a value the code no longer holds.
        /// </summary>
        private void CheckDependencies()
        {
            var stale = 0;

            foreach (var fact in facts.GetByStatus(FactStatus.Published))
            {
                if (fact.DependsOn == null || fact.DependsOn.Count == 0) continue;
                if (fact.Source == FactSource.Derived) continue;

                foreach (var dependency in fact.DependsOn)
                {
                    var now = CodeValues.Read(dependency.Key);
                    if (now == null || now == dependency.Value) continue;

                    facts.MarkStale(fact.Id,
                        "Written when " + dependency.Key + " was " + dependency.Value +
                        ". It is now " + now + ".");

                    stale++;
                    break;
                }
            }

            if (stale > 0)
            {
                logger.LogWarning(stale + " fact(s) describe something in the code that has since " +
                                  "changed, and are flagged for review.");
            }
        }
    }
}

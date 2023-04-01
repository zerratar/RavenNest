using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models.Tv;
using Shinobytes.OpenAI;
using Shinobytes.OpenAI.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RavenNest.BusinessLogic.Tv
{
    public class RavenfallTvManager : IDisposable
    {
        private static readonly int MaxRequestsPerMinute = 30; // 60 is max, but I don't want to exhaust all my requests.
        private static readonly int MaxDegreeOfParallelism = Environment.ProcessorCount;
        private static readonly int MaxRetryAttempts = 3;

        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;
        private readonly OpenAISettings openAISettings;
        private readonly ILogger<RavenfallTvManager> logger;
        private readonly GameData gameData;
        private readonly Thread requestProcessThread;
        private readonly RavenfallTvEpisodePromptGenerator promptGenerator;
        private readonly JsonRepository<Episode> episodes;
        private readonly JsonRepository<GenerateUserEpisodeRequest> episodeRequests;
        private readonly ConcurrentQueue<GenerateUserEpisodeRequest> requestQueue;

        private bool disposed;

        // for generating new episodes if we don't have any
        private DateTime lastGenerateRequestTime;
        private GenerateEpisodeRequest lastGenerateRequest;
        private readonly int episodeLimit = 50;
        private readonly TimeSpan generateInterval = TimeSpan.FromMinutes(0.1);

        private readonly OpenAIClient openAI;
        private readonly TimeSpan throttlePeriod;
        private readonly TransformBlock<GenerateUserEpisodeRequest, Episode> throttler;

        public RavenfallTvManager(
            ILogger<RavenfallTvManager> logger,
            IOptions<OpenAISettings> openAISettings,
            GameData gameData)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = cancellationTokenSource.Token;

            this.openAISettings = openAISettings.Value;
            this.logger = logger;
            this.gameData = gameData;

            promptGenerator = new RavenfallTvEpisodePromptGenerator(gameData);
            episodes = new JsonRepository<Episode>("../ravenfall-tv/episodes/");
            episodeRequests = new JsonRepository<GenerateUserEpisodeRequest>("../ravenfall-tv/episode-requests/");

            requestQueue = new ConcurrentQueue<GenerateUserEpisodeRequest>(episodeRequests.OrderedBy(x => x.Created));
            openAI = new OpenAIClient(new OpenAITokenString(this.openAISettings.AccessToken));

            this.throttlePeriod = TimeSpan.FromMinutes(1) / MaxRequestsPerMinute;
            this.throttler = new TransformBlock<GenerateUserEpisodeRequest, Episode>(
                async request => await ProcessRequestAsync(request, cancellationToken),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                    BoundedCapacity = MaxRequestsPerMinute,
                    CancellationToken = cancellationToken
                });

            requestProcessThread = new Thread(ProcessRequests);
            requestProcessThread.Start();
        }

        private async void ProcessRequests()
        {
            while (!disposed)
            {
                if (requestQueue.TryDequeue(out var req))
                {
                    await throttler.SendAsync(req, cancellationToken);
                }
                else
                {
                    // every minute we should generate a new episode that has no real players in it, to ensure we have episodes at all.
                    // but only if we have less than 20 episodes.
                    var now = DateTime.UtcNow;
                    if (episodes.Count() < episodeLimit
                        && requestQueue.Count == 0
                        && (now - lastGenerateRequestTime) >= generateInterval)
                    {
                        lastGenerateRequestTime = now;

                        // make sure this has been generated before we request to generate more.
                        if (lastGenerateRequest != null && !episodes.Contains(lastGenerateRequest.Id))
                        {
                            await Task.Delay(throttlePeriod, cancellationToken);
                            continue;
                        }

                        lastGenerateRequest = new GenerateEpisodeRequest { Id = Guid.NewGuid() };
                        await GenerateEpisodeAsync(lastGenerateRequest);
                    }

                    await Task.Delay(throttlePeriod, cancellationToken);
                }
            }
        }

        private async Task<Episode> ProcessRequestAsync(GenerateUserEpisodeRequest request, CancellationToken cancellationToken)
        {
            // give me the implementation

            int retryCount = 0;
            Episode episode = null;
            while (retryCount < MaxRetryAttempts && episode == null)
            {
                try
                {
                    var result = await openAI.GetCompletionAsync(GetPrompt(request), cancellationToken).ConfigureAwait(false);
                    foreach (var choice in result.Choices)
                    {
                        var content = EnsureValidJson(choice.Message.Content);
                        episode = Newtonsoft.Json.JsonConvert.DeserializeObject<Episode>(content);
                        if (episode != null)
                        {
                            episode.Id = request.Request.Id != Guid.Empty ? request.Request.Id : Guid.NewGuid();
                            episode.Created = DateTime.UtcNow;
                            episode.Requested = request.Created;

                            foreach (var c in episode.Characters)
                            {
                                if (Guid.TryParse(c.Id, out var characterId) && gameData.GetCharacter(characterId) != null)
                                {
                                    c.IsReal = true;
                                }
                            }

                            await episodes.SaveAsync(episode.Id.Value, episode);
                            await episodeRequests.DeleteAsync(request.Request.Id);
                        }
                    }
                }
                catch
                {
                    retryCount++;
                    if (retryCount < MaxRetryAttempts)
                    {
                        // Add a delay between retries, with exponential backoff.
                        int delayMilliseconds = (int)Math.Pow(2, retryCount) * 1000;
                        await Task.Delay(delayMilliseconds, cancellationToken);
                    }
                }
            }
            return episode;
        }

        private string EnsureValidJson(string content)
        {
            while (content[0] == '\n') content = content.Substring(1);

            content = content
                .Replace("’", "'")
                .Replace("”", "\"")
                .Replace(" ( ", " ) ")
                .Trim();

            // Remove semicolons at the end of lines
            content = Regex.Replace(content, @";\s*\n", "\n");

            // Remove trailing commas after the last element in an object or array
            content = Regex.Replace(content, @",\s*([}\]])", "$1");

            return content;
        }

        private string GetPrompt(GenerateUserEpisodeRequest request)
        {
            return promptGenerator.Generate(request);
        }

        public void Dispose()
        {
            disposed = true;
            cancellationTokenSource.Cancel();
        }

        public async Task<EpisodeResult> GenerateEpisodeAsync(GenerateEpisodeRequest request)
        {
            var req = new GenerateUserEpisodeRequest { Request = request, UserId = Guid.Empty, Created = DateTime.UtcNow };
            requestQueue.Enqueue(req);
            await episodeRequests.SaveAsync(request.Id, req);

            return new EpisodeResult()
            {
                Id = request.Id,
                Status = EpisodeGenerationStatus.Generating,
            };
        }

        public async Task<EpisodeResult> GenerateEpisodeAsync(User user, GenerateEpisodeRequest request)
        {
            var req = new GenerateUserEpisodeRequest { Request = request, UserId = user.Id, Created = DateTime.UtcNow };
            requestQueue.Enqueue(req);
            await episodeRequests.SaveAsync(request.Id, req);

            return new EpisodeResult()
            {
                Id = request.Id,
                Status = EpisodeGenerationStatus.Generating,
            };
        }

        public async Task<EpisodeResult> GetEpisodeAsync(Guid episodeId)
        {
            if (episodes.Contains(episodeId))
            {
                return new EpisodeResult
                {
                    Episode = await episodes.GetAsync(episodeId),
                    Id = episodeId,
                    Status = EpisodeGenerationStatus.Completed,
                };
            }

            if (episodeRequests.Contains(episodeId))
            {
                return new EpisodeResult
                {
                    Id = episodeId,
                    Status = EpisodeGenerationStatus.Generating,
                };
            }

            return new EpisodeResult()
            {
                Id = episodeId,
                Status = EpisodeGenerationStatus.NotFound,
            };
        }

        public async Task<List<Episode>> GetEpisodesAsync(Guid userId, DateTime date, int take)
        {
            // Not very graceful, but still. Better than nothing
            var e = episodes.TakeWhereOrdered(x => x.Created >= date && x.UserId == userId, x => x.Created, take);
            if (e.Count < take)
            {
                var random = episodes.TakeRandomWhere(x => x.Created >= date && e.All(y => y.Id != x.Id), take - e.Count);
                if (random.Count > 0)
                    e.AddRange(random);
            }
            return e;
        }

        public async Task<List<Episode>> GetEpisodesAsync(DateTime date, int take)
        {
            return episodes.TakeWhereOrdered(x => x.Created >= date, x => x.Created, take);
        }
    }
}

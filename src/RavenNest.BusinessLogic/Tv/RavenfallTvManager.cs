using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models.Tv;
using Shinobytes.OpenAI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        private readonly IOpenAIClient openAI;
        private readonly TimeSpan throttlePeriod;
        private readonly TransformBlock<GenerateUserEpisodeRequest, Episode> throttler;

        public RavenfallTvManager(
            ILogger<RavenfallTvManager> logger,
            IOpenAIClient openAI,
            GameData gameData)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = cancellationTokenSource.Token;

            this.logger = logger;
            this.gameData = gameData;

            promptGenerator = new RavenfallTvEpisodePromptGenerator(gameData);
            episodes = new JsonRepository<Episode>("../ravenfall-tv/episodes/");
            episodeRequests = new JsonRepository<GenerateUserEpisodeRequest>("../ravenfall-tv/episode-requests/");

            requestQueue = new ConcurrentQueue<GenerateUserEpisodeRequest>(episodeRequests.OrderedBy(x => x.Created));
            
            this.openAI = openAI;
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
                    var choice = result.Choices.FirstOrDefault();
                    var content = EnsureValidJson(choice.Message.Content);
                    episode = Newtonsoft.Json.JsonConvert.DeserializeObject<Episode>(content);
                    if (episode != null)
                    {
                        episode.Id = request.Request.Id != Guid.Empty ? request.Request.Id : Guid.NewGuid();
                        episode.UserId = request.UserId != Guid.Empty ? request.UserId : null;
                        episode.Created = DateTime.UtcNow;
                        episode.Requested = request.Created;

                        AddMissingCharacters(episode);

                        foreach (var c in episode.Characters)
                        {
                            if (Guid.TryParse(c.Id, out var characterId) && gameData.GetCharacter(characterId) != null)
                            {
                                c.IsReal = true;
                            }
                        }

                        await episodes.SaveAsync(episode.Id.Value, episode);
                        await episodeRequests.DeleteAsync(request.Request.Id);
                        return episode;
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

            requestQueue.Enqueue(request);
            return null;
        }

        private static void AddMissingCharacters(Episode episode)
        {
            // sometimes, the characters list is not filled up with the characters in the dialogue.
            // needs to be fixed on server side. but for now, populate missing character list.
            var characterList = episode.Characters.ToList();
            foreach (var d in episode.Dialogues)
            {
                var existing = characterList.FirstOrDefault(x => x.Name.Equals(d.Character, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // this one exists, skip it.
                    continue;
                }
                if (string.IsNullOrEmpty(d.Character)
                    || d.Character.Equals("narrator", StringComparison.OrdinalIgnoreCase)
                    || d.Character.Equals("all", StringComparison.OrdinalIgnoreCase)
                    || d.Character.Equals("group", StringComparison.OrdinalIgnoreCase)
                    || d.Character.Equals("party", StringComparison.OrdinalIgnoreCase))
                {
                    // this is a narrator, all, etc. Ignore this one.
                    continue;
                }

                // this is a generated character for sure.
                // we can't really determine gender, so pick a random one.
                var newCharacter = new Episode.Character
                {
                    Id = GenerateCharacterId(characterList),
                    Name = d.Character,
                    Gender = Random.Shared.NextDouble() >= 0.5 ? "male" : "female",
                    Job = "Other",
                    Race = "Human",
                    Strength = Random.Shared.Next(1, 400),
                    IsReal = false,
                };

                characterList.Add(newCharacter);
            }
            episode.Characters = characterList.ToArray();
        }

        private static string GenerateCharacterId(List<Episode.Character> characterList)
        {
            var index = 1;
            string id = index.ToString();
            while (true)
            {
                if (characterList.Any(x => x.Id == id))
                {
                    id = (++index).ToString();
                    continue;
                }

                break;
            }

            return id;
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
            var e = episodes.TakeWhereOrdered(x => x.Created.GetValueOrDefault().Truncate(TimeSpan.TicksPerSecond) > date && x.UserId == userId, x => x.Created.GetValueOrDefault(), take);
            if (e.Count < take)
            {
                //var items = episodes.OrderedBy(x => x.Created).ToList();
                var potentialEpisodes = episodes.TakeWhereOrdered(x => x.Created.GetValueOrDefault().Truncate(TimeSpan.TicksPerSecond) > date && e.All(y => y.Id != x.Id), x => x.Created.GetValueOrDefault(), take - e.Count);
                if (potentialEpisodes.Count > 0)
                    e.AddRange(potentialEpisodes);
            }
            return e;
        }

        public async Task<List<Episode>> GetEpisodesAsync(DateTime date, int take)
        {
            return episodes.TakeWhereOrdered(x => x.Created >= date, x => x.Created, take);
        }

    }

    public static class DateTimeUtils
    {
        /// <summary>
        /// <para>Truncates a DateTime to a specified resolution.</para>
        /// <para>A convenient source for resolution is TimeSpan.TicksPerXXXX constants.</para>
        /// </summary>
        /// <param name="date">The DateTime object to truncate</param>
        /// <param name="resolution">e.g. to round to nearest second, TimeSpan.TicksPerSecond</param>
        /// <returns>Truncated DateTime</returns>
        public static DateTime Truncate(this DateTime date, long resolution)
        {
            return new DateTime(date.Ticks - (date.Ticks % resolution), date.Kind);
        }
    }
}

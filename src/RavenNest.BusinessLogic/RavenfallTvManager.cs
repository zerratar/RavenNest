using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models.Tv;
using Shinobytes.OpenAI;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class InternalEpisodeRequest
    {
        public GenerateEpisodeRequest Request { get; set; }
        public Guid UserId { get; set; }
    }

    public class RavenfallTvEpisodePromptGenerator
    {
        private readonly GameData gameData;

        public RavenfallTvEpisodePromptGenerator(GameData gameData)
        {
            this.gameData = gameData;
        }
        public string Generate(InternalEpisodeRequest req)
        {
            throw new NotImplementedException();
            //return null;
        }
    }


    public class RavenfallTvManager : IDisposable
    {
        private readonly OpenAISettings openAISettings;
        private readonly GameData gameData;
        private readonly Thread requestProcessThread;
        private readonly RavenfallTvEpisodePromptGenerator promptGenerator;
        private readonly JsonRepository<Episode> episodes;
        private readonly JsonRepository<InternalEpisodeRequest> episodeRequests;
        private bool disposed;

        private readonly OpenAIClient openAI;
        // add a repository for the episodes and their requests

        public RavenfallTvManager(
            IOptions<OpenAISettings> openAISettings,
            GameData gameData)
        {
            this.promptGenerator = new RavenfallTvEpisodePromptGenerator(gameData);
            this.episodes = new JsonRepository<Episode>("../ravenfall-tv/episodes/");
            this.episodeRequests = new JsonRepository<InternalEpisodeRequest>("../ravenfall-tv/episode-requests/");
            this.openAISettings = openAISettings.Value;
            this.gameData = gameData;

            this.openAI = new OpenAIClient(new OpenAITokenString(this.openAISettings.AccessToken));
            this.requestProcessThread = new System.Threading.Thread(ProcessRequests);
            this.requestProcessThread.Start();
        }

        private async void ProcessRequests()
        {
            while (!disposed)
            {
                var req = await episodeRequests.GetOneAsync();
                if (req == null)
                {
                    System.Threading.Thread.Sleep(500);
                    continue;
                }

                if (episodes.Contains(req.Request.Id))
                {
                    await episodeRequests.DeleteAsync(req.Request.Id);
                    System.Threading.Thread.Sleep(100);
                    continue;
                }

                var result = await openAI.GetCompletionAsync(GetPrompt(req));

                foreach (var choice in result.Choices)
                {
                    var content = EnsureValidJson(choice.Message.Content);

                    try
                    {
                        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<Episode>(content);
                        if (obj != null)
                        {
                            obj.Id = req.Request.Id;
                            await episodes.SaveAsync(obj.Id, obj);
                            await episodeRequests.DeleteAsync(req.Request.Id);
                        }
                    }
                    catch { }
                }
            }
        }

        private string EnsureValidJson(string content)
        {
            while (content[0] == '\n') content = content.Substring(1);
            return content
                .Replace("’", "'")
                .Replace("”", "\"")
                .Replace(" ( ", " ) ")
                .Trim();
        }


        private string GetPrompt(InternalEpisodeRequest request)
        {
            return promptGenerator.Generate(request);
        }

        public void Dispose()
        {
            this.disposed = true;
        }

        public async Task<EpisodeResult> GenerateEpisodeAsync(User user, GenerateEpisodeRequest request)
        {
            await this.episodeRequests.SaveAsync(request.Id, new InternalEpisodeRequest { Request = request, UserId = user.Id });

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
    }
}

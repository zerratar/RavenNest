using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class ServerService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IServerManager serverManager;

        public ServerService(
            IGameData gameData,
            IServerManager serverManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.serverManager = serverManager;
        }

        public BotStats GetBotStats()
        {
            return gameData.Bot;
        }

        public void SendServerAnnouncement(string message, int milliSeconds)
        {
            serverManager.BroadcastMessageAsync(message, milliSeconds);
        }

        public void SendExpMultiplierEvent(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            serverManager.SendExpMultiplierEventAsync(multiplier, message, startTime, endTime);
        }

        public Task<bool> DeleteCodeOfConduct()
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                var coc = agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
                if (coc != null)
                {
                    gameData.Remove(coc);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }


        public Task<Agreements> UpdateCodeOfConduct(UpdateCodeOfConduct data)
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                var coc = agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
                if (coc != null)
                {
                    if (coc.Message != data.Message || coc.Title != data.Title || coc.VisibleInClient != data.VisibleInClient)
                    {
                        coc.Message = data.Message;
                        coc.Title = data.Title;
                        coc.Revision = coc.Revision + 1;
                        coc.LastModified = DateTime.UtcNow;
                        coc.VisibleInClient = data.VisibleInClient;
                    }
                }
                else
                {
                    coc = new Agreements
                    {
                        Id = Guid.NewGuid(),
                        Type = "coc",
                        Message = data.Message,
                        Title = data.Title,
                        Revision = 1,
                        ValidFrom = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        VisibleInClient = data.VisibleInClient
                    };
                    gameData.Add(coc);
                }
                return coc;
            });
        }

        public Task<Agreements> GetCodeOfConductAsync()
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                return agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
            });
        }
    }

    public class UpdateCodeOfConduct
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool VisibleInClient { get; set; }
    }
}

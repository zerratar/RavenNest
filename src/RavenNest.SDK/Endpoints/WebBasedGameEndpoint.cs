﻿using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.SDK.Endpoints
{
    internal class WebBasedGameEndpoint : IGameEndpoint
    {
        private readonly IRavenNestClient client;
        private readonly ILogger logger;
        private readonly IApiRequestBuilderProvider request;

        public WebBasedGameEndpoint(IRavenNestClient client, ILogger logger, IApiRequestBuilderProvider request)
        {
            this.client = client;
            this.logger = logger;
            this.request = request;
        }

        public Task<GameInfo> GetAsync()
        {
            return request.Create().Build().SendAsync<GameInfo>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        public Task<SessionToken> BeginSessionAsync(
            string clientVersion,
            string accessKey,
            bool local,
            float syncTime)
        {
            return request.Create()
                .AddParameter(clientVersion)
                .AddParameter(accessKey)
                .AddParameter("value1", local)
                .AddParameter("value2", syncTime)
                .Build()
                .SendAsync<SessionToken>(ApiRequestTarget.Game, ApiRequestType.Post);
        }

        public Task AttachPlayersAsync(Guid[] ids)
        {
            return request.Create()
                .Method("attach")
                .AddParameter("values", ids)
                .Build()
                .SendAsync(ApiRequestTarget.Game, ApiRequestType.Post);
        }

        public Task<ScrollUseResult> ActivateRaidAsync(IPlayerController plr)
        {
            return request.Create()
                   .Method("use-scroll")
                   .AddParameter(plr.Id.ToString())
                   .AddParameter(ScrollType.Raid.ToString())
                   .Build()
                   .SendAsync<ScrollUseResult>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        public Task<ScrollUseResult> ActivateDungeonAsync(IPlayerController plr)
        {
            return request.Create()
                   .Method("use-scroll")
                   .AddParameter(plr.Id.ToString())
                   .AddParameter(ScrollType.Dungeon.ToString())
                   .Build()
                   .SendAsync<ScrollUseResult>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        public Task<ScrollUseResult> ActivateExpMultiplierAsync(IPlayerController plr)
        {
            return request.Create()
                   .Method("use-scroll")
                   .AddParameter(plr.Id.ToString())
                   .AddParameter(ScrollType.Experience.ToString())
                   .Build()
                   .SendAsync<ScrollUseResult>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        //public Task<bool> EndSessionAndRaidAsync(string username, bool war)
        //{
        //    return request.Create()
        //        .Method("raid")
        //        .AddParameter(username)
        //        .AddParameter("value", war)
        //        .Build()
        //        .SendAsync<bool>(ApiRequestTarget.Game, ApiRequestType.Post);
        //}

        public Task<bool> EndSessionAndRaidAsync(string username, bool war)
        {
            return request.Create()
                .Method("raid")
                .AddParameter(username)
                .AddParameter(war.ToString())
                .Build()
                .SendAsync<bool>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        public Task EndSessionAsync()
        {
            return request.Create()
                .Method("end")
                .Build()
                .SendAsync(ApiRequestTarget.Game, ApiRequestType.Get);
        }

        //public Task EndSessionAsync()
        //{
        //    return request.Create()
        //        .Build()
        //        .SendAsync(ApiRequestTarget.Game, ApiRequestType.Post);
        //}

        public Task<EventCollection> PollEventsAsync(int revision)
        {
            return request.Create()
                .Method("events")
                .AddParameter(revision.ToString())
                .Build()
                .SendAsync<EventCollection>(ApiRequestTarget.Game, ApiRequestType.Get);
        }

    }
}

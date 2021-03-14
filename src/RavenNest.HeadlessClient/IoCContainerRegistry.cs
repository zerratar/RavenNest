using Newtonsoft.Json;
using RavenNest.HeadlessClient.Core;
using RavenNest.HeadlessClient.Game;
using RavenNest.SDK;
using RavenNest.SDK.Endpoints;
using RavenNest.SDK.EventSystem;
using System;

namespace RavenNest.HeadlessClient
{
    public class IoCContainerRegistry : IDisposable
    {
        private readonly IIoC ioc;

        public IoCContainerRegistry(IIoC ioc, TargetEnvironment targetEnvironment)
        {
            this.ioc = ioc;
            SetupIoCContainer(targetEnvironment);
        }

        public void Dispose()
        {
            this.ioc.Dispose();
        }

        private void SetupIoCContainer(TargetEnvironment targetEnvironment)
        {
            const string settingsFile = "settings.json";
            if (System.IO.File.Exists(settingsFile))
            {
                var text = System.IO.File.ReadAllText(settingsFile);
                var settings = JsonConvert.DeserializeObject<AppSettings>(text);
                ioc.RegisterCustomShared<AppSettings>(() => settings);
            }
            else
            {
                ioc.RegisterCustomShared<AppSettings>(() => new AppSettings(null, null, null, null));
            }

            switch (targetEnvironment)
            {
                case TargetEnvironment.Production:
                    ioc.RegisterShared<IAppSettings, ProductionRavenNestStreamSettings>();
                    break;
                case TargetEnvironment.Staging:
                    ioc.RegisterShared<IAppSettings, StagingRavenNestStreamSettings>();
                    break;
                case TargetEnvironment.Local:
                    ioc.RegisterShared<IAppSettings, LocalRavenNestStreamSettings>();
                    break;
            }

            ioc.RegisterCustomShared<IIoC>(() => ioc);

            ioc.RegisterShared<IPlayerManager, PlayerManager>();
            ioc.RegisterShared<ILogger, SDK.ConsoleLogger>();
            ioc.RegisterShared<IGameCache, GameCache>();
            ioc.RegisterShared<IGameManager, GameManager>();
            ioc.RegisterShared<IRavenNestClient, RavenNestClient>();
            ioc.RegisterShared<IGameClient, GameClient>();
            ioc.RegisterShared<EventTriggerSystem, EventTriggerSystem>();
        }
    }
}

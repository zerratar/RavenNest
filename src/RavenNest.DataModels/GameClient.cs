﻿using System;

namespace RavenNest.DataModels
{
    public partial class GameClient : Entity<GameClient>
    {
        private string clientVersion; public string ClientVersion { get => clientVersion; set => Set(ref clientVersion, value); }
        private string accessKey; public string AccessKey { get => accessKey; set => Set(ref accessKey, value); }
        private string downloadLink; public string DownloadLink { get => downloadLink; set => Set(ref downloadLink, value); }
    }
}

using System;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs;
using RavenNest.BusinessLogic.Docs.Html;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Serializers;
using RavenNest.Health;
using RavenNest.Sessions;

namespace RavenNest
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var appSettingsSection = Configuration.GetSection("AppSettings");
            var appSettings = appSettingsSection.Get<AppSettings>();
            services.Configure<AppSettings>(appSettingsSection);
            services.AddLogging(loggingBuilder =>
            {
                var loggingDbContext = new RavenfallDbContextProvider(Options.Create(appSettings));
                loggingBuilder.AddProvider(new RavenfallDbLoggerProvider(loggingDbContext));
            });

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin());
                options.AddPolicy("AllowAllMethods", builder => builder.AllowAnyMethod());
                options.AddPolicy("AllowAllHeaders", builder => builder.AllowAnyHeader());
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(1);
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            RegisterServices(services);

            services.AddMvc().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.IgnoreNullValues = true;
            }).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.Configure<GzipCompressionProviderOptions>(options =>
                options.Level = System.IO.Compression.CompressionLevel.Optimal);

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.AddRavenNestHealthChecks(Configuration.GetSection("AppSettings"));
        }

        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime, IWebHostEnvironment env)
        {
            applicationLifetime.ApplicationStopping.Register(() => app.ApplicationServices.GetService<IGameData>().Flush());

            app.AddRequestTiming();

            app.UseCors(builder =>
                builder
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin());

            app.UseResponseCompression();
            app.UseSession();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.Map("/download/latest", builder =>
            {
                builder.Run(async context =>
                {
                    var gameData = app.ApplicationServices.GetService<IGameData>();
                    var client = gameData.Client;
                    var redirectUrl = gameData.Client.DownloadLink.Replace("update.7z", $"Ravenfall.v{client.ClientVersion}-alpha.7z");
                    context.Response.Redirect(redirectUrl);
                });
            });

            app.Map("/session-state.js", builder =>
            {
                builder.Run(async context =>
                {
                    var sessionInfo = new SessionInfo();
                    context.Response.ContentType = "text/javascript";
                    var service = app.ApplicationServices.GetService<ISessionInfoProvider>();

                    service.TryGet(context.Session, out sessionInfo);
                    context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
                    context.Response.Headers.Add("Expires", "-1");

                    await context.Response.WriteAsync($"var SessionState = {JSON.Stringify(sessionInfo)};");
                });
            });


            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4096
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRavenNestHealthChecks();
                endpoints.MapControllers();
                endpoints.MapFallbackToController("Index", "Home");
            });

            app.MapWhen(x => !x.WebSockets.IsWebSocketRequest && !x.Request.Path.Value.StartsWith("/api"), builder =>
            {
                builder.UseRouting();
                builder.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}");

                    endpoints.MapFallbackToController("Index", "Home");
                });
            });

            TryGenerateDocumentation(env);
        }

        private static void TryGenerateDocumentation(IWebHostEnvironment env)
        {
            try
            {
                var docGenerator = new DocumentGenerator();
                var settings = new DefaultGeneratorSettings
                {
                    OutputFolder = System.IO.Path.Combine(env.WebRootPath, "docs"),
                    Assembly = Assembly.GetExecutingAssembly()
                };

                var apiDocument = docGenerator.Generate(new DefaultDocumentSettings
                {
                    Name = "RavenNest API Documentation",
                    Version = "v1.0"
                }, settings);

                var processor = new HtmlDocumentProcessor();

                processor.ProcessAsync(settings, apiDocument).Wait();
            }
            catch { }
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IKernel, Kernel>();
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<ISecureHasher, SecureHasher>();
            services.AddSingleton<IBinarySerializer, CompressedJsonSerializer>();
            services.AddSingleton<IGamePacketSerializer, GamePacketSerializer>();

            services.AddSingleton<IPlayerManager, PlayerManager>();
            services.AddSingleton<IGameManager, GameManager>();
            services.AddSingleton<IMarketplaceManager, MarketplaceManager>();
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<IAuthManager, AuthManager>();
            services.AddSingleton<IItemManager, ItemManager>();
            services.AddSingleton<IAdminManager, AdminManager>();
            services.AddSingleton<IHighScoreManager, HighScoreManager>();
            services.AddSingleton<IServerManager, ServerManager>();
            services.AddSingleton<IGamePacketManager, GamePacketManager>();
            services.AddSingleton<IPatreonManager, PatreonManager>();
            services.AddSingleton<IVillageManager, VillageManager>();
            services.AddSingleton<IClanManager, ClanManager>();

            services.AddSingleton<IQueryBuilder, QueryBuilder>();
            services.AddSingleton<IItemResolver, ItemResolver>();
            services.AddSingleton<IIntegrityChecker, PlayerIntegrityChecker>();
            services.AddSingleton<ITwitchClient, TwitchClient>();

            services.AddSingleton<IGameData, GameData>();
            services.AddSingleton<IGameDataMigration, GameDataMigration>();
            services.AddSingleton<IGameDataBackupProvider, GameDataBackupProvider>();

            services.AddSingleton<IPlayerInventoryProvider, PlayerInventoryProvider>();
            services.AddSingleton<IPlayerHighscoreProvider, PlayerHighscoreProvider>();
            services.AddSingleton<IRavenfallDbContextProvider, RavenfallDbContextProvider>();
            services.AddSingleton<IWebSocketConnectionProvider, WebSocketConnectionProvider>();
            services.AddSingleton<ISessionInfoProvider, SessionInfoProvider>();
            services.AddSingleton<IPropertyProvider, MemoryCachedPropertyProvider>();
        }
    }
}

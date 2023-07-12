using Blazorise;
using Blazorise.Bootstrap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.Blazor.Data;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Github;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Serializers;
using RavenNest.BusinessLogic.Tv;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.Health;
using RavenNest.Sessions;
using System;
using System.Text.Json.Serialization;
using Blazorise.Icons.FontAwesome;
using RavenNest.BusinessLogic.Data.Aggregators;
using Shinobytes.OpenAI;

namespace RavenNest.Blazor
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var appSettingsSection = Configuration.GetSection("AppSettings");

            services.Configure<OpenAISettings>(Configuration.GetSection("OpenAI"));
            services.Configure<AppSettings>(appSettingsSection);
            services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull;
            });

            //services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            var appSettings = appSettingsSection.Get<AppSettings>();
            services.AddLogging(loggingBuilder =>
            {
                var loggingDbContext = new RavenfallDbContextProvider(Options.Create(appSettings));
                loggingBuilder.AddProvider(new RavenfallDbLoggerProvider(loggingDbContext));
            });

            //services.Configure<CookiePolicyOptions>(options =>
            //{
            //    options.CheckConsentNeeded = context => true;
            //    options.MinimumSameSitePolicy = SameSiteMode.None;
            //});
            services.AddMvc();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin());
                options.AddPolicy("AllowAllMethods", builder => builder.AllowAnyMethod());
                options.AddPolicy("AllowAllHeaders", builder => builder.AllowAnyHeader());
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(24);
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddHttpContextAccessor();
            services.AddResponseCaching();


            services
                .AddBlazorise(options =>
                        {
                            options.Immediate = true;
                        })
            .AddBootstrapProviders()
            .AddFontAwesomeIcons();

            RegisterServices(services);

            services.Configure<GzipCompressionProviderOptions>(options =>
              options.Level = System.IO.Compression.CompressionLevel.Optimal);

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.AddRavenNestHealthChecks(Configuration.GetSection("AppSettings"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime, IWebHostEnvironment env)
        {
            applicationLifetime.ApplicationStopping.Register(
                () => OnApplicaftionStopping(app));

            //app.UseCookiePolicy();

            //#if DEBUG
            //            app.AddRequestTiming();
            //#endif

            app.AddSessionCookies();

            app.UseCors(builder =>
                builder
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin());

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
#if DEBUG
            Console.WriteLine("Debug mode");
#endif

            app.UseResponseCompression();
            app.UseSession();

            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120)
            });

            app.UseHttpsRedirection();
            app.UseResponseCaching();

            app.UseRewriter(new RewriteOptions()
#if !DEBUG
               .AddRedirectToWww()
#endif
               .AddRedirectToHttps()
            );

            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseStaticFiles();

            //app.UseUseBlazorise(options => { options.DelayTextOnKeyPress = true; });

            /*
                Linux
             */
            app.Map("/download/linux", builder =>
            {
                builder.Run(async context =>
                {
                    var gameData = app.ApplicationServices.GetService<GameData>();
                    var release = await Github.GetGithubReleaseAsync();
                    var clientVersion = gameData.GetClientVersion();
                    var client = gameData.Client;

                    if (release != null && release.Version >= clientVersion)
                    {
                        if (client.ClientVersion != release.VersionString)
                        {
                            client.ClientVersion = release.VersionString;
                            client.DownloadLink = release.UpdateDownloadUrl_Win;
                        }

                        var url = release.UpdateDownloadUrl_Linux;
                        context.Response.Redirect(url);
                        return;
                    }

                    // fall back to saved settings in db.
                    var redirectUrl = gameData.Client.DownloadLink;
                    redirectUrl = redirectUrl.Replace("update-linux.7z", $"Ravenfall.v{client.ClientVersion}-alpha-linux.7z");
                    context.Response.Redirect(redirectUrl);
                });
            });


            /*
                Windows
             */
            app.Map("/download/latest", builder =>
            {
                builder.Run(async context =>
                {
                    var gameData = app.ApplicationServices.GetService<GameData>();
                    var release = await Github.GetGithubReleaseAsync();
                    var clientVersion = gameData.GetClientVersion();
                    var client = gameData.Client;

                    if (release != null && release.Version >= clientVersion)
                    {
                        if (client.ClientVersion != release.VersionString)
                        {
                            client.ClientVersion = release.VersionString;
                            client.DownloadLink = release.UpdateDownloadUrl_Win;
                        }

                        context.Response.Redirect(release.FullDownloadUrl_Win);
                        return;
                    }

                    // fall back to saved settings in db.
                    var redirectUrl = gameData.Client.DownloadLink;
                    redirectUrl = redirectUrl.Replace("update.7z", $"Ravenfall.v{client.ClientVersion}-alpha.7z");
                    context.Response.Redirect(redirectUrl);
                });
            });

            app.Map("/session-state.js", builder =>
            {
                builder.Run(async context =>
                {
                    var sessionInfo = new RavenNest.Models.SessionInfo();
                    context.Response.ContentType = "text/javascript";
                    var service = app.ApplicationServices.GetService<SessionInfoProvider>();

                    service.TryGet(context.GetSessionId(), out sessionInfo);
                    context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
                    context.Response.Headers.Add("Expires", "-1");

                    await context.Response.WriteAsync($"var SessionState = {JSON.Stringify(sessionInfo)};");
                });
            });


            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRavenNestHealthChecks();
                endpoints.MapBlazorHub();
                endpoints.MapControllers();
                endpoints.MapFallbackToPage("/_Host");
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

            // Start the TCP Api
            app.ApplicationServices.GetService<ITcpSocketApi>();

            // Start Generating Ravenfall Tv Episodes
            app.ApplicationServices.GetService<RavenfallTvManager>();

            // Start the report aggregators
            app.ApplicationServices.GetService<MarketplaceReportAggregator>();
            app.ApplicationServices.GetService<EconomyReportAggregator>();
        }

        private void OnApplicaftionStopping(IApplicationBuilder app)
        {
            Dispose<GameData>(app);
            Dispose<ITcpSocketApi>(app);
            Dispose<IGameProcessorManager>(app);
            Dispose<MarketplaceReportAggregator>(app);
            Dispose<EconomyReportAggregator>(app);
            Dispose<RavenfallTvManager>(app);
        }

        private void Dispose<T>(IApplicationBuilder app) where T : IDisposable
        {
            try
            {
                app.ApplicationServices.GetService<T>().Dispose();
            }
            catch { }
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<MarketplaceReportAggregator>();
            services.AddSingleton<EconomyReportAggregator>();

            services.AddSingleton<EconomyService>();
            services.AddSingleton<PatreonService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<PoQService>();
            services.AddSingleton<HighscoreService>();
            services.AddSingleton<ItemService>();
            services.AddSingleton<ClanService>();
            services.AddSingleton<LogoService>();
            services.AddSingleton<PlayerService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<TwitchService>();
            services.AddSingleton<NewsService>();
            services.AddSingleton<MarketplaceService>();
            services.AddSingleton<AccountService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<ServerService>();
            services.AddSingleton<AIAssistanceService>();
            services.AddSingleton<LoyaltyService>();
            services.AddSingleton<CookieService>();
            services.AddSingleton<TownService>();

            services.AddSingleton<IOpenAIRequestBuilderFactory, OpenAIRequestBuilderFactory>();
            services.AddSingleton<IOpenAIModelProvider, OpenAIModelProvider>();
            services.AddSingleton<IOpenAIClient, OpenAIClient>();
            services.AddSingleton<RavenfallTvManager>();

            services.AddSingleton<IRavenBotApiClient, RavenBotApiClient>();

            services.AddSingleton<IKernel, Kernel>();

            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMemoryFileCacheProvider, MemoryFileCacheProvider>();

            services.AddSingleton<ISecureHasher, SecureHasher>();
            services.AddSingleton<IBinarySerializer, CompressedJsonSerializer>();

            services.AddSingleton<PlayerManager>();
            services.AddSingleton<GameManager>();
            services.AddSingleton<MarketplaceManager>();
            services.AddSingleton<SessionManager>();
            services.AddSingleton<HighScoreManager>();

            services.AddSingleton<IAuthManager, AuthManager>();
            services.AddSingleton<IItemManager, ItemManager>();
            services.AddSingleton<AdminManager>();
            services.AddSingleton<IServerManager, ServerManager>();
            services.AddSingleton<VillageManager>();
            services.AddSingleton<IPatreonManager, PatreonManager>();
            services.AddSingleton<ClanManager>();
            services.AddSingleton<INotificationManager, NotificationManager>();

            services.AddSingleton<IQueryBuilder, QueryBuilder>();
            services.AddSingleton<IEntityResolver, EntityResolver>();
            services.AddSingleton<IIntegrityChecker, PlayerIntegrityChecker>();
            services.AddSingleton<ITwitchClient, TwitchClient>();

            services.AddSingleton<GameData>();
            services.AddSingleton<GameDataMigration>();
            services.AddSingleton<GameDataBackupProvider>();

            services.AddSingleton<EnchantmentManager>();
            services.AddSingleton<PlayerInventoryProvider>();
            services.AddSingleton<IPlayerHighscoreProvider, PlayerHighscoreProvider>();
            services.AddSingleton<IRavenfallDbContextProvider, RavenfallDbContextProvider>();
            services.AddSingleton<SessionInfoProvider>();
            services.AddSingleton<IPropertyProvider, MemoryCachedPropertyProvider>();


            services.AddSingleton<IGameProcessorManager, GameProcessorManager>();

            services.AddSingleton<ITcpSocketApiConnectionProvider, TcpSocketApiConnectionProvider>();
            services.AddSingleton<ITcpSocketApi, TcpSocketApi>();
            services.AddSingleton<ITwitchExtensionConnectionProvider, TwitchExtensionConnectionProvider>();
            services.AddSingleton<IExtensionPacketDataSerializer, JsonPacketDataSerializer>();
        }
    }
}

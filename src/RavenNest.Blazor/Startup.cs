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
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Serializers;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.Health;
using RavenNest.Sessions;
using System;
using System.Text.Json.Serialization;

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
            var appSettings = appSettingsSection.Get<AppSettings>();
            services.Configure<AppSettings>(appSettingsSection);
            services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull;
            });

            //services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

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
               .AddRedirectToWww()
               .AddRedirectToHttps()
            );

            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseStaticFiles();
            app.Map("/download/latest", builder =>
            {
                builder.Run(async context =>
                {
                    var gameData = app.ApplicationServices.GetService<IGameData>();
                    var client = gameData.Client;

                    string[] versions = client.ClientVersion.Split('.');
                    string major = versions[0];
                    string minor = versions[1];
                    string build = versions[2];
                    //string patch = versions[3]; //No longer being used
                    var redirectUrl = gameData.Client.DownloadLink;

                    if (minor == "8")
                    {
                        //v0.8.0.0a version is tagged as 0.8a - this is a workaround fix. Correct solution would be to rename the tag: https://phoenixnap.com/kb/git-rename-tag
                        //However, we're moving away from including patch number and only doing major.minor.build, instead of major.minor.build.patch
                        string rebuild = major + "." + minor + "a";
                        redirectUrl = redirectUrl.Replace("update.7z", $"Ravenfall.v{rebuild}-alpha.7z");
                    }
                    else
                    {
                        redirectUrl = redirectUrl.Replace("update.7z", $"Ravenfall.v{client.ClientVersion}-alpha.7z");
                    }


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
        }

        private void OnApplicaftionStopping(IApplicationBuilder app)
        {
            try
            {
                app.ApplicationServices.GetService<IGameData>().Flush();
            }
            catch { }

            try
            {
                app.ApplicationServices.GetService<ITcpSocketApi>().Dispose();
            }
            catch { }


        }

        private static void RegisterServices(IServiceCollection services)
        {
            // keep this one for now... LUL
            services.AddSingleton<WeatherForecastService>();

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
            services.AddSingleton<LoyaltyService>();
            services.AddSingleton<CookieService>();
            services.AddSingleton<TownService>();

            services.AddSingleton<IRavenBotApiClient, RavenBotApiClient>();

            services.AddSingleton<IKernel, Kernel>();

            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IMemoryFileCacheProvider, MemoryFileCacheProvider>();

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
            services.AddSingleton<IVillageManager, VillageManager>();
            services.AddSingleton<IPatreonManager, PatreonManager>();
            services.AddSingleton<IClanManager, ClanManager>();
            services.AddSingleton<INotificationManager, NotificationManager>();
            services.AddSingleton<ILoyaltyManager, LoyaltyManager>();

            services.AddSingleton<IQueryBuilder, QueryBuilder>();
            services.AddSingleton<IEntityResolver, EntityResolver>();
            services.AddSingleton<IIntegrityChecker, PlayerIntegrityChecker>();
            services.AddSingleton<ITwitchClient, TwitchClient>();

            services.AddSingleton<IGameData, GameData>();
            services.AddSingleton<IGameDataMigration, GameDataMigration>();
            services.AddSingleton<IGameDataBackupProvider, GameDataBackupProvider>();

            services.AddSingleton<IEnchantmentManager, EnchantmentManager>();
            services.AddSingleton<IPlayerInventoryProvider, PlayerInventoryProvider>();
            services.AddSingleton<IPlayerHighscoreProvider, PlayerHighscoreProvider>();
            services.AddSingleton<IRavenfallDbContextProvider, RavenfallDbContextProvider>();
            services.AddSingleton<ISessionInfoProvider, SessionInfoProvider>();
            services.AddSingleton<IPropertyProvider, MemoryCachedPropertyProvider>();


            services.AddSingleton<ITcpSocketApiConnectionProvider, TcpSocketApiConnectionProvider>();
            services.AddSingleton<ITcpSocketApi, TcpSocketApi>();
            services.AddSingleton<IGameWebSocketConnectionProvider, GameWebSocketConnectionProvider>();
            services.AddSingleton<IExtensionWebSocketConnectionProvider, ExtensionConnectionProvider>();
            services.AddSingleton<IExtensionPacketDataSerializer, JsonPacketDataSerializer>();
        }
    }
}

using System;
using System.IO;
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
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs;
using RavenNest.BusinessLogic.Docs.Html;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Serializers;
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
            // The following will configure the channel to use the given folder to temporarily
            // store telemetry items during network or Application Insights server issues.
            // User should ensure that the given folder already exists
            // and that the application has read/write permissions.

            //services.AddSingleton(typeof(ITelemetryChannel),
            //            new ServerTelemetryChannel() { StorageFolder = "/tmp/myfolder" });

            services.AddApplicationInsightsTelemetry();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder => builder.AllowAnyOrigin());
                options.AddPolicy("AllowAllMethods", builder => builder.AllowAnyMethod());
                options.AddPolicy("AllowAllHeaders", builder => builder.AllowAnyHeader());
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            RegisterServices(services);

            services.AddMvc().AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
                options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.Configure<GzipCompressionProviderOptions>(options =>
                options.Level = System.IO.Compression.CompressionLevel.Optimal);

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IApplicationLifetime applicationLifetime, IHostingEnvironment env)
        {

            applicationLifetime.ApplicationStopping.Register(() =>
            {
                app.ApplicationServices.GetService<IGameData>().Flush();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();

            }

            app.UseCors(builder =>
                builder
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin());

            app.UseResponseCompression();
            app.UseSession();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseHttpsRedirection();

            if (System.IO.Directory.Exists(System.IO.Path.Combine(env.WebRootPath, "/assets/build")))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "build")),
                    RequestPath = "/assets/build",
                    ContentTypeProvider = UnityContentTypeProvider()
                });
            }

            app.Map("/session-state.js", builder =>
            {
                builder.Run(async context =>
                {
                    var sessionInfo = new SessionInfo();
                    context.Response.ContentType = "text/javascript";
                    var service = app.ApplicationServices.GetService<ISessionInfoProvider>();
                    //var logger = app.ApplicationServices.GetService<ILogger>();
                    if (!service.TryGet(context.Session, out sessionInfo))
                    {
                        //await logger.WriteErrorAsync("Failed to get sessionInfo for session-state.js");
                    }

                    context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
                    context.Response.Headers.Add("Expires", "-1");

                    await context.Response.WriteAsync($"var SessionState = {JSON.Stringify(sessionInfo)};");
                });
            });


            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            });

            app.UseMvc();

            app.MapWhen(x => !x.WebSockets.IsWebSocketRequest && !x.Request.Path.Value.StartsWith("/api"), builder =>
            {
                builder.UseMvc(routes =>
                {
                    routes.MapSpaFallbackRoute(
                        name: "spa-fallback",
                        defaults: new { controller = "Home", action = "Index" });
                });
            });

            TryGenerateDocumentation(env);
        }

        private static void TryGenerateDocumentation(IHostingEnvironment env)
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

        private static FileExtensionContentTypeProvider UnityContentTypeProvider()
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".unityweb"] = "application/octet-stream";
            provider.Mappings[".mem"] = "application/octet-stream";
            provider.Mappings[".data"] = "application/octet-stream";
            provider.Mappings[".memgz"] = "application/octet-stream";
            provider.Mappings[".datagz"] = "application/octet-stream";
            provider.Mappings[".unity3dgz"] = "application/octet-stream";
            provider.Mappings[".jsgz"] = "application/x-javascript; charset=UTF-8";
            return provider;
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IMemoryCache, MemoryCache>();

            // Register Managers
            services.AddSingleton<IPlayerManager, PlayerManager>();
            services.AddSingleton<IGameManager, GameManager>();
            services.AddSingleton<IMarketplaceManager, MarketplaceManager>();
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<IKernel, Kernel>();
            services.AddSingleton<IAuthManager, AuthManager>();
            services.AddSingleton<IItemManager, ItemManager>();
            services.AddSingleton<IHighScoreManager, HighScoreManager>();
            services.AddSingleton<IServerManager, ServerManager>();
            services.AddSingleton<IGamePacketManager, GamePacketManager>();
            services.AddSingleton<IQueryBuilder, QueryBuilder>();
            services.AddSingleton<IGameData, GameData>();

            // Register providers
            services.AddSingleton<IRavenfallDbContextProvider, RavenfallDbContextProvider>();
            services.AddSingleton<IWebSocketConnectionProvider, WebSocketConnectionProvider>();
            services.AddSingleton<ISessionInfoProvider, SessionInfoProvider>();

            services.AddSingleton<ISecureHasher, SecureHasher>();
#if DEBUG
            services.AddSingleton<ILogger, ConsoleLogger>();
#else
            services.AddSingleton<ILogger, RavenfallDbLogger>();
#endif
            services.AddSingleton<IBinarySerializer, BinarySerializer>();
            services.AddSingleton<IGamePacketSerializer, GamePacketSerializer>();
        }
    }
}
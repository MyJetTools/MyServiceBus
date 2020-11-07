using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage;
using MyDependencies;
using MyServiceBus.Server.Grpc;
using MyServiceBus.Server.Tcp;
using MyServiceBus.Domains;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Persistence.AzureStorage;
using MyServiceBus.TcpContracts;
using MyTcpSockets;
using Prometheus;
using ProtoBuf.Grpc.Server;

namespace MyServiceBus.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(1);
        
        public static SettingsModel Settings { get; internal set; }

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddCodeFirstGrpc();
            
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc(o => { o.EnableEndpointRouting = false; })
                .AddNewtonsoftJson();
            
            

            services.AddSignalR()
                .AddMessagePackProtocol(options =>
                {
                    options.FormatterResolvers = new List<MessagePack.IFormatterResolver>
                    {
                        MessagePack.Resolvers.StandardResolver.Instance
                    };
                });

            services.AddSwaggerDocument(o => o.Title = "MyServiceBus");
            
            var ioc = new MyIoc();
            var serverMode = Settings.GetServerMode();
            ioc.Register<IMyServiceBusSettings>(Settings);
            ioc.RegisterMyNoServiceBusDomainServices(serverMode);

            if (serverMode == ServerMode.Server)
            {
                Console.WriteLine("Server is in SERVER mode");
                var cloudStorage = CloudStorageAccount.Parse(Settings.QueuesConnectionString);
                var messagesConnectionString = CloudStorageAccount.Parse(Settings.MessagesConnectionString);
                ioc.BindTopicsPersistentStorage(cloudStorage);
                ioc.BindMessagesPersistentStorage(messagesConnectionString);
                
                ioc.Register<IMessagesToPersistQueue, MessagesToPersistQueue>();

            }
            else
            {
                Console.WriteLine("Server is in PROXY mode");
                ioc.Register<IMessagesToPersistQueue, MessagesToPersistInProxyMode>();
            }


            ioc.BindServerServices();
            
            
            ServiceLocator.Init(ioc);
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime)
        {

            applicationLifetime.ApplicationStopping.Register(() =>
            {
                ServiceLocator.Stop();
                Console.WriteLine("Everything is stopped properly");
            });

            app.UseStaticFiles();

            app.UseOpenApi();
            app.UseSwaggerUi3();


            app.UseRouting();

            app.UseEndpoints(

                endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGrpcService<PublisherApi>();
                    endpoints.MapGrpcService<ManagementGrpcService>();
                    endpoints.MapMetrics();
                });

            ServiceLocator.Start();

        }


    }
}
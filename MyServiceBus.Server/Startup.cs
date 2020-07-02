﻿using System;
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

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddCodeFirstGrpc();
            var settings = SettingsReader.ReadSettings();
            
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc(o => { o.EnableEndpointRouting = false; })
                .AddNewtonsoftJson();

            services.AddSignalR()
                .AddMessagePackProtocol(options =>
                {
                    options.FormatterResolvers = new List<MessagePack.IFormatterResolver>()
                    {
                        MessagePack.Resolvers.StandardResolver.Instance
                    };
                });

            services.AddSwaggerDocument(o => o.Title = "MyServiceBus");
            
            var ioc = new MyIoc();

            ioc.Register<IMyServiceBusSettings>(settings);
            ioc.RegisterMyNoServiceBusDomainServices();

            var cloudStorage = CloudStorageAccount.Parse(settings.QueuesConnectionString);
            
            var messagesConnectionString = CloudStorageAccount.Parse(settings.MessagesConnectionString);

            ioc.BindTopicsPersistentStorage(cloudStorage);
            ioc.BindMessagesPersistentStorage(messagesConnectionString);
            ioc.Register<IMessagesToPersistQueue>(new MessagesToPersistQueue());
            
            ServiceLocatorApi.Init(ioc);
            ServiceLocatorApi.TcpServer    = new MyServerTcpSocket<IServiceBusTcpContract>(new IPEndPoint(IPAddress.Any, 6421))
                .RegisterSerializer(()=> new MyServiceBusTcpSerializer())
                .SetService(()=>new MyServiceBusTcpContext())
                .AddLog((ctx, data) =>
                {
                    if (ctx == null)
                    {
                        Console.WriteLine($"{DateTime.UtcNow}: "+data);    
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.UtcNow}: ClientId: {ctx.Id}. "+data);
                    }
                    
                }); 
            
            ServiceLocatorApi.TcpServer.Start();
            
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime)
        {

            applicationLifetime.ApplicationStopping.Register(()=>
            {
                
                ServiceLocatorApi.Stop();
                
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
                });

            ServiceLocatorApi.Start();
            
        }
        
  
    }
}
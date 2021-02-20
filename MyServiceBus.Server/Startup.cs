﻿using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus.Server.Grpc;
using MyServiceBus.Server.Tcp;
using MyServiceBus.Domains;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Server.Hubs;
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

        private IServiceCollection _services;

        public void ConfigureServices(IServiceCollection services)
        {
            
            _services = services;

            SocketMemoryUtils.AllocateByteArray = size => GC.AllocateUninitializedArray<byte>(size);
            MyServiceBusMemory.AllocateByteArray = SocketMemoryUtils.AllocateByteArray;
            
            services.AddCodeFirstGrpc();
            services.AddSignalR();
            var settings = MySettingsReader.SettingsReader.GetSettings<SettingsModel>(".myservicebus");
            
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc(o => { o.EnableEndpointRouting = false; })
                .AddNewtonsoftJson();

            services.AddSwaggerDocument(o => o.Title = "MyServiceBus");

            services.AddSingleton<IMyServiceBusSettings>(settings);
            services.RegisterMyNoServiceBusDomainServices();

            services.BindGrpcServices(settings.GrpcUrl);
            services.BindServerServices();
            
            services.AddSingleton<IMessagesToPersistQueue, MessagesToPersistQueue>();

            



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
                    endpoints.MapHub<MonitoringHub>("/monitoringhub");
                });

            var sp = _services.BuildServiceProvider();
                        
            ServiceLocator.Init(sp);
            ServiceLocator.TcpServer    = new MyServerTcpSocket<IServiceBusTcpContract>(new IPEndPoint(IPAddress.Any, 6421))
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

            ServiceLocator.TcpServer.Start();
            
            ServiceLocator.Start();

        }


    }
}
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Domains;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.Metrics;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Hubs;
using MyServiceBus.Server.Services;
using MyServiceBus.Server.Services.Sessions;
using MyServiceBus.Server.Tcp;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.Server
{
    public static class ServiceLocator
    {
        public static int TcpConnectionsSnapshotId { get; set; }
        
        static ServiceLocator()
        {
            StartedAt = DateTime.UtcNow;

            var name = Assembly.GetEntryAssembly()?.GetName();

            string appName = name?.Name ?? string.Empty;

            var nameSegments = appName.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (nameSegments.Length > 2)
            {
                appName = string.Join('.', nameSegments.Skip(1));
            }

            AppName = appName;
            AppVersion = name?.Version?.ToString();

            AspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Host = Environment.GetEnvironmentVariable("HOSTNAME");

            Console.WriteLine($"AppName: {AppName}");
            Console.WriteLine($"AppVersion: {AppVersion}");
            Console.WriteLine($"AspNetEnvironment: {AspNetEnvironment}");
            Console.WriteLine($"Host: {Host}");
            Console.WriteLine($"StartedAt: {StartedAt}");
            Console.WriteLine($"Port (http1 and http2): 6123");
            Console.WriteLine($"Port (http2): 6124");
            Console.WriteLine();
        }


        public static readonly ConnectionsLog ConnectionsLog = new ConnectionsLog();

        public static string AppName { get; }
        public static string AppVersion { get;}

        public static DateTime StartedAt { get; }

        public static string AspNetEnvironment { get;  }
        public static string Host { get; }
        public static TopicsManagement TopicsManagement { get; private set; }
        public static TopicsList TopicsList { get; private set; }
        public static GlobalVariables MyGlobalVariables { get; private set; }
        public static MyServiceBusPublisher MyServiceBusPublisher { get; private set; }
        public static MyServiceBusSubscriber Subscriber { get; private set; }
        public static MyServiceBusBackgroundExecutor MyServiceBusBackgroundExecutor { get; private set; }
        public static MyServerTcpSocket<IServiceBusTcpContract> TcpServer { get; internal set; }
        public static IMessagesToPersistQueue MessagesToPersistQueue { get; private set; }
        public static MessagesPerSecondByTopic MessagesPerSecondByTopic { get; private set; }
        public static GrpcSessionsList GrpcSessionsList { get; private set; }

        private static IMetricCollector _metricCollector;

        public static void Init(IServiceProvider serviceProvider)
        {

            GrpcSessionsList = serviceProvider.GetRequiredService<GrpcSessionsList>();
            
            MessagesPerSecondByTopic = serviceProvider.GetRequiredService<MessagesPerSecondByTopic>();
            
            TopicsManagement = serviceProvider.GetRequiredService<TopicsManagement>();
            TopicsList = serviceProvider.GetRequiredService<TopicsList>();
            
            MyGlobalVariables = serviceProvider.GetRequiredService<GlobalVariables>();

            MyServiceBusPublisher = serviceProvider.GetRequiredService<MyServiceBusPublisher>();
            Subscriber = serviceProvider.GetRequiredService<MyServiceBusSubscriber>();

            MessagesToPersistQueue = serviceProvider.GetRequiredService<IMessagesToPersistQueue>();
            
            MyServiceBusBackgroundExecutor = serviceProvider.GetRequiredService<MyServiceBusBackgroundExecutor>();

            _metricCollector = serviceProvider.GetRequiredService<IMetricCollector>();

            DataInitializer.InitAsync(serviceProvider).Wait();
        }
        
        
        private static readonly MyTaskTimer TimerGarbageCollector = new (1000);
        
        private static readonly MyTaskTimer TimerPersistent = new (1000);

        private static readonly MyTaskTimer TimerStatistic = new (1000);


        public static void Start()
        {

            TimerGarbageCollector.Register("GC or Warm up pages",
                MyServiceBusBackgroundExecutor.GcOrWarmupMessagesAndPushDelivery);

            TimerGarbageCollector.Register("Persist Topics And Queues",
                MyServiceBusBackgroundExecutor.PersistTopicsAndQueuesSnapshotAsync);

            TimerGarbageCollector.Register("Persist messages content",
                MyServiceBusBackgroundExecutor.PersistMessageContentAsync);

            
            TimerPersistent.Register("Sessions List GC",
                ()=>
                {
                    GrpcSessionsList.GarbageCollectSessions(DateTime.UtcNow);
                    return new ValueTask();
                });

            TimerStatistic.Register("Metrics timer", () =>
            {
                TopicsList.KickMetricsTimer();
                
                
                var connections = TcpServer.GetConnections();

                foreach (var connection in connections.Cast<MyServiceBusTcpContext>()
                    .Where(itm => itm.SessionContext != null))
                {
                    connection.SessionContext.OneSecondTimer();
                }

                foreach (var myTopic in TopicsList.Get())
                {
                    MessagesPerSecondByTopic.PutData(myTopic.TopicId, myTopic.MessagesPerSecond);
                    _metricCollector.UpdateTopicQueueSize(myTopic);

                    foreach (var queue in myTopic.GetQueues())
                    {
                        _metricCollector.UpdateTopicQueueSize(queue);
                    }
                }
                    

                return new ValueTask(MonitoringHub.BroadCasMetricsAsync());
            });
            
            
            TimerStatistic.Register("Prometheus", () =>
            {

                foreach (var topic in TopicsList.Get())
                {

                }
                
                
                return new ValueTask();
            });

            TimerGarbageCollector.Start();
            TimerPersistent.Start();
            TimerStatistic.Start();
        }


        private static void StopTimers()
        {
            var sw = new Stopwatch();
            Console.WriteLine("Stopping background timers");
            sw.Start();
            TimerPersistent.Stop();
            TimerGarbageCollector.Stop();
            TimerStatistic.Stop();
            sw.Stop();
            Console.WriteLine("Background timers are stopped in: "+sw.Elapsed);
        }


        public  static void Stop()
        {
            
            MyGlobalVariables.ShuttingDown = true;

            ServiceStopper.StopTcpServer();

            ServiceStopper.WaitingSessionsAreZero();
            
            StopTimers();
            
            ServiceStopper.PersistMessagesContentAsync().Wait();
            ServiceStopper.PersistQueueSnapshotAsync().Wait();
            
            Console.WriteLine("Waiting for produce requests are being finished");

            while (MyGlobalVariables.PublishRequestsAmountAreBeingProcessed > 0)
                Thread.Sleep(500);
        }
        
    }
}
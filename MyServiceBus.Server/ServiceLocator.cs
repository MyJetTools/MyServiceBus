using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyDependencies;
using MyServiceBus.Domains;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Metrics;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Tcp;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.Server
{
    public static class ServiceLocator
    {
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
            Console.WriteLine("Port (http1 and http2): "+Startup.Settings.ListenHttpPort);
            Console.WriteLine("Port (http2): "+Startup.Settings.ListenHttpPort);
            Console.WriteLine("Port (TCP): "+Startup.Settings.ListenTcpPort);
            Console.WriteLine();
        }

        public static string AppName { get; }
        public static string AppVersion { get; }

        public static DateTime StartedAt { get; }

        public static string AspNetEnvironment { get; }
        public static string Host { get; }
        public static SessionsList SessionsList { get; private set; }
        public static TopicsManagement TopicsManagement { get; private set; }
        public static TopicsList TopicsList { get; private set; }
        public static GlobalVariables MyGlobalVariables { get; private set; }
        public static MyServiceBusPublisher MyServiceBusPublisher { get; private set; }
        public static MyServiceBusSubscriber Subscriber { get; private set; }

        private static IMyServerBusBackgroundExecutor _myServiceBusBackgroundExecutor;
        public static IMessagesToPersistQueue MessagesToPersistQueue { get; private set; }
        public static MessageContentCacheByTopic CacheByTopic { get; private set; }
        
        public static readonly MessagesPerSecondByTopic MessagesPerSecondByTopic = new MessagesPerSecondByTopic();

        public static void Init(IServiceResolver serviceResolver)
        {
     
            TopicsManagement = serviceResolver.GetService<TopicsManagement>();
            TopicsList = serviceResolver.GetService<TopicsList>();
            
            MyGlobalVariables = serviceResolver.GetService<GlobalVariables>();

            MyServiceBusPublisher = serviceResolver.GetService<MyServiceBusPublisher>();
            Subscriber = serviceResolver.GetService<MyServiceBusSubscriber>();
            
            SessionsList = serviceResolver.GetService<SessionsList>();

            MessagesToPersistQueue = serviceResolver.GetService<IMessagesToPersistQueue>();
            
            _myServiceBusBackgroundExecutor = serviceResolver.GetService<MyServiceBusBackgroundExecutor>();

            CacheByTopic = serviceResolver.GetService<MessageContentCacheByTopic>();

            DataInitializer.InitAsync(serviceResolver).Wait();
            
            InitTcpListenPort();
        }
        
        
        private static readonly MyTaskTimer TimerGarbageCollector = new MyTaskTimer(1000);
        
        private static readonly MyTaskTimer TimerPersistent = new MyTaskTimer(1000);

        private static readonly MyTaskTimer TimerStatistic = new MyTaskTimer(1000);


        public static MyServerTcpSocket<IServiceBusTcpContract> TcpServer { get; private set; }
        private static void InitTcpListenPort()
        {
            TcpServer = new MyServerTcpSocket<IServiceBusTcpContract>(new IPEndPoint(IPAddress.Any, Startup.Settings.GetTcpPort()))
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
        }
        
        //If we are in Proxy Mode
        public static MyClientTcpSocket<IServiceBusTcpContract> TcpClient { get; private set; }


        private static void InitProxyTcpClient()
        {
            
        }
        
        public static void Start()
        {

            TcpServer.Start();
            
            TimerGarbageCollector.Register("Long pooling subscribers GarbageCollect",
                _myServiceBusBackgroundExecutor.GarbageCollect);
            
            TimerPersistent.Register("Long pooling subscribers Persist",
                _myServiceBusBackgroundExecutor.PersistAsync);

            TimerStatistic.Register("Topics timer", () =>
            {
                foreach (var myTopic in TopicsList.Get())
                    MessagesPerSecondByTopic.PutData(myTopic.TopicId, myTopic.MessagesPerSecond);

                TopicsList.Timer();
                SessionsList.Timer();
                return new ValueTask();
            });

            TimerGarbageCollector.Start();
            TimerPersistent.Start();
            TimerStatistic.Start();

        }


        public  static void Stop()
        {
            
            MyGlobalVariables.ShuttingDown = true;

            Console.WriteLine("Stopping background timers");

            TimerPersistent.Stop();
            TimerGarbageCollector.Stop();
            TimerStatistic.Stop();

            Console.WriteLine("Waiting for produce requests are being finished");

            while (MyGlobalVariables.PublishRequestsAmountAreBeingProcessed > 0)
                Thread.Sleep(500);

        }
        
    }
}
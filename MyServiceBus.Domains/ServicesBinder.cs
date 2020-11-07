using MyDependencies;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains
{
    
    public enum ServerMode
    {
        Server, Proxy
    }

    public static class ServicesBinder
    {
        public static void RegisterMyNoServiceBusDomainServices(this IServiceRegistrator sr, ServerMode serverMode)
        {
            
            sr.Register<TopicsList>();
            sr.Register<GlobalVariables>();
            
            sr.Register<MyServiceBusPublisher>();
            sr.Register<MyServiceBusSubscriber>();

            if (serverMode == ServerMode.Server)
            {
                sr.Register<IMyServerBusBackgroundExecutor, MyServiceBusBackgroundExecutor>();
            }
            else
            {
                sr.Register<IMyServerBusBackgroundExecutor, MyServiceBusBackgroundExecutor>();
            }
            
            sr.Register<MessageContentCacheByTopic>();
            
            sr.Register<TopicsAndQueuesPersistenceProcessor>();
            
            sr.Register<MessageContentPersistentProcessor>();
            
            sr.Register<MessageContentReader>();
            
            sr.Register<TopicsManagement>();
            
            sr.Register<SessionsList>();
            
            sr.Register<MyServiceBusDeliveryHandler>();
        }
        
    }
}
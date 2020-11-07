using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyDependencies;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains
{
    
    
    
    public static class DataInitializer
    {

        private static async Task InitTopicsAsync(IServiceResolver serviceResolver)
        {
            var topicsList = serviceResolver.GetService<TopicsList>();
            var storage = serviceResolver.GetService<ITopicPersistenceStorage>();

            var data = await storage.GetSnapshotAsync();

            topicsList.Restore(data);
        }
        


        private static async Task RestoreMessagesAsync(IServiceResolver serviceResolver)
        { 
            var topicsList = serviceResolver.GetService<TopicsList>();

            var persistentProcessor = serviceResolver.GetService<MessageContentPersistentProcessor>();

            var tasks = new List<Task>();
            foreach (var topic in topicsList.Get())
            {
                var activePages = topic.GetActiveMessagePages();
                var task = persistentProcessor.LoadActivePagesAsync(topic, activePages.Keys.ToList());
                tasks.Add(task);
            }


            await Task.WhenAll(tasks);

        }

        public static async Task InitAsync(IServiceResolver serviceResolver)
        {
            await InitTopicsAsync(serviceResolver);
            await RestoreMessagesAsync(serviceResolver);
        }
        
    }
}
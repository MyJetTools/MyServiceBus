using System.Threading.Tasks;
using MyServiceBus.Domains.Persistence;

namespace MyServiceBus.Domains.Topics
{
    public class TopicsManagement
    {
        private readonly TopicsList _topicsList;
        private readonly TopicsAndQueuesPersistenceProcessor _topicsAndQueuesPersistenceProcessor;


        public TopicsManagement(TopicsList topicsList, 
            TopicsAndQueuesPersistenceProcessor topicsAndQueuesPersistenceProcessor)
        {
            _topicsList = topicsList;
            _topicsAndQueuesPersistenceProcessor = topicsAndQueuesPersistenceProcessor;
        }

        public async ValueTask<MyTopic> AddIfNotExistsAsync(string topicId)
        {
            var topic = _topicsList.AddIfNotExists(topicId);
            await _topicsAndQueuesPersistenceProcessor.PersistTopicsAndQueuesInBackgroundAsync(_topicsList.Get());
            return topic;
        }
        
    }
}
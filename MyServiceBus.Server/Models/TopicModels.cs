using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Server.Models
{
    public class TopicInfoModel
    {
        public string Id { get; set;  }
        public long MessageId { get; set; }

        public static TopicInfoModel Create(MyTopic topic)
        {
            return new ()
            {
                Id = topic.TopicId,
                MessageId = topic.MessageIdGenerator.Value
            };
        }
        
    }
}
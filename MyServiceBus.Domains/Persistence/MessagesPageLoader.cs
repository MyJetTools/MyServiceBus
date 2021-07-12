using System;
using System.Threading.Tasks;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Persistence
{
    public class MessagesPageLoader
    {
        private readonly IMyServiceBusMessagesPersistenceGrpcService _messagesPersistenceGrpcService;

        public MessagesPageLoader(IMyServiceBusMessagesPersistenceGrpcService messagesPersistenceGrpcService)
        {
            _messagesPersistenceGrpcService = messagesPersistenceGrpcService;
        }


        public async Task<MessagesPage> LoadPageAsync(MyTopic topic, MessagesPageId pageId)
        {

            var attemptNo = 0;

            while (true)
            {
                if (attemptNo >= 5)
                {
                    var emptyPage = new MessagesPage(pageId);
                    topic.MessagesContentCache.UploadPage(emptyPage);
                    return emptyPage;
                }

                try
                {
                    Console.WriteLine($"Trying to restore page {pageId.Value} for topic {topic.TopicId}");

                    var page =
                        await _messagesPersistenceGrpcService.GetPageAsync(topic.TopicId, pageId.Value)
                            .ToPageInMemoryAsync(pageId);

                    topic.MessagesContentCache.UploadPage(page);
                    return page;
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        $"Count not load page {pageId} for topic {topic.TopicId}. Attempt: {attemptNo}. Message: " +
                        e.Message);

                    await Task.Delay(200);
                    attemptNo++;
                }

            }
        }
    }
}
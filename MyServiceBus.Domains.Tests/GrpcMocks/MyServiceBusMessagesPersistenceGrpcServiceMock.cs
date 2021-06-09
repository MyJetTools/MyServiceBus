using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Tests.GrpcMocks
{
    public class MyServiceBusMessagesPersistenceGrpcServiceMock : IMyServiceBusMessagesPersistenceGrpcService
    {
        private readonly Dictionary<string, Dictionary<long, MessageContentGrpcModel>> _messages
            = new Dictionary<string, Dictionary<long, MessageContentGrpcModel>>();

        private IReadOnlyList<MessageContentGrpcModel> GetPageAsync(string topicId, long pageId)
        {
            var result = new List<MessageContentGrpcModel>();
            
            lock (_messages)
            {
                if (!_messages.ContainsKey(topicId))
                    return result;

                var byTopic = _messages[topicId].Values;
                
                
                result.AddRange(byTopic.Where(itm => itm.MessageId.GetMessageContentPageId().Value == pageId));
            }

            return result;
        }

        public async ValueTask SaveMessagesAsync(IAsyncEnumerable<CompressedMessageChunkModel> request)
        {
            var saveMessagesContract = await request.DecompressAndMerge<SaveMessagesGrpcContract>();

            lock (_messages)
            {
                if (!_messages.ContainsKey(saveMessagesContract.TopicId))
                    _messages.Add(saveMessagesContract.TopicId, new Dictionary<long, MessageContentGrpcModel>());

                var messagesByTopic = _messages[saveMessagesContract.TopicId];

                foreach (var grpcMessage in saveMessagesContract.Messages)
                {
                    if (messagesByTopic.ContainsKey(grpcMessage.MessageId))
                        messagesByTopic[grpcMessage.MessageId] = grpcMessage;
                    else
                        messagesByTopic.Add(grpcMessage.MessageId, grpcMessage);
                }
            }
        }


        IAsyncEnumerable<CompressedMessageChunkModel> IMyServiceBusMessagesPersistenceGrpcService.GetPageCompressedAsync(GetMessagesPageGrpcRequest request)
        {
            var page = GetPageAsync(request.TopicId, request.PageNo);
            return page.CompressAndSplitAsync(1024 * 1024 * 3);
        }

        public ValueTask<MessageContentGrpcModel> GetMessageAsync(GetMessageGrpcRequest request)
        {
            lock (_messages)
            {
                if (!_messages.ContainsKey(request.TopicId))
                    return new ValueTask<MessageContentGrpcModel>();

                _messages[request.TopicId].TryGetValue(request.MessageId, out var value);
                return new ValueTask<MessageContentGrpcModel>(value);
            }
        }
    }
}
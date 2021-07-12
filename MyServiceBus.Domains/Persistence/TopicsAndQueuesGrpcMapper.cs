using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Persistence
{
    public static class TopicsAndQueuesGrpcMapper
    {
        private static QueueIndexRangeGrpcModel ToGrpcModel(this IQueueIndexRange src)
        {
            return new QueueIndexRangeGrpcModel()
            {
                FromId = src.FromId,
                ToId = src.ToId
            };
        }

        private static QueueTypePersistenceGrpcEnum ToGrpcModel(this TopicQueueType topicQueueType)
        {
            return topicQueueType switch
            {
                TopicQueueType.Permanent => QueueTypePersistenceGrpcEnum.Permanent,
                TopicQueueType.PermanentWithSingleConnection => QueueTypePersistenceGrpcEnum
                    .PermanentWithSingleConnection,
                _ => QueueTypePersistenceGrpcEnum.AutoDelete
            };
        }

        private static TopicQueueType ToDomain(this QueueTypePersistenceGrpcEnum src)
        {
            return src switch
            {
                QueueTypePersistenceGrpcEnum.Permanent => TopicQueueType.Permanent,
                QueueTypePersistenceGrpcEnum.PermanentWithSingleConnection => TopicQueueType
                    .PermanentWithSingleConnection,
                _ => TopicQueueType.DeleteOnDisconnect
            };
        }


        private static QueueSnapshotGrpcModel ToGrpcModel(this IQueueSnapshot src)
        {
            return new QueueSnapshotGrpcModel()
            {
                QueueId = src.QueueId,
                Ranges = src.Ranges.Select(itm => itm.ToGrpcModel()).ToArray(),
                QueueType = src.TopicQueueType.ToGrpcModel()
            };
        }
        


        private static TopicAndQueuesSnapshotGrpcModel ToGrpcModel(this ITopicPersistence src)
        {
            return new TopicAndQueuesSnapshotGrpcModel()
            {
                TopicId = src.TopicId,
                MessageId = src.MessageId,
                QueueSnapshots = src.QueueSnapshots.Select(itm => itm.ToGrpcModel()).ToArray(),
                
            };
        }
        

        public static  ValueTask SaveSnapshotAsync(this IMyServiceBusQueuePersistenceGrpcService grpcService,
            IEnumerable<ITopicPersistence> topics)
        {

            var grpcRequestContract = new SaveQueueSnapshotGrpcRequest
            {
                QueueSnapshot = topics.Select(topic => topic.ToGrpcModel()).ToArray(),
            }; 
            
            return grpcService.SaveSnapshotAsync(grpcRequestContract);
        }
        
  
        
        
        private static QueueIndexRangeReadOnly  ToDomain(this QueueIndexRangeGrpcModel src)
        {
            return new QueueIndexRangeReadOnly(src.FromId, src.ToId);
        }
        
        

        
        private static IQueueSnapshot ToDomain(this QueueSnapshotGrpcModel src)
        {
            return new QueueSnapshot
            {
                QueueId = src.QueueId,
                RangesData = src.Ranges?
                    .Select(itm => itm.ToDomain()).ToList() ?? Array.Empty<QueueIndexRangeReadOnly>() as IReadOnlyList<QueueIndexRangeReadOnly>,
                TopicQueueType = src.QueueType.ToDomain()
            };
        }


        private static ITopicPersistence ToDomain(this TopicAndQueuesSnapshotGrpcModel src)
        {
            return new TopicPersistence
            {
                TopicId = src.TopicId,
                MessageId = src.MessageId,
                QueueSnapshots = src.QueueSnapshots?.Select(itm => itm.ToDomain()).ToList() ?? Array.Empty<IQueueSnapshot>() as IReadOnlyList<IQueueSnapshot>
                
            };
        }


        public static async Task<IReadOnlyList<ITopicPersistence>> GetTopicsAndQueuesSnapshotAsync(
            this IMyServiceBusQueuePersistenceGrpcService grpcService)
        {
            var result = new List<ITopicPersistence>();
            
            await foreach (var grpcModel in grpcService.GetSnapshotAsync())
            {
                result.Add(grpcModel.ToDomain());
            }

            return result;
        }
        
    }
}
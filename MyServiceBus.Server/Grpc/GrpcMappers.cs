using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.GrpcContracts;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Server.Grpc
{
    public static class GrpcMappers
    {

        public static MessageContentMetaDataItem[] ToMessageMetaData(this IEnumerable<MessageHeaderGrpcModel> metadata)
        {
            if (metadata == null)
                return Array.Empty<MessageContentMetaDataItem>();

            return metadata.Select(itm => new MessageContentMetaDataItem
            {
                Key = itm.Key,
                Value = itm.Value
            }).ToArray();
        }
        
    }
}
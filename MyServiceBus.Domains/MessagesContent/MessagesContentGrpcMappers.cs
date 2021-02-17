using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.MessagesContent
{
    public static class MessagesContentGrpcMappers
    {

        

        public static async ValueTask<MessagesPageInMemory> ToPageInMemoryAsync(this IAsyncEnumerable<MessageContentGrpcModel> pageMessagesAsync, MessagesPageId pageId)
        {
            var result = new MessagesPageInMemory(pageId);

            await foreach (var grpcMessage in pageMessagesAsync)
            {
                result.Add(grpcMessage);
            }

            return result;
        }
        
    }
}
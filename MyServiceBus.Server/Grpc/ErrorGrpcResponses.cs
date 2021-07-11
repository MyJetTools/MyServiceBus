
using MyServiceBus.GrpcContracts;

namespace MyServiceBus.Server.Grpc
{
    public static class GrpcResponses
    {
        public static readonly MyServiceBusGrpcResponse OkResponse = new()
        {
            Status  = MyServiceBusResponseStatus.Ok
        };
        
        public static readonly MyServiceBusGrpcResponse TopicNotFoundGrpcResponse = new()
        {
            Status  = MyServiceBusResponseStatus.TopicNotFound
        };
        
        public static readonly MyServiceBusGrpcResponse InvalidSession = new ()
        {
            Status  = MyServiceBusResponseStatus.InvalidSession
        };
        


        public static readonly MyServiceBusGrpcResponse SubscribeTopicNotFound = new ()
        {
            Status  = MyServiceBusResponseStatus.TopicNotFound
        };
    }
}
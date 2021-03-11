using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Grpc;
using MyServiceBus.Grpc.Contracts;
using MyServiceBus.Grpc.Models;

namespace MyServiceBus.Server.Grpc
{
    public class PublisherApi : ControllerBase, IPublisherGrpcService
    {
        public async ValueTask<PublishMessageGrpcResponse> PublishMessageAsync(PublishMessageGrpcRequest request)
        {

            GrpcExtensions.GrpcPreExecutionCheck();
            
            var now = DateTime.UtcNow;

            var session = ServiceLocator.GrpcSessionsList.TryGetSession(request.SessionId, now);

            if (session == null)
                return ErrorGrpcResponses.SessionExpired;

            var messagesToPublish 
                = request.Messages.Select(itm => (itm, (IReadOnlyDictionary<string, string>)null));

            var response = await ServiceLocator
                .MyServiceBusPublisher
                .PublishAsync(session.SessionContext, request.TopicId, messagesToPublish, now, request.PersistImmediately);

            if (response == ExecutionResult.TopicNotFound)
            {
                Console.WriteLine($"Attempt to write to Topic {request.TopicId} which does not exist. Disconnecting session for app: "+session.Name);
                return ErrorGrpcResponses.TopicNotFoundGrpcResponse;
            }


            return new PublishMessageGrpcResponse
            {
                Status = GrpcResponseStatus.Ok,
            };

        }
    }
}
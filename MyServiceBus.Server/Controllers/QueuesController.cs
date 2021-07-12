using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Domains.QueueSubscribers;

namespace MyServiceBus.Server.Controllers
{
    
    public class QueuesController : Controller
    {
        [HttpDelete("/Queues/")]
        public IActionResult Delete([FromQuery][Required]string topicId, [FromQuery][Required]string queueId)
        {
            ServiceLocator.SubscriberOperations.DeleteQueue(topicId, queueId);

            return Content("Ok");
        }

        [HttpPost("/Queues/SetMessageId")]
        public IActionResult SetMessageId([FromQuery] [Required] string topicId, [FromQuery] [Required] string queueId,
            [FromQuery] [Required] long messageId)
        {
            var topic = ServiceLocator.TopicsList.TryGet(topicId);

            if (topic == null)
                return Conflict($"Topic {topicId} is not found");

            var topicQueue = topic.Queues.TryGetQueue(queueId);
            
            if (topicQueue == null)
                return Conflict($"Topic {topicId} is not found");

            ServiceLocator.SubscriberOperations.ReplayMessageAsync(topicQueue, messageId);

            return Content("Ok");
        }

        
        private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);
        [HttpPost("/Queues/PushMessageAgain")]
        public IActionResult PushMessageAgain([FromQuery] [Required] string topicId, 
            [FromQuery] [Required] string queueId)
        {
            var topic = ServiceLocator.TopicsList.TryGet(topicId);

            if (topic == null)
                return Conflict($"Topic {topicId} is not found");

            var queue = topic.Queues.TryGetQueue(queueId);
            
            if (queue == null)
                    return Conflict($"Queue {queueId} is not found");


            var result = new StringBuilder();



            foreach (var subscriber in queue.GetRwAccess(rwAccess => rwAccess.SubscribersList.GetSubscribers()))
            {
                if (subscriber.Status == SubscriberStatus.OnDelivery &&
                    DateTime.UtcNow - subscriber.OnDeliveryStart > TenSeconds)
                {
                    subscriber.Session.SendMessagesToSubscriber(subscriber);

                    result.AppendLine("Push message to the subscriber: " + subscriber.Session.Id);
                }
            }

            return Content(result.ToString());
        }
    }
}
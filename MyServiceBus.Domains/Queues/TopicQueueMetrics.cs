using System;

namespace MyServiceBus.Domains.Queues
{
    public class TopicQueueMetrics
    {
        
        //ToDo - Plug Data Update to this Field
        public int MessagesOnDeliveryAmount { get; set; } 
        
        
        //ToDo - Plug It
        public DateTime LastDisconnect { get; set; }
        
    }
}
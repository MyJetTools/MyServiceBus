using System.Collections.Generic;
using MyServiceBus.Abstractions.QueueIndex;

namespace MyServiceBus.TcpClient;

public class MessageDeDublicator
{
    private readonly QueueWithIntervals _intervals = new ();


    public bool HasMessage(long messageId)
    {
        return _intervals.HasMessage(messageId);
    }


    public void NewConfirmedMessages(IEnumerable<long> messages)
    {
        foreach (var msgId in messages)
        {
            _intervals.Enqueue(msgId);
        }
    }
    
    
}
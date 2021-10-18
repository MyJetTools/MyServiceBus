using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.TcpContracts;

namespace MyServiceBus.TcpClient
{

    
    public class SubscriberInfo
    {
        private readonly IMyServiceBusLogInvoker _log;


        private readonly MessageDeDublicator _messageDeDublicator;

        public SubscriberInfo(IMyServiceBusLogInvoker log, string topicId, string queueId, TopicQueueType queueType, 
            Func<IMyServiceBusMessage, ValueTask> callbackAsOneMessage,
            Func<IConfirmationContext, IReadOnlyList<IMyServiceBusMessage>, ValueTask> callbackAsAPackage, bool enableDedublicator)
        {
            _log = log;
            TopicId = topicId;
            QueueId = queueId;
            QueueType = queueType;
            CallbackAsOneMessage = callbackAsOneMessage;
            CallbackAsAPackage = callbackAsAPackage;
            
            if (enableDedublicator)
            {
                _messageDeDublicator = new MessageDeDublicator();
            }
        }

        public string TopicId { get; }
        public string QueueId { get; }
        public TopicQueueType QueueType { get; }
        public Func<IMyServiceBusMessage, ValueTask> CallbackAsOneMessage { get; }
        public Func<IConfirmationContext, IReadOnlyList<IMyServiceBusMessage>, ValueTask> CallbackAsAPackage { get; }
        

        private async Task InvokeOneByOne(IReadOnlyList<NewMessagesContract.NewMessageData> messages,
            Action confirmAllOk, Action confirmAllReject, Action<QueueWithIntervals> confirmSomeMessagesAreOk)
        {
            
            if (CallbackAsOneMessage == null)
                return;
            
            QueueWithIntervals okMessages = null;

            try
            {
                foreach (var message in messages)
                {

                    if (_messageDeDublicator != null)
                    {
                        if (_messageDeDublicator.HasMessage(message.Id))
                        {
                            okMessages ??= new QueueWithIntervals();
                            okMessages.Enqueue(message.Id);
                            continue;
                        }
                            
                    }

                    await CallbackAsOneMessage(message);

                    okMessages ??= new QueueWithIntervals();
                    okMessages.Enqueue(message.Id);
                }
            }
            catch (Exception e)
            {
                _log.InvokeLogException(e);
                if (okMessages == null)
                    confirmAllReject();
                else
                    confirmSomeMessagesAreOk(okMessages);
                    
                return;
            }

            confirmAllOk();
        }

        private async Task InvokeBulkCallback(IConfirmationContext confirmationContext, IReadOnlyList<NewMessagesContract.NewMessageData> messages, 
            Action confirmAllOk, Action confirmAllReject)
        {
            if (CallbackAsAPackage == null)
                return;

            if (_messageDeDublicator != null)
                messages = messages.Where(msg => _messageDeDublicator.HasMessage(msg.Id)).ToList();

            try
            {
                await CallbackAsAPackage(confirmationContext, messages);
                _messageDeDublicator?.NewConfirmedMessages(messages.Select(itm => itm.Id));
                confirmAllOk();
            }
            catch (Exception e)
            {                
                _log.InvokeLogException(e);
                confirmAllReject();
            }

        }

        public void InvokeNewMessages(IConfirmationContext confirmationContext, IReadOnlyList<NewMessagesContract.NewMessageData> messages,
            Action confirmAllOk, Action confirmAllReject, 
            Action<QueueWithIntervals> confirmSomeMessagesAreOk)
        {
            #pragma warning disable
            InvokeOneByOne(messages, confirmAllOk, confirmAllReject, confirmSomeMessagesAreOk);
            InvokeBulkCallback(confirmationContext, messages,confirmAllOk, confirmAllReject);
            #pragma warning restore
        }

    }
}
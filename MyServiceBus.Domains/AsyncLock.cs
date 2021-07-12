using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyServiceBus.Domains
{

    public readonly struct LockHandler : IDisposable
    {
        private readonly AsyncLock _lockObject;

        public LockHandler(AsyncLock lockObject)
        {
            _lockObject = lockObject;
        }
        
        public void Dispose()
        {
            _lockObject.Unlock();
        }
    }
    
    
    public class AsyncLock
    {
        private int _lockAmount;

        private readonly object _lockObject;

        private readonly Queue<TaskCompletionSource<LockHandler>> _awaitingLocks = new Queue<TaskCompletionSource<LockHandler>>();

        public AsyncLock(object lockObject)
        {
            _lockObject = lockObject;
        }
        
        public ValueTask<LockHandler> LockAsync()
        {
            lock (_lockObject)
            {
                if (_lockAmount == 0)
                {
                    _lockAmount++;
                    return new ValueTask<LockHandler>(new LockHandler(this));
                }

                var awaitingLock = new TaskCompletionSource<LockHandler>();
                _awaitingLocks.Enqueue(awaitingLock);
                return new ValueTask<LockHandler>(awaitingLock.Task);
            }
        }

        internal void Unlock()
        {
            TaskCompletionSource<LockHandler> result = null; 
            lock (_lockObject)
            {
                _lockAmount--;
                if (_awaitingLocks.Count > 0)
                    result = _awaitingLocks.Dequeue();
            }

            result?.SetResult(new LockHandler(this));
        }
    }
}
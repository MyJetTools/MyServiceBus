using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyServiceBus.TcpClient
{
    
    public class PayloadPackage
    {

        public long RequestId { get; }
        public bool ImmediatelyPersist { get; private set; }

        public bool OnPublishing { get; set; }

        public PayloadPackage(long requestId)
        {
            RequestId = requestId;
        }

        private readonly TaskCompletionSource<int> _commitTask = new();

        public Task GetTask()
        {
            return _commitTask.Task;
        }

        public void CommitTask()
        {
            _commitTask.SetResult(0);
        }

        public void Disconnect()
        {
            _commitTask.TrySetException(new Exception("Disconnected"));
        }

        private readonly List<byte[]> _payLoads = new();

        public IReadOnlyList<byte[]> PayLoads => _payLoads;

        public void Add(IEnumerable<byte[]> payLoads, bool immediatelyPersist)
        {
            if (immediatelyPersist)
                ImmediatelyPersist = true;

            foreach (var payLoad in payLoads)
            {
                _payLoads.Add(payLoad);
                PayLoadSize += payLoad.Length;
            }
        }

        public void Add(byte[] payLoad, bool immediatelyPersist)
        {
            if (immediatelyPersist)
                ImmediatelyPersist = true;

            _payLoads.Add(payLoad);
            PayLoadSize += payLoad.Length;
        }

        public long PayLoadSize { get; private set; }
    }

    public class PayloadPackagesList
    {
        private readonly int _maxPayloadSize;

        private Func<long> _getNextRequestId;


        public PayloadPackagesList(int maxPayloadSize, Func<long> getNextRequestId)
        {
            _maxPayloadSize = maxPayloadSize;
            _getNextRequestId = getNextRequestId;
        }

        private List<PayloadPackage> _items = new ();

        private PayloadPackage AddPayload()
        {
            var result = new PayloadPackage(_getNextRequestId());
            _items.Add(result);
            return result;
        }

        public PayloadPackage GetNextPayloadPackageToPost()
        {

            if (_items.Count == 0)
                return AddPayload();
            
            var lastPayload = _items[^1];

            if (lastPayload.PayLoadSize >= _maxPayloadSize || lastPayload.OnPublishing)
                return AddPayload();
            
            
            if (lastPayload.OnPublishing)
                return AddPayload();

            return lastPayload;
        }


        public PayloadPackage GetNextPayloadPackageToDeliver()
        {
            if (_items.Count == 0)
                return null;

            var result = _items[0];

            if (result.OnPublishing)
                return null;

            result.OnPublishing = true;

            return result;
        }

        public PayloadPackage TryToCommit(long requestId)
        {
            if (_items.Count == 0)
                return null;

            if (_items[0].RequestId != requestId)
                return null;

            var result = _items[0];
            _items.RemoveAt(0);
            result.CommitTask();
            return result;
        }

        public IReadOnlyList<PayloadPackage> ClearAll()
        {
            var result = _items;
            _items = null;
            return result;
        }


        public int Count => _items.Count;


    }
}
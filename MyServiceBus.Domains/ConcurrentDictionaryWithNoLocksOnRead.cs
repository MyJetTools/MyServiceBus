using System;
using System.Collections.Generic;
using System.Linq;

namespace MyServiceBus.Domains
{
    public class ConcurrentDictionaryWithNoLocksOnRead<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private IReadOnlyList<TValue> _itemsAsList = Array.Empty<TValue>();
        private readonly object _lockObject = new object();
        
        public int SnapshotId { get; private set; }

        public TValue Add(TKey key, Func<TValue> getValue)
        {
            lock (_lockObject)
            {

                var (added, newDictionary, value) = _dictionary.AddIfNotExistsByCreatingNewDictionary(key, getValue);

                if (added)
                {
                    _dictionary = newDictionary;
                    _itemsAsList = _dictionary.Values.ToList();
                }
                
                SnapshotId++;
                return value;
            }
        }

        public void AddBulk(IEnumerable<TValue> values, Func<TValue, TKey> getKey)
        {
            lock (_lockObject)
            {

                var resultDictionary = new Dictionary<TKey, TValue>(_dictionary);

                foreach (var value in values)
                    resultDictionary.Add(getKey(value), value);

                _dictionary = resultDictionary;
                _itemsAsList = _dictionary.Values.ToList();
                SnapshotId++;
            }
        }


        public IReadOnlyList<TValue> GetAllValues()
        {
            return _itemsAsList;
        }

        public (IReadOnlyList<TValue> Values, int SnapshotId) GetAllValuesWithSnapshot()
        {
            lock (_lockObject)
            {
                return (_itemsAsList, SnapshotId);
            }
        }


        public TValue this[TKey key] => _dictionary[key];

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }
        
        public TValue TryGetValueOrDefault(TKey key)
        {
            return _dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public TValue TryRemoveOrDefault(TKey key)
        {
            lock (_lockObject)
            {
                if (_dictionary.Remove(key, out var result))
                {
                    _itemsAsList = _dictionary.Values.ToList();
                    return result;
                }

                return default;
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock (_lockObject)
            {
                return _dictionary.ContainsKey(key);
            }
        }

        public int Count => _itemsAsList.Count;

    }
}
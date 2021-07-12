using System;
using System.Collections.Generic;
using System.Linq;

namespace MyServiceBus.Domains
{
    public class DictionaryWithList<TKey, TValue>
    {

        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private IReadOnlyList<TValue> _itemsAsList = Array.Empty<TValue>();
        
        public int SnapshotId { get; private set; }

        public void Add(TKey key, TValue value)
        {

            _dictionary.Add(key, value);
            _itemsAsList = _dictionary.Values.ToList();
            SnapshotId++;
        }

        public TValue TryGetValue(TKey key)
        {
            if (_dictionary.TryGetValue(key, out var result))
                return result;

            return default;
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public bool Remove(TKey key)
        {

            var result = _dictionary.Remove(key);
            if (result)
            {
                _itemsAsList = _dictionary.Values.ToList();
                SnapshotId++;
            }

            return result;
        }

        public TValue TryRemoveOrDefault(TKey key)
        {
            if (_dictionary.Remove(key, out var result))
            {
                _itemsAsList = _dictionary.Values.ToList();
                SnapshotId++;
                return result;
            }

            return default;
        }

        public IReadOnlyList<TValue> GetAllValues()
        {
            return _itemsAsList;
        }

        public TValue this[TKey key] => _dictionary[key];

        public int Count => _itemsAsList.Count;
    }

}
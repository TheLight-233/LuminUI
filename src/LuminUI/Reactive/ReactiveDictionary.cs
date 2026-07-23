using System;
using System.Collections;
using System.Collections.Generic;

namespace LuminUI
{
    /// <summary>
    /// 增量响应式字典。预设足够容量后，查询、替换及变化通知热路径不产生托管分配。
    /// </summary>
    public sealed class ReactiveDictionary<TKey, TValue> : IReadOnlyReactiveDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary;
        private readonly IEqualityComparer<TValue> _valueComparer;

        private Action<TKey, TValue>? _onAdd;
        private Action<TKey, TValue>? _onRemove;
        private Action<TKey, TValue, TValue>? _onReplace;
        private Action? _onClear;

        public ReactiveDictionary()
            : this(0, null, null) { }

        public ReactiveDictionary(int capacity)
            : this(capacity, null, null) { }

        public ReactiveDictionary(
            int capacity,
            IEqualityComparer<TKey>? keyComparer,
            IEqualityComparer<TValue>? valueComparer)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _dictionary = new Dictionary<TKey, TValue>(capacity, keyComparer);
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
        }

        public int Count => _dictionary.Count;
        public int Version { get; private set; }
        public Dictionary<TKey, TValue>.KeyCollection Keys => _dictionary.Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values => _dictionary.Values;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _dictionary.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _dictionary.Values;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                if (_dictionary.TryGetValue(key, out var oldValue))
                {
                    if (_valueComparer.Equals(oldValue, value)) return;
                    _dictionary[key] = value;
                    unchecked { Version++; }
                    _onReplace?.Invoke(key, oldValue, value);
                    return;
                }

                _dictionary.Add(key, value);
                unchecked { Version++; }
                _onAdd?.Invoke(key, value);
            }
        }

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            unchecked { Version++; }
            _onAdd?.Invoke(key, value);
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (_dictionary.ContainsKey(key)) return false;
            _dictionary.Add(key, value);
            unchecked { Version++; }
            _onAdd?.Invoke(key, value);
            return true;
        }

        public bool Remove(TKey key)
        {
            if (!_dictionary.TryGetValue(key, out var value)) return false;
            if (!_dictionary.Remove(key)) return false;
            unchecked { Version++; }
            _onRemove?.Invoke(key, value);
            return true;
        }

        public void Clear()
        {
            if (_dictionary.Count == 0) return;
            _dictionary.Clear();
            unchecked { Version++; }
            _onClear?.Invoke();
        }

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value)
            => _dictionary.TryGetValue(key, out value!);

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator()
            => _dictionary.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => _dictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _dictionary.GetEnumerator();

        public void Observe(
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared)
        {
            if (added == null) throw new ArgumentNullException(nameof(added));
            if (removed == null) throw new ArgumentNullException(nameof(removed));
            if (replaced == null) throw new ArgumentNullException(nameof(replaced));
            if (cleared == null) throw new ArgumentNullException(nameof(cleared));
            _onAdd += added;
            _onRemove += removed;
            _onReplace += replaced;
            _onClear += cleared;
        }

        public void Unobserve(
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared)
        {
            _onAdd -= added;
            _onRemove -= removed;
            _onReplace -= replaced;
            _onClear -= cleared;
        }
    }
}

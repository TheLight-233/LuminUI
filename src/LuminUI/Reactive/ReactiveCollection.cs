using System;
using System.Collections;
using System.Collections.Generic;

namespace LuminUI
{
    /// <summary>
    /// 响应式集合。增删改清空都会发出回调，配合 LuminWidgetList 做增量刷新（不整表重建）。
    /// 回调用四个独立委托而非事件参数对象，避免每次变化的 EventArgs 装箱/分配。
    /// </summary>
    public sealed class ReactiveCollection<T> : IReadOnlyReactiveCollection<T>, IReactiveCollectionObserver<T>
    {
        private readonly List<T> _list;

        private Action<int, T>?    _onAdd;     // index, item
        private Action<int, T>?    _onRemove;  // index, removedItem
        private Action<int, T, T>? _onReplace; // index, old, new
        private Action<int, int, T>? _onMove;  // oldIndex, newIndex, item
        private Action?            _onClear;

        public ReactiveCollection()            => _list = new List<T>();
        public ReactiveCollection(int capacity) => _list = new List<T>(capacity);

        public int Count => _list.Count;
        public int Version { get; private set; }

        public T this[int index]
        {
            get => _list[index];
            set
            {
                var old = _list[index];
                if (EqualityComparer<T>.Default.Equals(old, value)) return;
                _list[index] = value;
                unchecked { Version++; }
                _onReplace?.Invoke(index, old, value);
            }
        }

        public void Add(T item)
        {
            _list.Add(item);
            unchecked { Version++; }
            _onAdd?.Invoke(_list.Count - 1, item);
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            unchecked { Version++; }
            _onAdd?.Invoke(index, item);
        }

        public void RemoveAt(int index)
        {
            var old = _list[index];
            _list.RemoveAt(index);
            unchecked { Version++; }
            _onRemove?.Invoke(index, old);
        }

        public bool Remove(T item)
        {
            int i = _list.IndexOf(item);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        public void Clear()
        {
            if (_list.Count == 0) return;
            _list.Clear();
            unchecked { Version++; }
            _onClear?.Invoke();
        }

        /// <summary>移动现有元素；列表容量不变化，稳定状态下不产生托管分配。</summary>
        public void Move(int oldIndex, int newIndex)
        {
            if ((uint)oldIndex >= (uint)_list.Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if ((uint)newIndex >= (uint)_list.Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            if (oldIndex == newIndex) return;

            var item = _list[oldIndex];
            _list.RemoveAt(oldIndex);
            _list.Insert(newIndex, item);
            unchecked { Version++; }
            _onMove?.Invoke(oldIndex, newIndex, item);
        }

        public int IndexOf(T item) => _list.IndexOf(item);

        public IReadOnlyList<T> AsReadOnly() => _list;

        public List<T>.Enumerator GetEnumerator() => _list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()        => _list.GetEnumerator();

        // ── 订阅（仅供 LuminWidgetList 等内部使用）──────────────────────────
        public void Observe(Action<int, T> add, Action<int, T> remove,
                            Action<int, T, T> replace, Action<int, int, T> move,
                            Action clear)
        {
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            if (replace == null) throw new ArgumentNullException(nameof(replace));
            if (move == null) throw new ArgumentNullException(nameof(move));
            if (clear == null) throw new ArgumentNullException(nameof(clear));
            _onAdd += add;
            _onRemove += remove;
            _onReplace += replace;
            _onMove += move;
            _onClear += clear;
        }

        public void Unobserve(Action<int, T> add, Action<int, T> remove,
                              Action<int, T, T> replace, Action<int, int, T> move,
                              Action clear)
        {
            _onAdd -= add;
            _onRemove -= remove;
            _onReplace -= replace;
            _onMove -= move;
            _onClear -= clear;
        }
    }
}

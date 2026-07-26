using System;
using System.Collections.Generic;
using LuminUI.Interface;

namespace LuminUI
{
    // 非泛型基类，供 LuminView 统一登记、销毁时释放、语言切换时级联。
    public abstract class LuminWidgetListBase
    {
        internal abstract void __Suspend();
        internal abstract void __Dispose();
        internal abstract void __LanguageCascade();
    }

    // 可复用 cell 列表（虚拟滚动地基）。把数据源同步到一组组件 cell：复用空闲 cell（对象池）
    // 而非每次 Instantiate；绑定 ReactiveCollection 时做增量刷新，不整表重建。
    public sealed class LuminWidgetList<TWidget, TItem> : LuminWidgetListBase
        where TWidget : LuminView
    {
        private struct Cell { public TWidget Widget; public object Root; }

        private readonly LuminView _owner;
        private readonly IUiBridge _bridge;
        private readonly object _container;
        private readonly object _template;
        private readonly Func<TWidget> _factory;
        private readonly Action<TWidget, TItem, int> _binder;
        private readonly int _maxIdle;

        private readonly List<Cell> _active = new List<Cell>();
        private readonly Stack<Cell> _idle = new Stack<Cell>();

        // 集合回调缓存一次，避免每次 Bind 重复创建，且保证 Unobserve 传入同一实例。
        private readonly Action<int, TItem> _onAdded;
        private readonly Action<int, TItem> _onRemoved;
        private readonly Action<int, TItem, TItem> _onReplaced;
        private readonly Action<int, int, TItem> _onMoved;
        private readonly Action _onCleared;

        private IReadOnlyReactiveCollection<TItem>? _bound;

        internal LuminWidgetList(LuminView owner, IUiBridge bridge, object container, object template,
                                 Func<TWidget> factory, Action<TWidget, TItem, int> binder, int maxIdle)
        {
            _owner = owner;
            _bridge = bridge;
            _container = container;
            _template = template;
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _binder = binder ?? throw new ArgumentNullException(nameof(binder));
            _maxIdle = maxIdle < 0 ? 0 : maxIdle;

            _onAdded = OnItemAdded;
            _onRemoved = OnItemRemoved;
            _onReplaced = OnItemReplaced;
            _onMoved = OnItemMoved;
            _onCleared = OnCleared;

            _bridge.SetActive(_template, false); // 模板本身不显示
        }

        public int Count => _active.Count;
        public TWidget this[int index] => _active[index].Widget;

        // 手动设置数据源，按下标 diff：长则补、短则回收，再统一 rebind 并重排同级序。
        public void SetItems(IReadOnlyList<TItem> items)
        {
            int n = items.Count;
            while (_active.Count < n) _active.Add(RentCell());
            while (_active.Count > n)
            {
                RecycleCell(_active[_active.Count - 1]);
                _active.RemoveAt(_active.Count - 1);
            }
            for (int i = 0; i < n; i++) _binder(_active[i].Widget, items[i], i);
            ReorderSiblings();
        }

        // 绑定响应式集合：先全量填充，随后增删改清空都做增量刷新。
        public void Bind(IReadOnlyReactiveCollection<TItem> collection)
        {
            if (collection is not IReactiveCollectionObserver<TItem> observer)
                throw new ArgumentException(
                    "Reactive collection does not support LuminUI subscriptions.", nameof(collection));
            Unbind();
            _bound = collection;
            SetItems(collection);
            observer.Observe(_onAdded, _onRemoved, _onReplaced, _onMoved, _onCleared);
        }

        public void Unbind()
        {
            if (_bound == null) return;
            ((IReactiveCollectionObserver<TItem>)_bound).Unobserve(
                _onAdded, _onRemoved, _onReplaced, _onMoved, _onCleared);
            _bound = null;
        }

        private void OnItemAdded(int index, TItem item)
        {
            var cell = RentCell();
            if (index >= _active.Count) _active.Add(cell);
            else _active.Insert(index, cell);
            for (int i = index; i < _active.Count; i++) _binder(_active[i].Widget, GetItem(i), i);
            ReorderSiblings();
        }

        private void OnItemRemoved(int index, TItem item)
        {
            if (index < 0 || index >= _active.Count) return;
            RecycleCell(_active[index]);
            _active.RemoveAt(index);
            for (int i = index; i < _active.Count; i++) _binder(_active[i].Widget, GetItem(i), i);
            ReorderSiblings();
        }

        private void OnItemReplaced(int index, TItem oldItem, TItem newItem)
        {
            if (index >= 0 && index < _active.Count) _binder(_active[index].Widget, newItem, index);
        }

        private void OnItemMoved(int oldIndex, int newIndex, TItem item)
        {
            if ((uint)oldIndex >= (uint)_active.Count || (uint)newIndex >= (uint)_active.Count) return;
            var cell = _active[oldIndex];
            _active.RemoveAt(oldIndex);
            _active.Insert(newIndex, cell);
            int first = oldIndex < newIndex ? oldIndex : newIndex;
            int last = oldIndex > newIndex ? oldIndex : newIndex;
            for (int i = first; i <= last; i++) _binder(_active[i].Widget, GetItem(i), i);
            ReorderSiblings();
        }

        private void OnCleared()
        {
            for (int i = 0; i < _active.Count; i++) RecycleCell(_active[i]);
            _active.Clear();
        }

        private TItem GetItem(int i) => _bound != null ? _bound[i] : default!;

        private Cell RentCell()
        {
            if (_idle.Count > 0)
            {
                var c = _idle.Pop();
                _bridge.SetActive(c.Root, true);
                c.Widget.__Mount(_bridge, c.Root, _owner);
                return c;
            }
            var root = _bridge.Instantiate(_template, _container);
            _bridge.SetActive(root, true);
            var w = _factory();
            w.__Mount(_bridge, root, _owner);
            return new Cell { Widget = w, Root = root };
        }

        private void RecycleCell(Cell c)
        {
            c.Widget.__Unmount();
            _bridge.SetActive(c.Root, false);
            if (_idle.Count < _maxIdle) _idle.Push(c);
            else _bridge.Destroy(c.Root);
        }

        private void ReorderSiblings()
        {
            for (int i = 0; i < _active.Count; i++) _bridge.SetSiblingIndex(_active[i].Root, i);
        }

        internal override void __Dispose()
        {
            Unbind();
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].Widget.__Unmount(true);
                _bridge.Destroy(_active[i].Root);
            }
            _active.Clear();
            while (_idle.Count > 0) _bridge.Destroy(_idle.Pop().Root);
        }

        internal override void __Suspend()
        {
            Unbind();
            for (int i = 0; i < _active.Count; i++) RecycleCell(_active[i]);
            _active.Clear();
        }

        internal override void __LanguageCascade()
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Widget.__LanguageCascade();
        }
    }
}

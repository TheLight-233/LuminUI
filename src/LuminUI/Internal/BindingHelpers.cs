using System;
using LuminUI.Event;

namespace LuminUI.Internal
{
    /// <summary>EventBus 退订辅助：每个 T 一个静态委托，退订时零闭包零分配。</summary>
    internal static class UnsubHelper<T> where T : struct
    {
        internal static readonly Action<object> Do =
            h => EventBus.Unsubscribe((Action<T>)h);
    }

    /// <summary>ReactiveProperty 退订辅助：每个 T 一个静态委托，零闭包零分配。</summary>
    internal static class PropHelper<T>
    {
        internal static readonly Action<object, object> Do =
            (p, h) => ((IReactivePropertyObserver<T>)p).Unsubscribe((Action<T>)h);
    }

    internal sealed class CollectionSubscription<T>
    {
        private readonly IReactiveCollectionObserver<T> _source;
        private readonly Action<int, T> _added;
        private readonly Action<int, T> _removed;
        private readonly Action<int, T, T> _replaced;
        private readonly Action<int, int, T> _moved;
        private readonly Action _cleared;

        internal CollectionSubscription(
            IReactiveCollectionObserver<T> source,
            Action<int, T> added,
            Action<int, T> removed,
            Action<int, T, T> replaced,
            Action<int, int, T> moved,
            Action cleared)
        {
            _source = source;
            _added = added;
            _removed = removed;
            _replaced = replaced;
            _moved = moved;
            _cleared = cleared;
        }

        internal void Start() => _source.Observe(_added, _removed, _replaced, _moved, _cleared);
        internal void Stop() => _source.Unobserve(_added, _removed, _replaced, _moved, _cleared);
    }

    internal static class CollectionSubHelper<T>
    {
        internal static readonly Action<object, object> Do =
            (subscription, _) => ((CollectionSubscription<T>)subscription).Stop();
    }

    internal sealed class DictionarySubscription<TKey, TValue> where TKey : notnull
    {
        private readonly IReactiveDictionaryObserver<TKey, TValue> _source;
        private readonly Action<TKey, TValue> _added;
        private readonly Action<TKey, TValue> _removed;
        private readonly Action<TKey, TValue, TValue> _replaced;
        private readonly Action _cleared;

        internal DictionarySubscription(
            IReactiveDictionaryObserver<TKey, TValue> source,
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared)
        {
            _source = source;
            _added = added;
            _removed = removed;
            _replaced = replaced;
            _cleared = cleared;
        }

        internal void Start() => _source.Observe(_added, _removed, _replaced, _cleared);
        internal void Stop() => _source.Unobserve(_added, _removed, _replaced, _cleared);
    }

    internal static class DictionarySubHelper<TKey, TValue> where TKey : notnull
    {
        internal static readonly Action<object, object> Do =
            (subscription, _) => ((DictionarySubscription<TKey, TValue>)subscription).Stop();
    }
}

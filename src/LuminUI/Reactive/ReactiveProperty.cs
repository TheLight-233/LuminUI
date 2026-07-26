using System;
using System.Collections.Generic;

namespace LuminUI
{
    /// <summary>
    /// MVR 响应式单值。值变化时通知订阅者，相同值不触发。
    /// Publish/通知路径零分配；订阅用方法组可零闭包。
    /// </summary>
    public sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>, IReactivePropertyObserver<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;
        private Action<T>? _changed;

        public ReactiveProperty(T initial = default!)
            : this(initial, EqualityComparer<T>.Default) { }

        public ReactiveProperty(T initial, IEqualityComparer<T> comparer)
        {
            _value = initial;
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }

        /// <summary>每次真实变化或强制通知后递增，可用于低成本脏标记。</summary>
        public int Version { get; private set; }

        public T Value
        {
            get => _value;
            set
            {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                unchecked { Version++; }
                _changed?.Invoke(value);
            }
        }

        /// <summary>订阅变化，并立即推送当前值（绑定后 UI 立刻同步）。</summary>
        public void Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _changed += handler;
            handler(_value);
        }

        /// <summary>订阅变化，但不立即推送当前值。</summary>
        public void SubscribeNoPush(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _changed += handler;
        }

        /// <summary>取消订阅。</summary>
        public void Unsubscribe(Action<T> handler) => _changed -= handler;

        /// <summary>静默赋值，不触发通知。</summary>
        public void SetSilent(T value) => _value = value;

        /// <summary>无视相等性，强制以当前值通知所有订阅者（引用类型内部被修改时使用）。</summary>
        public void ForceNotify()
        {
            unchecked { Version++; }
            _changed?.Invoke(_value);
        }

        public static implicit operator T(ReactiveProperty<T> p) => p._value;
    }
}

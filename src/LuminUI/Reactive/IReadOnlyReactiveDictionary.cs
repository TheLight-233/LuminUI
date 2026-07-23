using System;
using System.Collections.Generic;

namespace LuminUI
{
    /// <summary>只读响应式字典视图。</summary>
    public interface IReadOnlyReactiveDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        int Version { get; }

        void Observe(
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared);

        void Unobserve(
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared);
    }
}

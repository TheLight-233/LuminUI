using System;
using System.Collections.Generic;

namespace LuminUI
{
    /// <summary>只读响应式列表视图。观察回调均为强类型委托，变化通知热路径零装箱。</summary>
    public interface IReadOnlyReactiveCollection<T> : IReadOnlyList<T>
    {
        int Version { get; }
    }

    internal interface IReactiveCollectionObserver<T>
    {
        void Observe(
            Action<int, T> added,
            Action<int, T> removed,
            Action<int, T, T> replaced,
            Action<int, int, T> moved,
            Action cleared);

        void Unobserve(
            Action<int, T> added,
            Action<int, T> removed,
            Action<int, T, T> replaced,
            Action<int, int, T> moved,
            Action cleared);
    }
}

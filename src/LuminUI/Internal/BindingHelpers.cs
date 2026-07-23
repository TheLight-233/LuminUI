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
            (p, h) => ((IReadOnlyReactiveProperty<T>)p).Unsubscribe((Action<T>)h);
    }
}

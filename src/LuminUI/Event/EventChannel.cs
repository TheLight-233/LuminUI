using System;

namespace LuminUI.Event
{
    /// <summary>每个事件类型 T 独享一个静态通道：零字典查找、零装箱、Raise 零 GC。</summary>
    internal static class EventChannel<T> where T : struct
    {
        private static Action<T>? _handlers;

        // 首次被引用时（静态构造）登记复位委托，供 EventBus.ResetAll() 统一清空。
        static EventChannel() => EventResetRegistry.Register(RemoveAll);

        internal static void Add(Action<T> handler)    => _handlers += handler;
        internal static void Remove(Action<T> handler) => _handlers -= handler;
        internal static void RemoveAll()               => _handlers = null;

        /// <summary>触发事件，零 GC。</summary>
        internal static void Raise(in T evt) => _handlers?.Invoke(evt);
    }
}

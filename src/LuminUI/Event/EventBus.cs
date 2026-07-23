using System;

namespace LuminUI.Event
{
    /// <summary>
    /// 可选的跨系统事件总线。局部 UI Action 优先使用生成的 Reactive 代理。
    /// Publish 路径零 GC、零装箱、零字典查找。
    /// 事件类型必须是 struct。推荐在 View 的 OnInit 中通过 Listen&lt;T&gt; 订阅，销毁时自动清理。
    /// </summary>
    public static class EventBus
    {
        /// <summary>订阅事件。推荐使用方法组避免 lambda 分配。</summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
            => EventChannel<T>.Add(handler);

        /// <summary>取消订阅。</summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
            => EventChannel<T>.Remove(handler);

        /// <summary>清除指定事件类型的所有订阅者。</summary>
        public static void UnsubscribeAll<T>() where T : struct
            => EventChannel<T>.RemoveAll();

        /// <summary>发布事件，零 GC。in 修饰对大型 struct 避免栈拷贝。</summary>
        public static void Publish<T>(in T evt) where T : struct
            => EventChannel<T>.Raise(in evt);

        /// <summary>清空所有事件通道（由桥接层在快速进入 PlayMode 时调用，防止静态残留）。</summary>
        public static void ResetAll() => EventResetRegistry.ResetAll();
    }
}

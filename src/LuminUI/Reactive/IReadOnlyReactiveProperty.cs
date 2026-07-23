using System;

namespace LuminUI
{
    /// <summary>
    /// 只读响应式值。View 只能读取和订阅，Model 保留写权限。
    /// 通知热路径不创建 EventArgs、不装箱。
    /// </summary>
    public interface IReadOnlyReactiveProperty<T>
    {
        T Value { get; }
        int Version { get; }

        void Subscribe(Action<T> handler);
        void SubscribeNoPush(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }
}

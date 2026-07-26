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
    }

    // Public read-only projections intentionally hide raw subscription methods.
    // LuminView owns subscriptions through this internal contract.
    internal interface IReactivePropertyObserver<T>
    {
        void Subscribe(System.Action<T> handler);
        void SubscribeNoPush(System.Action<T> handler);
        void Unsubscribe(System.Action<T> handler);
    }
}

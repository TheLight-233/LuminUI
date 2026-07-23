using System;
using System.Collections.Generic;

namespace LuminUI.Event
{
    /// <summary>
    /// 收集所有已使用过的 EventChannel&lt;T&gt; 的复位委托。
    /// 用于"关闭 Domain Reload 的快速进入 PlayMode"场景下，一键清空所有静态订阅，避免跨场次残留。
    /// </summary>
    internal static class EventResetRegistry
    {
        private static readonly object   _gate    = new object();
        private static readonly List<Action> _resets = new List<Action>();

        internal static void Register(Action reset)
        {
            lock (_gate) _resets.Add(reset);
        }

        internal static void ResetAll()
        {
            lock (_gate)
                for (int i = 0; i < _resets.Count; i++) _resets[i]();
        }
    }
}

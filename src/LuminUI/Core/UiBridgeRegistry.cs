using System;
using LuminUI.Interface;

namespace LuminUI
{
    // 全局桥接注册点。平台桥接层启动时设置一次。
    public static class UiBridgeRegistry
    {
        private static IUiBridge? _bridge;

        public static IUiBridge Bridge
        {
            get => _bridge ?? throw new InvalidOperationException(
                       "[LuminUI] Bridge not set. Call UiBridgeRegistry.SetBridge() from your platform bridge.");
            private set => _bridge = value;
        }

        public static void SetBridge(IUiBridge bridge)
            => Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }
}

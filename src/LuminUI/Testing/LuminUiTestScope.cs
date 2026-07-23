using System;

namespace LuminUI.Testing
{
    // 测试作用域：进入时装配 Null 桥接/加载器，离开时完整复位（清空屏、池、事件总线）。
    // using (new LuminUiTestScope()) { LuminUIRuntime.RegisterAll(); ... }
    public sealed class LuminUiTestScope : IDisposable
    {
        public LuminUiTestScope()
        {
            LuminUi.ResetForTesting();
            UiBridgeRegistry.SetBridge(new NullUiBridge());
            LuminUi.SetBridgeForTesting(new NullUiBridge());
            LuminUi.SetLoader(new NullUiLoader());
        }

        public void Dispose() => LuminUi.ResetForTesting();
    }
}

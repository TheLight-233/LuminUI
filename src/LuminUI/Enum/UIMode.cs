namespace LuminUI
{
    /// <summary>面板显示模式，决定同层之间的交互方式。</summary>
    public enum UIMode
    {
        /// <summary>普通：同层多个面板并存，互不隐藏。</summary>
        Normal,
        /// <summary>栈：同层只有栈顶可见，打开压栈隐藏下层，Back() 弹栈恢复。</summary>
        Stack,
        /// <summary>覆盖：不参与 Stack 隐藏逻辑，始终置于同层之上（典型为模态弹窗）。</summary>
        Overlay,
    }
}

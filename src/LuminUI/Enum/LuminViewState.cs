namespace LuminUI
{
    /// <summary>View（Panel/Widget）的完整状态，单一枚举消除多 bool 的非法组合。</summary>
    public enum LuminViewState
    {
        /// <summary>未初始化或已彻底销毁。</summary>
        None,
        /// <summary>资源加载中或入场动画播放中。</summary>
        Opening,
        /// <summary>完全打开，参与 Update。</summary>
        Open,
        /// <summary>打开但隐藏，已脱离 Update，不消耗每帧时间。</summary>
        Hidden,
        /// <summary>退场动画播放中，即将销毁。</summary>
        Closing,
    }
}

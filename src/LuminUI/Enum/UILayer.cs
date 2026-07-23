namespace LuminUI
{
    /// <summary>UI 层级。数值即基准渲染序，层内由 LuminUi 递增分配排序号传给 Loader。</summary>
    public enum UILayer
    {
        Background = 0,
        Scene      = 100,
        HUD        = 200,
        Popup      = 300,
        Loading    = 400,
        Toast      = 500,
    }
}

using System;
using LuminUI.Attributes;

namespace LuminUI.Samples.Inventory;

// 本 Sample 不依赖 Unity，所以这里只定义能让示例编译的最小控件契约。
// 真实项目中，IUiBridge 会从 Unity 节点取得 Button、TMP_Text 等实际控件。

// [UiClickEvent] 告诉源生成器：这个控件的点击事件名为 Clicked。
// 因此 View 上的 [OnClick(nameof(_button))] 可以生成强类型 += / -=，无需反射。
[UiClickEvent(nameof(Clicked))]
public sealed class Button
{
    public event Action? Clicked;
    public bool Enabled { get; set; } = true;

    // 测试或示例代码可调用 Click 来模拟一次用户点击。
    public void Click() => Clicked?.Invoke();
}

// Label 同样只是平台控件的占位契约，不是 LuminUI 要求项目照搬的实现。
public sealed class Label
{
    public string Text { get; set; } = "";
    public int Number { get; private set; }
    public int SecondaryNumber { get; private set; }

    // Sample 的数值刷新不格式化字符串，用来模拟平台层提供的无分配数字文本接口。
    // Unity 适配层可以在这里接入自己的整数格式化缓存或 TMP API。
    public void SetInt(int value) => Number = value;

    // 一次保存“已用格数 / 总容量”两个值，供 InventorySummary.ShowSlots 调用。
    public void SetPair(int value, int secondary)
    {
        Number = value;
        SecondaryNumber = secondary;
    }
}

# LuminUI

**LuminUI 是一个面向游戏与保留式 UI 的高性能 C# MVR（Model · View · Reactive）框架。**

它把 UI 业务状态、控件显示和响应式更新清晰地分开，并通过源生成器在编译期生成绑定、事件与生命周期代码。你可以专注于编写状态和界面逻辑，不必维护字符串路径、手动订阅或手动解绑。

LuminUI 不使用反射，也不构建虚拟 DOM；在稳定运行后的属性、集合和字典通知热路径中，以 **0 B 托管分配** 为目标。

## 它解决什么问题？

传统游戏 UI 往往把状态、控件引用、事件注册和生命周期管理混在一起。界面变复杂后，更新遗漏、订阅泄漏和难以追踪的耦合会迅速累积。

LuminUI 为这三个职责建立明确边界：

- **Model**：只保存业务状态，并通过显式 Action 改变状态。
- **View**：只描述控件如何显示，以及用户交互如何触发 Action。
- **Reactive**：由生成器提供类型安全的状态访问和通知调度，并自动处理订阅与释放。

每个 Screen 或 Widget 都拥有隔离的 Model；创建、初次刷新、订阅与销毁时的退订由框架生成的代码统一管理。

## 快速感受

先定义状态和操作：

```csharp
[LuminModel]
public sealed class CounterModel
{
    public ReactiveProperty<int> Count { get; } = new(0);

    [LuminAction]
    public void Add() => Count.Value++;
}
```

然后让 View 关心显示与交互：

```csharp
[Screen(typeof(CounterModel))]
public partial class CounterView : LuminView
{
    [UiElement("Count")]
    private Label _count = null!;

    [UiElement("Add")]
    private Button _add = null!;

    [Observe(nameof(CounterModel.Count))]
    private void ShowCount(int value) => _count.SetInt(value);

    [OnClick(nameof(_add))]
    private void Add() => Reactive.Add();
}
```

注册一次生成的内容后，即可用强类型 API 打开界面：

```csharp
LuminUIRuntime.RegisterAll();
var handle = await CounterView.OpenAsync(new CounterModel());
```

`Count` 改变时，`ShowCount` 会自动收到更新；界面关闭时，相关订阅会自动解除。

## 为性能敏感的 UI 而设计

LuminUI 将高频更新视为一等场景：

- `ReactiveProperty<T>` 用于单值状态；
- `ReactiveCollection<T>` 用于列表的替换、移动和变化通知；
- `ReactiveDictionary<TKey, TValue>` 用于键值状态；
- `[Observe]` 的调度代码在编译期生成。

初始化、首次订阅、委托创建、容量扩张与资源加载属于冷路径；稳定热路径则避免额外托管分配。

## 开始使用

```bash
dotnet add package LuminUI
```

该包同时包含运行时和源生成器。你的平台层只需实现并注册 `IUiBridge` 与 `IUiLoader`，用来连接实际 UI 框架的控件与资源加载能力。

完整的背包 UI 示例位于 [LuminUI.Samples.Inventory](samples/LuminUI.Samples.Inventory)。

## License

[MIT](LICENSE)

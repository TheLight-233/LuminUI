# 背包 Sample：从 Model 到界面刷新

这个示例用于说明 LuminUI 的 MVR 写法，不包含真实平台的 `Loader` 和 `Bridge`。`Controls.cs` 中的 `Button`、`Label` 只是最小占位控件；在 Unity 项目里应由适配层取得真实的 UGUI、UIToolkit 或其他控件。

## 建议阅读顺序

1. `InventoryModel.cs`：先看状态放在哪里，以及三个 `[LuminAction]` 如何修改状态。
2. `InventoryViews.cs` 的 `InventoryView`：看根 Screen 如何组合 Widget、绑定列表。
3. 继续看 `InventorySummary`、`InventoryActions`、`InventoryDetails`：分别对应读取状态、发起 Action、显示选中项。
4. 最后看 `InventoryItemCell` 和 `Controls.cs`：理解 Cell 复用，以及平台控件如何接入生成器。

第一次阅读时可以先忽略排序算法和 `IndexById` 的维护，它们只是背包业务细节。

## 一条完整的数据流

```text
用户点击 Button
    -> [OnClick] 生成的事件接线
    -> View 调用 Reactive.UseSelected()
    -> [LuminAction] 生成的转发调用 InventoryModel.UseSelected()
    -> Model 修改 ReactiveProperty / ReactiveCollection
    -> [Observe] 或 [BindList] 收到通知
    -> 只刷新相关 View / Cell
```

这里没有手写 ViewModel。View 也不直接持有 `InventoryModel`：源生成器会创建一个较窄的 `InventoryReactive` 门面，View 从它读取响应式状态，并通过 Action 请求 Model 改变状态。

## 三类手写代码

### 1. Model：状态与业务规则

`[LuminModel]` 会扫描 `InventoryModel` 的公开响应式成员：

- `ReactiveProperty<T>` 表示一个值，例如金币、容量、当前选中物品；
- `ReactiveCollection<T>` 表示有顺序、会增删改和移动的物品列表；
- `ReactiveDictionary<TKey, TValue>` 表示可响应变化的键值映射；本例用它快速查找物品下标；
- `[LuminAction]` 表示允许 View 调用的业务操作，例如选择、使用和排序。

真正的写权限保留在 Model。生成的 `InventoryReactive` 只向 View 暴露只读响应式接口，并把 Action 强类型转发回 Model。

### 2. Screen 与 Widget：界面结构

`InventoryView` 使用 `[Screen(typeof(InventoryModel))]`，所以它是可打开的根界面，并要求一个 `InventoryModel`。它通过三个 `[UiWidget]` 组成固定子组件，并通过 `[BindList]` 管理动态 Cell：

- `InventorySummary`：观察金币和格数；
- `InventoryActions`：把按钮点击转换为 Action；
- `InventoryDetails`：观察当前选中物品；
- `InventoryItemCell`：由列表创建和复用，用于显示一件物品。

`[View(typeof(InventoryModel))]` 表示响应式子组件。打开一个 `InventoryView` 时，框架为这棵界面树建立一个 `InventoryReactive` 上下文；根 Screen、三个 Widget 和所有活跃 Cell 都共享它。这个共享范围是“一次打开的根 Screen 及其组件树”，不是每个 Widget 各创建一份状态。

`InventoryHelp` 展示了另一种情况：`[Screen]` 没有指定 Model，因此它是纯展示界面，不生成 `Reactive`，打开时也不需要传 Model。

### 3. 渲染方法：只描述“变化后怎么显示”

`[Observe(nameof(InventoryModel.Gold))]` 会生成以下行为：

1. Widget 挂载时调用一次 `ShowGold`，立即显示当前值；
2. `Gold.Value` 变化后再次调用 `ShowGold`；
3. Widget 卸载或 Screen 关闭时自动退订。

一个方法也可以同时观察多个值。`ShowSlots` 观察 `UsedSlots` 和 `Capacity`，任意一个变化时都会用两者的最新值刷新。

## 列表为什么不会整表重建

`[BindList(nameof(InventoryModel.Items), ...)]` 生成 `LuminWidgetList<InventoryItemCell, InventoryItem>`，首次绑定时创建当前所需的 Cell，之后按集合通知增量更新：

| Model 中的操作 | 列表行为 | 本例位置 |
| --- | --- | --- |
| `Items.Add/Insert` | 从空闲池取一个 Cell，没有才创建 | 构造函数的 `AddInitial` 发生在打开前，因此首次绑定直接显示最终快照 |
| `Items[index] = value` | 只重新绑定该 Cell | 使用数量大于 1 的物品 |
| `Items.RemoveAt(index)` | 回收对应 Cell，并更新后续下标 | 用完最后一个物品 |
| `Items.Move(old, new)` | 移动已有 Cell，并重绑受影响区间 | 按名称排序 |
| `Items.Clear()` | 回收全部活跃 Cell | 本例未触发，但生成列表已支持 |

`MaxIdle = 12` 表示最多缓存 12 个暂时不用的 Cell。Cell 被复用时，`InventoryItemCell.Show` 会收到新物品，所以它必须重新写入 `_itemId`、名称和数量。

## 哪些代码由源生成器完成

下列内容都不需要手写，也不依赖运行时反射：

- `InventoryReactive`，包括只读状态入口和 `Select`、`UseSelected`、`SortByName` 转发方法；
- 每个响应式 View 的 `Reactive` 属性及上下文接入；
- `[UiElement]` 的控件查找与字段赋值；
- `[OnClick]` 的事件订阅和退订；
- `[Observe]` 的首次渲染、变化订阅和关闭退订；
- `[UiWidget]` 的创建、挂载、卸载与复用；
- `[BindList]` 的集合订阅、增量刷新和 Cell 池；
- `InventoryView.OpenAsync(InventoryModel)` 与 `InventoryHelp.OpenAsync()`；
- `LuminUIRuntime.RegisterAll()` 中的 Screen、Bridge、Loader 注册代码。

可以把生成结果大致想成下面这样，但不要在项目中重复手写：

```csharp
// 仅用于理解，真实代码由生成器输出。
sealed class InventoryReactive : LuminReactive
{
    private InventoryModel _model = null!;

    public IReadOnlyReactiveCollection<InventoryItem> Items => _model.Items;
    public IReadOnlyReactiveProperty<int> Gold => _model.Gold;
    public void UseSelected() => _model.UseSelected();

    protected override void OnAttach(object model) => _model = (InventoryModel)model;
    protected override void OnDetach() => _model = null!;
}

partial class InventoryDetails
{
    protected InventoryReactive Reactive { get; private set; }

    // 生成器还会在正确生命周期中订阅、首次调用并退订 ShowSelected。
}
```

## 打开界面

平台项目提供并注册好 `Bridge`、`Loader` 后，在启动阶段调用一次：

```csharp
LuminUIRuntime.RegisterAll();
```

随后用 Model 打开响应式 Screen：

```csharp
var model = new InventoryModel();
var handle = await InventoryView.OpenAsync(model);

// handle.View 是当前 InventoryView；关闭可使用：
await handle.CloseAsync();
```

纯展示 Screen 不需要 Model：

```csharp
var help = await InventoryHelp.OpenAsync();
```

打开时，`[Observe]` 会先完成首帧数据填充，`[BindList]` 会按 `Items` 当前内容准备 Cell。之后所有刷新都由 Model 的响应式变化驱动。

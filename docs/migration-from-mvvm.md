# 从 MVVM 或旧版 LuminUI 迁移

## Screen 不再接收 Model

旧版：

```csharp
[Screen(typeof(InventoryModel))]
public partial class InventoryView : LuminView { }

var screen = await InventoryView.OpenAsync(model);
```

新版：

```csharp
[Screen]
public partial class InventoryView : LuminView { }

var screen = await InventoryView.OpenAsync();
```

数据源由独立 Reaction 选择，不构建 Screen Model Scope 或 Model 图。

## ViewModel / Reactive Context 改为 Model 只读投影

```csharp
[LuminModel]
public sealed partial class InventoryModel
{
    private readonly ReactiveProperty<int> _gold = new();

    public void AddGold(int value) => _gold.Value += value;
}
```

生成器产生：

```csharp
public IReadOnlyReactiveProperty<int> Gold => _gold;
```

不再生成 `InventoryReactive`，也不复制状态。

## View 订阅迁移到 Reaction

旧版或中间版本：

```csharp
protected override void OnInit()
    => Subscribe(BagManager.Instance.Model.Gold, RenderGold);
```

新版 View：

```csharp
[View]
public partial class InventoryHeaderView : LuminView
{
    [Element("Gold")]
    private Label _gold = null!;

    internal void RenderGold(int value) => _gold.SetInt(value);
}
```

新版 Reaction：

```csharp
[ReactionFor(typeof(InventoryHeaderView))]
public sealed partial class InventoryHeaderReaction
{
    protected override void OnBind()
        => Subscribe(BagManager.Instance.Model.Gold, View.RenderGold);
}
```

View 已不提供 Subscribe；Reaction 会由生成器自动创建和托管。

## View 事件迁移到 Reaction

旧版 View 方法：

```csharp
[OnClick(nameof(_useButton))]
private void Use() => BagManager.Instance.UseSelected();
```

新版把事件目标字段设为 internal，并把方法移动到 Reaction：

```csharp
[OnClick(nameof(InventoryDetailView.UseButton))]
private void Use() => BagManager.Instance.UseSelected();
```

生成器会验证目标 Element 并生成跨类型事件连接。

## `[BindList]` 改为显式列表

View 在 `OnInit` 创建列表，Reaction 在 `OnBind` 选择集合。这样列表结构属于 View，数据来源属于 Reaction，同时保留增量更新和 Cell 池。

## 属性重命名

```text
[UiElement] -> [Element]
[UiWidget]  -> [Widget]
```

已移除的概念包括 `[Observe]`、`[BindList]`、`LuminReactive`、Screen Model 参数、手动 Reactor 和 `IUiSubscriptionScope`。

## EventBus

局部 UI 状态优先使用响应式 Model。EventBus 只用于没有直接状态所有者的跨模块广播，并通过 Reaction 的 `Listen` 托管生命周期。

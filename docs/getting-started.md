# Getting started

## 1. 定义 Model

```csharp
[LuminModel]
public sealed partial class PlayerModel
{
    private readonly ReactiveProperty<int> _hp = new(100);
    private readonly ReactiveCollection<ItemData> _items = new(32);

    public void TakeDamage(int amount)
        => _hp.Value = Math.Max(0, _hp.Value - amount);
}
```

生成器创建只读的 `Hp` 和 `Items`。Model 内部写私有字段，外部只能读取或调用业务方法。

## 2. 定义 View

```csharp
[Screen(Layer = UILayer.HUD, PoolSize = 1)]
public partial class PlayerHudView : LuminView
{
    [Element("Hp")]
    private Label _hp = null!;

    [Element("Damage")]
    internal Button DamageButton = null!;

    internal void RenderHp(int hp) => _hp.SetInt(hp);
}
```

View 不读取 PlayerManager，也不调用 Subscribe。事件目标字段使用 internal，供独立 Reaction 通过 `nameof` 强类型引用；其他 Element 可以保持 private。

## 3. 定义 Reaction

```csharp
[ReactionFor(typeof(PlayerHudView))]
public sealed partial class PlayerHudReaction
{
    private static PlayerModel Model => PlayerManager.Instance.Model;

    protected override void OnBind()
        => Subscribe(Model.Hp, View.RenderHp);

    [OnClick(nameof(PlayerHudView.DamageButton))]
    private void Damage() => Model.TakeDamage(10);
}
```

不要写 `LuminReaction<PlayerHudView>` 基类，不要在 View 中 new，也不要手动释放。生成器完成这些部分。

## 4. 注册并打开

```csharp
LuminUIRuntime.RegisterAll();
var handle = await PlayerHudView.OpenAsync();
```

`OpenAsync` 不接收 Model。

## 5. 动态订阅

需要临时停止某项更新时，在 Reaction 中保存句柄：

```csharp
private SubscriptionHandle _hp;

protected override void OnBind()
    => _hp = Subscribe(Model.Hp, View.RenderHp);

private void Pause() => Unsubscribe(ref _hp);

private void Resume()
{
    if (!_hp.IsActive)
        _hp = Subscribe(Model.Hp, View.RenderHp);
}
```

恢复订阅会立即推送当前值。没有手动取消的句柄会在 Reaction Detach 时统一清理。

## 6. 组合 Widget

```csharp
[Widget("Details")]
private ItemDetailsView _details = null!;
```

Widget 可以继续声明 Widget。每个需要响应逻辑的 Widget 单独关联 Reaction；纯结构或纯展示 Widget 不需要。

## 7. 列表

View 在 `OnInit` 创建列表：

```csharp
protected override void OnInit()
{
    if (_list == null)
        _list = CreateWidgetList("Items", "Items/Template", CreateCell, RenderCell);
    else
        RegisterList(_list);
}

internal void BindItems(IReadOnlyReactiveCollection<ItemData> items)
    => _list!.Bind(items);
```

Reaction 选择数据源：

```csharp
protected override void OnBind()
    => View.BindItems(BagManager.Instance.Model.Items);
```

View 关闭时列表自动 Unbind，Cell 自动回收；池化 View 重开后重新注册并绑定。

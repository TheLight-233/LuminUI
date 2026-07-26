# Architecture

## 单向数据流

```text
UI event -> generated bridge -> Reaction -> Model / domain service
                                           |
                                           v
View.Render <- Reaction.Subscribe <- generated read-only Model projection
```

MVR 中的 R 是独立的 Reaction 组件，不是 ViewModel，也不是 View 手动创建的中转对象。

## Model

Model 拥有业务状态和修改权限：

```csharp
[LuminModel]
public sealed partial class PlayerModel
{
    private readonly ReactiveProperty<int> _hp = new(100);
    private readonly ReactiveCollection<BuffData> _buffs = new(16);

    public void TakeDamage(int value)
        => _hp.Value = Math.Max(0, _hp.Value - value);
}
```

生成器规则：

- 类型必须是非泛型顶层 `partial class`。
- 所有显式字段必须是 `private`。
- `ReactiveProperty<T>`、`ReactiveCollection<T>`、`ReactiveDictionary<TKey,TValue>` 分别生成 public 只读投影。
- 普通实现字段保持私有。
- 可空引用类型会完整保留，例如 `ReactiveProperty<Item?>` 生成 `IReadOnlyReactiveProperty<Item?>`。

外部代码只能读取响应状态并调用 Model 或领域 Service 的业务方法。

## View

View 是被动 UI：

- `[Screen]` 可以通过生成的零参数 `OpenAsync()` 打开。
- `[View]` 只能作为 Widget 或列表 Cell 挂载。
- View 声明 Element、Widget、列表结构、运行时 Widget 和 Render 方法。
- View 不提供 Subscribe API，不选择 Model，不实现跨系统业务规则。
- 纯展示 View 可以没有 Reaction。

`OnInit` 仍然保留，但只用于创建列表控制器、运行时 Widget 等 UI 结构。

## Reaction

Reaction 是单独文件中的顶层 partial 类型：

```csharp
[ReactionFor(typeof(PlayerHudView))]
public sealed partial class PlayerHudReaction
{
    protected override void OnBind()
        => Subscribe(PlayerManager.Instance.Model.Hp, View.RenderHp);
}
```

用户不声明基类。生成器产生：

```csharp
partial class PlayerHudReaction : LuminReaction<PlayerHudView>
{
}
```

Reaction 可以：

- 选择一个或多个 Model 实例；
- 订阅只读属性、集合、字典和 EventBus；
- 保存非权威的展示组合缓存；
- 调用 View 的 internal Render 方法；
- 处理 `[OnClick]` 等 UI 事件并调用 Model 或领域 Service。

Reaction 不能声明 Reactive 容器，否则会产生 `LUIN204`。业务状态必须回到 Model。

## 生成事件

Reaction 方法可以直接引用目标 View 的 internal Element 字段：

```csharp
[OnClick(nameof(InventoryView.EquipButton))]
private void Equip() => InventoryService.EquipSelected();
```

生成器验证字段存在并带有 `[Element]`，然后在 View 的 Reaction Attach/Detach 中生成强类型事件连接。View 不需要一行转发方法。

## 生命周期

打开 Screen 或挂载 Widget 时：

1. 绑定 Element；
2. 连接 View 自身事件；
3. 创建并挂载子 Widget；
4. 执行 View `OnInit`，完成列表和运行时结构；
5. Attach 缓存的 Reaction 并执行 `OnBind`。

关闭、回池或卸载时顺序相反：先 Detach Reaction 和事件，再释放列表与子 Widget。

订阅规则：

- `Subscribe` 默认立即推送当前值；
- `SubscriptionHandle` 可以提前取消；
- Hide/Cover 保持 Reaction；
- Close、回池、销毁和 Widget/Cell 卸载自动取消全部订阅；
- 池化 View 复用同一个 Reaction 对象，再次执行 `OnBind`。

## Widget Tree

`[Widget]` 生成创建和挂载代码，并维护 `Parent` / `Children`。Widget 可以继续声明 Widget，形成嵌套 UI Tree。

运行时组件通过 `AddWidget` 挂载。池化 View 重开时必须再次调用 `AddWidget`，这样旧实例会重新进入 Tree。

`ShowWidget` / `HideWidget` 只改变可见性，不 Detach Reaction。

## 列表

View 在 `OnInit` 创建并注册 `LuminWidgetList`，Reaction 在 `OnBind` 选择集合：

```csharp
protected override void OnBind()
    => View.BindItems(BagManager.Instance.Model.Items);
```

列表负责首次同步、增量更新、Cell 池化和 View 关闭时自动 Unbind。Cell 可以拥有自己的 Reaction。

## 诊断

Reaction 相关编译错误：

- `LUIN200`：Reaction 不是 partial。
- `LUIN201`：Reaction 不是受支持的顶层具体类型。
- `LUIN202`：目标不是有效 View。
- `LUIN203`：一个 View 关联多个 Reaction。
- `LUIN204`：Reaction 声明 Reactive 状态。
- `LUIN205`：Reaction 没有可用的无参构造。
- `LUIN206`：Reaction 事件目标 Element 不存在。
- `LUIN207`：已有 Reaction 的 View 仍声明事件逻辑。
- `LUIN208`：Reaction 事件方法签名不受支持。

## 性能边界

生成路径不使用运行时反射。Reaction 每个 View 实例只创建一次；方法组、订阅存储和 UI 事件连接在初始化冷路径建立。已经建立的响应通知链路不创建 EventArgs、不装箱，并由 0 B 分配测试锁定。

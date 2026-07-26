# Inventory Sample

这个示例不是单个计数器，而是一套完整的游戏背包结构。所有可见 UI 文本均为英文，便于直接放入未导入中文字体的 Unity 项目。

## 三个职责目录

```text
Model/
  InventoryTypes.cs       数据类型
  InventoryModels.cs      三个响应式 Model
  InventoryService.cs     跨 Model 业务命令和数据入口

View/
  Controls.cs              非 Unity 编译用控件
  InventoryScreenView.cs   主 Screen、Header、分类页签
  WeaponViews.cs           武器列表、筛选 Widget、武器 Cell
  EquipmentViews.cs        装备列表、Loadout 和嵌套装备槽
  ItemViews.cs             消耗品列表和 Cell
  DetailViews.cs           详情、属性、命令栏、运行时比较 Widget

Reaction/
  InventoryScreenReactions.cs
  WeaponReactions.cs
  EquipmentReactions.cs
  ItemReactions.cs
  DetailReactions.cs
```

职责固定如下：

- Model 保存业务状态并提供修改方法，源生成器只向外暴露只读响应属性。
- View 只声明 Element、Widget、列表结构和 Render 方法，不访问 Model，也不能调用 Subscribe。
- Reaction 选择 Model、建立订阅、组合展示状态并处理 UI 事件，不需要手写基类、构造、实例化或释放。

## 打开 Screen

```csharp
LuminUIRuntime.RegisterAll();
var handle = await InventoryScreenView.OpenAsync();
```

`OpenAsync` 不接收任何 Model。示例中的 `InventoryContext` 模拟游戏里的背包模块入口。

## 多 Model

示例使用三个独立 Model：

- `InventoryModel`：武器、装备、消耗品、分类、筛选、选中项和容量。
- `PlayerLoadoutModel`：当前装备的武器、头部、身体、饰品以及总战力。
- `WalletModel`：金币。

`InventoryHeaderReaction` 同时订阅三个 Model，但 View 不知道这些数据来自哪里：

```csharp
protected override void OnBind()
{
    Subscribe(InventoryContext.Wallet.Gold, View.RenderGold);
    Subscribe(InventoryContext.Inventory.UsedSlots, OnUsedSlots);
    Subscribe(InventoryContext.Inventory.Capacity, OnCapacity);
    Subscribe(InventoryContext.Loadout.Power, View.RenderPower);
}
```

## Reaction 自动生成

用户只写独立的逻辑类：

```csharp
[ReactionFor(typeof(WeaponFilterView))]
public sealed partial class WeaponFilterReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Inventory.ActiveWeaponFilter, View.RenderFilter);

    [OnClick(nameof(WeaponFilterView.NextFilterButton))]
    private void NextFilter()
        => InventoryContext.Inventory.CycleWeaponFilter();
}
```

生成器负责补全：

- `LuminReaction<WeaponFilterView>` 基类；
- 缓存的 Reaction 实例；
- View 打开或 Widget 挂载时 Attach；
- Close、回池或卸载时 Detach 和统一退订；
- Reaction 方法到 View Element 的强类型事件连接；
- Hide 和 Stack Cover 时保持订阅。

## Widget Tree

主 Screen 组合 Header、分类页签、武器、装备、物品和详情 Widget。装备面板继续嵌套：

```text
InventoryScreenView
  EquipmentInventoryView
    EquipmentLoadoutView
      EquippedWeaponSlotView
      EquippedHeadSlotView
      EquippedBodySlotView
      EquippedAccessorySlotView
  InventoryDetailView
    InventoryStatsView
    InventoryCommandBarView
```

所有 `[Widget]` 都由生成器创建和挂载，并自动维护 `Parent` / `Children`。

## 列表与 Cell

武器、装备、物品分别使用独立的 `LuminWidgetList`。View 在 `OnInit` 中创建列表结构，Reaction 选择并绑定只读集合：

```csharp
protected override void OnBind()
    => View.BindWeapons(InventoryContext.Inventory.VisibleWeapons);
```

Cell 本身也是 View，并拥有自己的 Reaction。点击 Cell 后，Reaction 读取 Cell 当前渲染的 `ItemId`，再通知 Model 选择该条目。列表增删、替换、移动和清空都会增量更新，Cell 会随列表池复用。

## 详情与业务命令

`InventoryDetailView` 只渲染名称、类型、描述和稀有度。它下面的 `InventoryStatsView` 单独渲染攻击、防御、数量和效果。

`InventoryCommandBarReaction` 同时读取选中项、金币和容量，用于决定 Equip、Use、Upgrade 和 Expand Capacity 是否可用。真正的跨 Model 业务由 `InventoryService` 完成，Reaction 不直接实现扣金币、升级和换装规则。

## 运行时 Widget 和动态订阅

`InventoryComparisonView` 不是 `[Widget]` 字段，而是在主 Screen 的 `OnInit` 中运行时挂载。Screen 从池中重新打开时会复用同一个实例并重新加入 UI Tree。

它的 Reaction 同时观察当前选中项和四个装备槽，并根据条目类型选择正确的比较目标。Toggle Live 按钮演示运行时取消和恢复订阅：恢复时会立即推送最新值。

分类切换和比较面板显示隐藏只改变 Widget 可见性，不会取消 Reaction；只有 Close、回池和真正卸载才会 Detach。

## 编译验证

```bash
dotnet build samples/LuminUI.Samples.Inventory/LuminUI.Samples.Inventory.csproj -c Release
```

该项目不依赖 Unity。`View/Controls.cs` 只是最小控件契约，Unity 项目由 `IUiBridge` 返回真实的 UGUI 或 TMP 组件。

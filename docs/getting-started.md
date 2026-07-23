# Getting started

## 1. 写 Model

只选需要的响应容器：单值用 `ReactiveProperty<T>`，有序数据用 `ReactiveCollection<T>`，按键查询用 `ReactiveDictionary<TKey,TValue>`。允许 UI 触发的方法加 `[LuminAction]`。

## 2. 写 View

给 Screen 或 Widget 标记 Model 类型，然后用：

- `[Observe]` 刷新状态；
- `[OnClick]` 调用 `Reactive.Action()`；
- `[UiWidget]` 组合组件；
- `[BindList]` 渲染增量列表。

不需要 Model 的页面继续写 `[Screen]` 或 `[View]`，没有额外成本。

## 3. 注册和打开

启动时调用一次 `LuminUIRuntime.RegisterAll()`。生成器会为每个 Screen 产生匹配 Model 类型的静态 `OpenAsync`。

完整例子请从背包 Sample 的 `InventoryModel.cs` 和 `InventoryViews.cs` 开始阅读。

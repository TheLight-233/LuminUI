# 从旧版单向 MVVM 迁移到 MVR

## ViewModel → Reactive

旧版：

```csharp
[Screen]
partial class InventoryView : LuminView<InventoryViewModel> { }
```

新版：

```csharp
[Screen(typeof(InventoryModel))]
partial class InventoryView : LuminView { }
```

`InventoryReactive` 由生成器创建，一个实例由根 Screen 和整个 Widget/Cell 树共享。View 不持有 Model。

## 手写 Bind → Observe

```csharp
// 旧：OnInit() => Bind(ViewModel.Gold, ShowGold);

[Observe(nameof(InventoryModel.Gold))]
private void ShowGold(int gold) { }
```

生成器缓存委托，负责初始调用与退订。

## 局部 EventBus → LuminAction

```csharp
[LuminAction]
public void Sort() { /* 修改 Model */ }

[OnClick(nameof(_sortButton))]
private void OnSort() => Reactive.Sort();
```

`EventBus` 仍可用于真正的跨系统广播。

## 手写 AddWidget/BindList → 特性

```csharp
[UiWidget("Header")]
private HeaderWidget _header = null!;

[BindList(nameof(InventoryModel.Items), "Items", "Items/Template")]
private void ShowItem(ItemCell cell, Item item, int index) => cell.Show(item);
```

Widget 与 Cell 在 Screen 池化复用时保留实例；不再为每次打开重新创建。

using LuminUI.Attributes;

namespace LuminUI.Samples.Inventory;

// 背包中的一条数据使用不可变值类型。
// 数量变化时创建一个新值并写回 ReactiveCollection，集合就能发出“替换”通知。
public readonly struct InventoryItem
{
    public InventoryItem(int id, string name, int count)
    {
        Id = id;
        Name = name;
        Count = count;
    }

    public int Id { get; }
    public string Name { get; }
    public int Count { get; }

    // 不直接修改旧物品，而是返回只改变 Count 的副本。
    public InventoryItem WithCount(int count) => new(Id, Name, count);
}

// [LuminModel] 告诉源生成器：
// 1. 为公开的 Reactive* 成员生成只读访问入口；
// 2. 为 [LuminAction] 方法生成转发入口；
// 3. 最终得到 InventoryReactive。
// View 只拿到 InventoryReactive，不直接持有 InventoryModel；可变状态仍由 Model 管理。
[LuminModel]
public sealed class InventoryModel
{
    // 集合负责背包顺序。Add/RemoveAt/下标赋值/Move 会分别发出增、删、改、移动通知。
    public ReactiveCollection<InventoryItem> Items { get; } = new(32);

    // 用物品 id 快速定位 Items 下标，避免每次点击都线性扫描列表。
    public ReactiveDictionary<int, int> IndexById { get; } = new(32);

    // ReactiveProperty<T> 在 Value 真正变化时通知观察它的 View。
    public ReactiveProperty<InventoryItem?> Selected { get; } = new(null);
    public ReactiveProperty<int> Gold { get; } = new(1250);
    public ReactiveProperty<int> Capacity { get; } = new(30);
    public ReactiveProperty<int> UsedSlots { get; } = new(0);

    public InventoryModel()
    {
        // 这些数据在 Screen 打开前加入。列表绑定时会先读取当前快照并创建首批 Cell；
        // 如果在 Screen 打开后调用 Items.Add，则只会增量增加一个 Cell。
        AddInitial(new InventoryItem(1, "生命药水", 5));
        AddInitial(new InventoryItem(2, "铁剑", 1));
        AddInitial(new InventoryItem(3, "回城卷轴", 3));
    }

    // [LuminAction] 把方法暴露到生成的 InventoryReactive。
    // View 可以调用 Reactive.Select(id)，但不能越过 Action 随意修改 Model。
    [LuminAction]
    public void Select(int itemId)
    {
        if (IndexById.TryGetValue(itemId, out var index))
            // 赋值后，观察 Selected 的 InventoryDetails 会自动刷新。
            Selected.Value = Items[index];
    }

    [LuminAction]
    public void UseSelected()
    {
        var selected = Selected.Value;
        if (!selected.HasValue || !IndexById.TryGetValue(selected.Value.Id, out var index)) return;

        var item = Items[index];
        if (item.Count > 1)
        {
            var changed = item.WithCount(item.Count - 1);

            // 下标赋值发出“替换”通知：[BindList] 只重新绑定这一格，而不是重建整张列表。
            Items[index] = changed;

            // 同步选中值，使右侧详情也显示新数量。
            Selected.Value = changed;
            return;
        }

        // 最后一个物品被用掉时发出“删除”通知；对应 Cell 会被回收到列表池。
        Items.RemoveAt(index);
        IndexById.Remove(item.Id);
        ReindexFrom(index);
        UsedSlots.Value = Items.Count;
        Selected.Value = null;
    }

    [LuminAction]
    public void SortByName()
    {
        // 原地选择排序：预热后不创建数组、List 或闭包。
        // Items.Move 发出“移动”通知，列表复用原 Cell，只调整顺序和受影响区间的数据。
        for (int i = 0; i < Items.Count - 1; i++)
        {
            int best = i;
            for (int j = i + 1; j < Items.Count; j++)
                if (string.CompareOrdinal(Items[j].Name, Items[best].Name) < 0) best = j;
            if (best != i) Items.Move(best, i);
        }
        ReindexFrom(0);
    }

    private void AddInitial(InventoryItem item)
    {
        // 先记录即将插入的下标，再把数据加入集合。
        IndexById.Add(item.Id, Items.Count);
        Items.Add(item);
        UsedSlots.Value = Items.Count;
    }

    private void ReindexFrom(int start)
    {
        // 删除或排序后，只修正受影响位置开始的 id -> index 映射。
        for (int i = start; i < Items.Count; i++)
            IndexById[Items[i].Id] = i;
    }
}

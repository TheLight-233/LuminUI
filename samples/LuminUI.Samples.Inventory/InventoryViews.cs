using LuminUI.Attributes;

namespace LuminUI.Samples.Inventory;

// 阅读入口：InventoryView 是可由 LuminUi 打开的根 Screen。
// typeof(InventoryModel) 声明这棵界面使用哪个 Model；生成器因此会：
// - 生成 InventoryView.OpenAsync(InventoryModel model)；
// - 打开时创建一个 InventoryReactive；
// - 把同一个 Reactive 上下文交给下面的 Widget 和列表 Cell。
[Screen(typeof(InventoryModel), Layer = UILayer.Popup, Mode = UIMode.Stack, PoolSize = 1)]
public partial class InventoryView : LuminView
{
    // [UiWidget] 会 new InventorySummary()，在指定子节点挂载它，并随父 Screen 一起卸载。
    // 字段不需要用户赋值；null! 只是告诉可空检查器“生成代码会负责初始化”。
    [UiWidget("Header/Summary")]
    private InventorySummary _summary = null!;

    [UiWidget("Header/Actions")]
    private InventoryActions _actions = null!;

    [UiWidget("Details")]
    private InventoryDetails _details = null!;

    // [BindList] 把 Model.Items 连接到可复用的 InventoryItemCell 列表：
    // 第 2 个参数是 Cell 父节点，第 3 个参数是供克隆的模板节点。
    // Items 增删改、移动时只更新受影响的 Cell；MaxIdle 控制最多保留多少个空闲 Cell。
    [BindList(nameof(InventoryModel.Items), "Items/Content", "Items/Content/Template", MaxIdle = 12)]
    private void ShowItem(InventoryItemCell cell, InventoryItem item, int index)
        => cell.Show(item);
}

// [View] 表示它是由父 View 挂载的组件，不能单独 OpenAsync。
// 指定同一个 InventoryModel 后，它会复用根 Screen 已创建的 InventoryReactive，
// 而不是持有 Model，也不会为自己复制一份状态。
[View(typeof(InventoryModel))]
public partial class InventorySummary : LuminView
{
    // [UiElement] 让生成器通过 IUiBridge.Find<Label>(root, "Gold") 给字段赋值。
    [UiElement("Gold")]
    private Label _gold = null!;

    [UiElement("Slots")]
    private Label _slots = null!;

    // [Observe] 自动生成首次刷新、订阅和退订。
    // 所以打开界面时即使 Gold 从未变化，ShowGold 也会先收到当前值 1250。
    [Observe(nameof(InventoryModel.Gold))]
    private void ShowGold(int gold) => _gold.SetInt(gold);

    // 可以同时观察多个值：其中任意一个变化，方法都会拿到两者的最新值。
    [Observe(nameof(InventoryModel.UsedSlots), nameof(InventoryModel.Capacity))]
    private void ShowSlots(int used, int capacity) => _slots.SetPair(used, capacity);
}

// 这个 Widget 只负责把用户输入翻译成 Model Action，不在 View 中修改业务状态。
[View(typeof(InventoryModel))]
public partial class InventoryActions : LuminView
{
    [UiElement("Use")]
    private Button _use = null!;

    [UiElement("Sort")]
    private Button _sort = null!;

    // [OnClick] 生成事件接线和解绑；Reactive 是生成器加到此 partial class 的强类型属性。
    // 它只暴露 Model 上标了 [LuminAction] 的操作，因此这里看不到任意写 Model 的入口。
    [OnClick(nameof(_use))]
    private void Use() => Reactive.UseSelected();

    [OnClick(nameof(_sort))]
    private void Sort() => Reactive.SortByName();
}

// 详情 Widget 展示当前选中物品。
[View(typeof(InventoryModel))]
public partial class InventoryDetails : LuminView
{
    [UiElement("Name")]
    private Label _name = null!;

    [UiElement("Count")]
    private Label _count = null!;

    // Select/UseSelected 修改 Selected.Value 后，这个方法自动再次执行。
    [Observe(nameof(InventoryModel.Selected))]
    private void ShowSelected(InventoryItem? item)
    {
        _name.Text = item?.Name ?? "请选择物品";
        _count.SetInt(item?.Count ?? 0);
    }
}

// 每一个列表 Cell 也是普通 Widget，并与根 Screen 共享 InventoryReactive。
// Cell 可能被对象池复用，所以 Show 必须把所有与旧数据有关的字段重新写一遍。
[View(typeof(InventoryModel))]
public partial class InventoryItemCell : LuminView
{
    [UiElement("Button")]
    private Button _button = null!;

    [UiElement("Name")]
    private Label _name = null!;

    [UiElement("Count")]
    private Label _count = null!;

    private int _itemId;

    // 这个方法就是上面 [BindList] 指定的 binder 最终调用的位置。
    public void Show(InventoryItem item)
    {
        _itemId = item.Id;
        _name.Text = item.Name;
        _count.SetInt(item.Count);
    }

    // Cell 不持有 InventoryModel，只保存当前 itemId，再通过生成的 Action 请求选择物品。
    [OnClick(nameof(_button))]
    private void Select() => Reactive.Select(_itemId);
}

// 没有 Model 的 [Screen] 是纯展示界面：仍有控件绑定和生命周期，
// 但不会创建 Reactive，也不要求 OpenAsync 传入 Model。
[Screen(Layer = UILayer.Popup, Mode = UIMode.Overlay, Modal = true, CloseOnClickMask = true)]
public partial class InventoryHelp : LuminView
{
    [UiElement("Text")]
    private Label _text = null!;

    protected override void OnInit()
        => _text.Text = "点击物品查看详情；使用和排序会自动刷新所有相关 View。";
}

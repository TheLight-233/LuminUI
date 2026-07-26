# LuminUI

LuminUI 是面向 Unity 和保留式 UI 的高性能 MVR 框架。

- **Model**：私有可变状态和业务方法。
- **View**：Element、Widget、列表结构和 Render 方法。
- **Reaction**：数据源选择、生命周期订阅、展示状态组合和 UI 事件。
- **Source Generator**：生成只读 Model 投影、Reaction 宿主、元素与事件接线、Widget Tree、Screen 注册和强类型打开。

框架不使用运行时反射，不构建虚拟 DOM。响应通知稳定热路径以 **0 B 托管分配**为验收门槛。

## 最小示例

```csharp
[LuminModel]
public sealed partial class CounterModel
{
    private readonly ReactiveProperty<int> _count = new(0);

    public void Add() => _count.Value++;
}

public static class CounterContext
{
    public static CounterModel Model { get; } = new();
}
```

生成器把 `_count` 投影为只读属性：

```csharp
public IReadOnlyReactiveProperty<int> Count => _count;
```

View 不引用 Model，也没有 Subscribe API：

```csharp
[Screen]
public partial class CounterView : LuminView
{
    [Element("Count")]
    private Label _count = null!;

    [Element("Add")]
    internal Button AddButton = null!;

    internal void RenderCount(int value) => _count.SetInt(value);
}
```

Reaction 是独立文件中的逻辑类。用户不写基类、构造、实例化和释放：

```csharp
[ReactionFor(typeof(CounterView))]
public sealed partial class CounterReaction
{
    protected override void OnBind()
        => Subscribe(CounterContext.Model.Count, View.RenderCount);

    [OnClick(nameof(CounterView.AddButton))]
    private void Add() => CounterContext.Model.Add();
}
```

生成器为 Reaction 补上 `LuminReaction<CounterView>`，在 View 打开时 Attach，关闭或回池时 Detach，并直接生成按钮事件的 `+=` / `-=` 代码。

打开 Screen 不传 Model：

```csharp
LuminUIRuntime.RegisterAll();
var handle = await CounterView.OpenAsync();
```

## 生命周期

`Subscribe` 默认立即推送当前值，并返回 `SubscriptionHandle`。Reaction 可以在运行时提前取消或重新订阅。

- Hide、Show、Stack Cover 和 Reveal 不会解除订阅。
- Screen Close、回池、销毁、Widget/Cell 卸载会自动解除全部订阅。
- 池化 View 再次打开时复用 Reaction 对象并重新执行 `OnBind`。
- 属性、集合、字典和 EventBus 监听都由 Reaction 生命周期托管。

## 强规则

- `[LuminModel]` 必须是顶层非泛型 `partial class`，显式字段必须为 `private`。
- 一个 View 最多关联一个 `[ReactionFor]`。
- Reaction 不能声明 `ReactiveProperty`、`ReactiveCollection` 或 `ReactiveDictionary`。
- View 不能直接 Subscribe；有 Reaction 的 View 不能再声明 `[OnClick]` 等事件方法。
- 纯展示 View 可以没有 Reaction。
- Reaction 可以选择任意数量和任意实例的 Model，Screen Open 不携带 Model 图。

## Widget Tree

`[Widget]` 由生成器创建和挂载，框架维护 `Parent` / `Children`。Widget 可以继续声明 Widget；运行时可选组件使用 `AddWidget`。显示隐藏只改变节点可见性，不解除 Reaction。

`LuminWidgetList` 管理响应式集合的增量更新和 Cell 池。View 创建列表结构，Reaction 选择并绑定集合。

## 目录

```text
src/          运行时与 Roslyn 4.3.0 源生成器
samples/      Model / View / Reaction 三层背包示例
tests/        生成、生命周期、池化和 0GC 门禁
benchmarks/   性能与分配基准
docs/         架构、入门和迁移文档
```

## 验证

```bash
dotnet restore LuminUI.sln
dotnet build LuminUI.sln -c Release --no-restore
dotnet test LuminUI.sln -c Release --no-build
dotnet run -c Release --project benchmarks/LuminUI.Benchmarks
```

完整示例见 `samples/LuminUI.Samples.Inventory`。

## License

MIT，见 [LICENSE](LICENSE)。

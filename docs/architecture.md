# MVR architecture

## 数据方向

```text
View event -> generated Reactive action -> Model mutation
                                         |
                                         v
View render <- generated observer <- ReactiveProperty/Collection/Dictionary
```

Reactive 是每次 Screen 打开时租借的上下文。根 Screen 拥有它；所有相同 Model 的 Widget 和列表 Cell 自动借用。关闭顺序固定为：

1. 停止 View 的响应订阅；
2. 卸载 Widget/Cell 树；
3. 清除各 View 的 Reactive 引用；
4. Reactive 解除 Model 引用并回池；
5. View 和根节点按 Screen 配置回池或销毁。

## 三类 View

- `[Screen] class X : LuminView`：无 Model 的纯 Screen。
- `[Screen(typeof(MyModel))]`：拥有生成的 `MyReactive`。
- `[View(typeof(MyModel))]`：借用父级同一个 `MyReactive` 的 Widget/Cell。

纯 `[View]` 只生成节点和事件代码，不被迫接入状态系统。

## 生成器职责

- Model → 只读 Reactive 投影；
- `[LuminAction]` → 明确允许的动作代理；
- `[Observe]` → 缓存委托、订阅、一次初始 Render、自动退订；
- `[UiWidget]` → 自动挂载并继承 Reactive；
- `[BindList]` → 自动创建池化 Cell 列表并进行增量更新；
- `[Screen]` → 元数据注册、资源名、强类型 `OpenAsync`；
- `[UiElement]`/事件特性 → 无反射控件查找和事件接线。

## 0GC 边界

热路径禁止 EventArgs、反射、LINQ、装箱和临时闭包。容器构造时可传容量，生成器缓存所有实例委托。性能门禁在 `tests/LuminUI.Tests/ReactiveAllocationTests.cs`，独立基准在 `benchmarks/`。

LuminUI 延续游戏 UI 的主线程模型：Model Action、Reactive 通知和 View 渲染应在平台 UI 主线程执行。容器为此不引入锁及其额外开销；后台任务应先切回主线程再修改 Model。

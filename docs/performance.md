# Performance guide

## 已由测试锁定的 0 B 热路径

- `ReactiveProperty<T>`：比较、赋值、通知；
- `ReactiveCollection<T>`：已有容量内的替换、移动、通知；
- `ReactiveDictionary<TKey,TValue>`：预留容量后的查询、替换、通知；
- `Model mutation → ReactiveProperty → Reaction → View` 的生命周期订阅链路。

运行门禁：

```bash
dotnet test LuminUI.sln -c Release
dotnet run -c Release --project benchmarks/LuminUI.Benchmarks
```

## 使用规则

1. 已知最大规模时给 Collection/Dictionary 传容量。
2. 数字文本使用平台提供的无分配格式化接口；不要在每帧观察方法里进行字符串插值。
3. 用方法组建立 `Subscribe`，不在热路径创建捕获 lambda。
4. UI 状态在主线程修改，避免为响应容器增加锁。

## 冷路径

首次类型初始化、首次委托缓存、Screen/资源创建、容量扩张、异步等待源以及用户主动进行的字符串格式化允许分配。Screen、Widget 和列表 Cell 会池化，以避免重复打开时重新创建主要 UI 对象。

基准数字依赖 CPU 和运行时，验收以每项 `0 B` 为稳定指标，而不是固定耗时。

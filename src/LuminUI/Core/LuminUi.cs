using System;
using System.Collections.Generic;
using System.Threading;
using LuminThread;
using LuminUI.Event;
using LuminUI.Interface;

namespace LuminUI
{
    // UI 总管理器：屏注册、打开/关闭、层级与栈、模态遮罩、过渡输入锁、对象池、覆盖/露出、
    // 返回值、语言广播、错误路由、Domain Reload 复位。约定主线程使用，内部无锁。
    public static class LuminUi
    {
        private static readonly Dictionary<int, ScreenEntry> _screens = new Dictionary<int, ScreenEntry>();
        private static readonly Dictionary<UILayer, LinkedList<int>> _stacks = new Dictionary<UILayer, LinkedList<int>>();
        private static readonly Dictionary<UILayer, int> _orders = new Dictionary<UILayer, int>();
        private static readonly Dictionary<Type, ScreenMeta> _metas = new Dictionary<Type, ScreenMeta>();
        private static readonly Stack<ScreenEntry> _entryPool = new Stack<ScreenEntry>();

        private static int _nextId;       // 单调递增，从不复用 —— 句柄按此代际安全
        private static int _inputLock;    // 过渡输入锁重入计数
        private static IUiLoader? _loader;
        private static IUiBridge? _bridgeOverride;   // 仅测试用

        internal static bool IsTesting => _bridgeOverride != null;

        // 即发即忘关闭/动画路径里的未捕获异常回调。默认丢弃。
        public static Action<Exception>? OnError;

        public static void SetLoader(IUiLoader loader)
            => _loader = loader ?? throw new ArgumentNullException(nameof(loader));

        // 由生成器在 LuminUIRuntime.RegisterAll() 中调用。
        public static void RegisterScreen<T>(in ScreenOptions opt,
                                             Func<LuminView> viewFactory,
                                             Func<LuminReactive>? reactiveFactory = null) where T : LuminView
            => _metas[typeof(T)] = new ScreenMeta(opt, viewFactory, reactiveFactory);

        // 异步打开屏。任意阶段失败/取消都会完整回滚，不留幽灵 Entry、不泄漏 VM/栈节点/根节点/遮罩。
        public static async LuminTask<ScreenHandle<T>> OpenAsync<T>(object? model = null, CancellationToken ct = default)
            where T : LuminView
        {
            ct.ThrowIfCancellationRequested();
            var meta = GetMeta<T>();

            LuminReactive? reactive = null;
            object? root = null;
            object? mask = null;
            LuminView? view = null;
            ScreenEntry? entry = null;
            int id = 0;
            bool fromPool = false;
            bool registered = false;
            bool inStack = false;
            int coveredTop = 0;

            LockInput();
            try
            {
                try
                {
                    if (meta.HasReactive)
                    {
                        if (model == null)
                            throw new ArgumentNullException(nameof(model),
                                "[LuminUI] Screen " + typeof(T).Name + " requires a model.");
                        reactive = meta.RentReactive(model);
                    }

                    // 模态：遮罩拿较低 order，屏拿较高 order
                    int order;
                    if (meta.Modal)
                    {
                        int maskOrder = NextOrder(meta.Layer);
                        order = NextOrder(meta.Layer);
                        mask = GetBridge().CreateMask(meta.Layer, maskOrder, meta.MaskOpacity);
                    }
                    else
                    {
                        order = NextOrder(meta.Layer);
                    }

                    fromPool = meta.TryRentFromPool(out var pooled, out root);
                    if (fromPool)
                    {
                        view = pooled;
                        GetBridge().SetVisible(root, true);
                    }
                    else
                    {
                        string resourceName = string.IsNullOrEmpty(meta.ResourceName)
                            ? typeof(T).Name
                            : meta.ResourceName;
                        root = await GetLoader().LoadAsync(resourceName, meta.Layer, ct);
                        ct.ThrowIfCancellationRequested();
                        if (root == null)
                            throw new InvalidOperationException("[LuminUI] Loader returned null root for " + typeof(T).Name + ".");
                        view = (T)meta.CreateView();
                    }

                    var bridge = GetBridge();
                    bridge.SetOrder(root!, meta.Layer, order);
                    bridge.SetLayout(root!, meta.X, meta.Y, meta.Width, meta.Height);

                    view.Layer = meta.Layer;
                    view.Mode = meta.Mode;
                    view.__AssignReactive(reactive);

                    id = Interlocked.Increment(ref _nextId);
                    entry = RentEntry();
                    entry.Completion = new ScreenCompletion();
                    view.__SetScreen(id, entry.Completion);
                    entry.Id = id;
                    entry.View = view;
                    entry.Reactive = reactive;
                    entry.Root = root!;
                    entry.Mask = mask;
                    entry.Meta = meta;
                    _screens[id] = entry;
                    registered = true;

                    // 点遮罩关闭：用 entry 上缓存的委托，零额外分配（不产生闭包）
                    if (mask != null && meta.CloseOnClickMask)
                        bridge.SetMaskClickHandler(mask, entry.MaskClick);

                    if (meta.Mode == UIMode.Stack)
                    {
                        coveredTop = CoverStackTop(meta.Layer);
                        entry.StackNode = GetStack(meta.Layer).AddLast(id);
                        inStack = true;
                    }

                    await view.__Open(bridge, root!, ct);
                    return new ScreenHandle<T>((T)view, id, entry.Completion);
                }
                catch
                {
                    if (inStack && entry?.StackNode != null && _stacks.TryGetValue(meta.Layer, out var sl))
                        sl.Remove(entry.StackNode);
                    if (coveredTop != 0) RevealById(coveredTop);
                    if (registered) _screens.Remove(id);

                    if (view != null)
                    {
                        if (meta.CanPool && root != null)
                        {
                            view.__PrepareForPool();
                            if (meta.TryReturnToPool(view, root)) GetBridge().SetVisible(root, false);
                            else
                            {
                                view.__DestroyFromPool();
                                GetLoader().Unload(root);
                            }
                        }
                        else
                        {
                            view.__DestroyImmediate();
                            if (root != null) GetLoader().Unload(root);
                        }
                    }
                    else if (root != null)
                    {
                        GetLoader().Unload(root);
                    }

                    if (mask != null) GetBridge().Destroy(mask);
                    if (reactive != null) meta.ReturnReactive(reactive);
                    if (entry != null) ReturnEntry(entry);
                    throw;
                }
            }
            finally
            {
                UnlockInput();
            }
        }

        // 打开并等待返回值（弹窗确认/取消等）。屏内部用 SetResult(...) + Close()。
        public static async LuminTask<TResult?> OpenForResultAsync<TScreen, TResult>(
            object? model = null, CancellationToken ct = default) where TScreen : LuminView
        {
            var h = await OpenAsync<TScreen>(model, ct);
            return await h.WaitForResultAsync<TResult>();
        }

        public static LuminTask PreloadAsync<T>(CancellationToken ct = default) where T : LuminView
        {
            var meta = GetMeta<T>();
            string resourceName = string.IsNullOrEmpty(meta.ResourceName)
                ? typeof(T).Name
                : meta.ResourceName;
            return GetLoader().PreloadAsync(resourceName, meta.Layer, ct);
        }

        internal static void CloseById(int id) => _ = CloseSafe(id);

        private static async LuminTask CloseSafe(int id)
        {
            try { await CloseByIdAsync(id, CancellationToken.None); }
            catch (Exception ex) { OnError?.Invoke(ex); }
        }

        internal static async LuminTask<bool> CloseByIdAsync(int id, CancellationToken ct)
        {
            if (!_screens.TryGetValue(id, out var e)) return false;
            if (e.View.IsClosing) return false;

            LockInput();
            try { await e.View.__BeginClose(ct); }
            finally { UnlockInput(); }

            if (!_screens.Remove(id)) return false;  // 期间已被别的路径移除

            if (e.Meta.Mode == UIMode.Stack && e.StackNode != null
                && _stacks.TryGetValue(e.Meta.Layer, out var list))
            {
                list.Remove(e.StackNode);
                e.StackNode = null;
                if (list.Last != null) RevealById(list.Last.Value);
            }

            object? result = e.View.__Result;
            if (e.Meta.CanPool)
            {
                e.View.__PrepareForPool();
                if (e.Meta.TryReturnToPool(e.View, e.Root)) GetBridge().SetVisible(e.Root, false);
                else
                {
                    e.View.__DestroyFromPool();
                    GetLoader().Unload(e.Root);
                }
            }
            else
            {
                e.View.__DestroyImmediate();
                GetLoader().Unload(e.Root);
            }

            if (e.Reactive != null) e.Meta.ReturnReactive(e.Reactive);
            e.Completion.Complete(result);

            if (e.Mask != null) GetBridge().Destroy(e.Mask);
            ReturnEntry(e);
            return true;
        }

        // 关闭某层栈顶（导航返回）。
        public static bool Back(UILayer layer = UILayer.Popup)
        {
            if (!_stacks.TryGetValue(layer, out var list) || list.Last == null) return false;
            CloseById(list.Last.Value);
            return true;
        }

        public static async LuminTask<bool> BackAsync(UILayer layer = UILayer.Popup, CancellationToken ct = default)
        {
            if (!_stacks.TryGetValue(layer, out var list) || list.Last == null) return false;
            return await CloseByIdAsync(list.Last.Value, ct);
        }

        public static void CloseAll()
        {
            var ids = RentIdBuffer();
            ids.AddRange(_screens.Keys);
            for (int i = 0; i < ids.Count; i++) CloseById(ids[i]);
            ReturnIdBuffer(ids);
        }

        public static async LuminTask CloseAllAsync(CancellationToken ct = default)
        {
            var ids = RentIdBuffer();
            ids.AddRange(_screens.Keys);
            for (int i = 0; i < ids.Count; i++) await CloseByIdAsync(ids[i], ct);
            ReturnIdBuffer(ids);
        }

        public static void CloseAll<T>() where T : LuminView
        {
            var ids = RentIdBuffer();
            foreach (var kv in _screens)
                if (kv.Value.View is T) ids.Add(kv.Key);
            for (int i = 0; i < ids.Count; i++) CloseById(ids[i]);
            ReturnIdBuffer(ids);
        }

        public static T? GetFirst<T>() where T : LuminView
        {
            foreach (var kv in _screens)
                if (kv.Value.View is T t) return t;
            return null;
        }

        public static bool HasAny<T>() where T : LuminView => GetFirst<T>() != null;

        internal static LinkedList<int> GetStack(UILayer layer)
        {
            if (!_stacks.TryGetValue(layer, out var l)) { l = new LinkedList<int>(); _stacks[layer] = l; }
            return l;
        }

        // 用户主动显隐（走 OnShow/OnHide）
        internal static void ShowById(int id)
        {
            if (!_screens.TryGetValue(id, out var e)) return;
            GetBridge().SetVisible(e.Root, true);
            if (e.Mask != null) GetBridge().SetActive(e.Mask, true);
            e.View.__Show();
        }

        internal static void HideById(int id)
        {
            if (!_screens.TryGetValue(id, out var e)) return;
            e.View.__Hide();
            if (e.Mask != null) GetBridge().SetActive(e.Mask, false);
            GetBridge().SetVisible(e.Root, false);
        }

        // 栈驱动的覆盖/露出（走 OnCovered/OnReveal）
        internal static void CoverById(int id)
        {
            if (!_screens.TryGetValue(id, out var e)) return;
            e.View.__Cover();
            if (e.Meta.HideWhenCovered)
            {
                GetBridge().SetVisible(e.Root, false);
                if (e.Mask != null) GetBridge().SetActive(e.Mask, false);
            }
        }

        internal static void RevealById(int id)
        {
            if (!_screens.TryGetValue(id, out var e)) return;
            if (e.Meta.HideWhenCovered)
            {
                if (e.Mask != null) GetBridge().SetActive(e.Mask, true);
                GetBridge().SetVisible(e.Root, true);
            }
            e.View.__Reveal();
        }

        private static int CoverStackTop(UILayer layer)
        {
            if (!_stacks.TryGetValue(layer, out var list) || list.Last == null) return 0;
            int top = list.Last.Value;
            CoverById(top);
            return top;
        }

        // 切语言后调一次，级联刷新所有打开屏及其组件树/列表 cell。
        public static void BroadcastLanguageChanged()
        {
            foreach (var kv in _screens) kv.Value.View.__LanguageCascade();
        }

        internal static bool IsAlive(int id) => _screens.ContainsKey(id);

        internal static bool TryGetState(int id, out LuminViewState state)
        {
            if (_screens.TryGetValue(id, out var e)) { state = e.View.State; return true; }
            state = LuminViewState.None;
            return false;
        }

        public static void ClearPool<T>() where T : LuminView
        {
            if (_metas.TryGetValue(typeof(T), out var m)) m.DrainPool(UnloadRoot);
        }

        public static void ClearAllPools()
        {
            foreach (var m in _metas.Values) m.DrainPool(UnloadRoot);
        }

        public static void Reset()
        {
            foreach (var kv in _screens)
            {
                var e = kv.Value;
                var result = e.View.__Result;
                e.View.__DestroyImmediate();
                if (e.Reactive != null) e.Meta.ReturnReactive(e.Reactive);
                e.Completion.Complete(result);
                try { _loader?.Unload(e.Root); } catch { }
                if (e.Mask != null) { try { GetBridge().Destroy(e.Mask); } catch { } }
            }
            foreach (var m in _metas.Values)
                m.DrainPool(r => { try { _loader?.Unload(r); } catch { } });

            _screens.Clear();
            _stacks.Clear();
            _orders.Clear();
            _metas.Clear();
            _entryPool.Clear();
            _nextId = 0;
            _inputLock = 0;
        }

        private static void LockInput()   { if (_inputLock++ == 0) GetBridge().SetInputLock(true); }
        private static void UnlockInput() { if (_inputLock > 0 && --_inputLock == 0) GetBridge().SetInputLock(false); }

        private static int NextOrder(UILayer layer)
        {
            _orders.TryGetValue(layer, out var v);
            v++;
            _orders[layer] = v;
            return v;
        }

        private static ScreenMeta GetMeta<T>() where T : LuminView
        {
            if (_metas.TryGetValue(typeof(T), out var m)) return m;
            throw new InvalidOperationException(
                "[LuminUI] Screen not registered: " + typeof(T).Name +
                ". Is it marked [Screen] and did LuminUIRuntime.RegisterAll() run?");
        }

        private static IUiLoader GetLoader()
            => _loader ?? throw new InvalidOperationException("[LuminUI] Loader not set. Call LuminUi.SetLoader().");

        private static IUiBridge GetBridge()
            => _bridgeOverride ?? UiBridgeRegistry.Bridge;

        // 不捕获局部变量，编译器缓存为静态委托，DrainPool 调用无分配
        private static readonly Action<object> UnloadRoot = root => GetLoader().Unload(root);

        private static ScreenEntry RentEntry()
            => _entryPool.Count > 0 ? _entryPool.Pop() : new ScreenEntry();

        private static void ReturnEntry(ScreenEntry e)
        {
            e.Clear();
            if (_entryPool.Count < 32) _entryPool.Push(e);
        }

        private static List<int>? _idBuffer;
        private static List<int> RentIdBuffer() { var b = _idBuffer ?? new List<int>(); _idBuffer = null; b.Clear(); return b; }
        private static void ReturnIdBuffer(List<int> b) => _idBuffer = b;

        public static void SetBridgeForTesting(IUiBridge bridge) => _bridgeOverride = bridge;

        public static void ResetForTesting()
        {
            Reset();
            EventBus.ResetAll();
            _bridgeOverride = null;
            _loader = null;
            OnError = null;
        }

        // 一条打开屏的运行时记录，池化复用；MaskClick 委托在对象首次创建时绑定一次。
        private sealed class ScreenEntry
        {
            public int Id;
            public LuminView View = null!;
            public LuminReactive? Reactive;
            public ScreenCompletion Completion = null!;
            public object Root = null!;
            public object? Mask;
            public ScreenMeta Meta = null!;
            public LinkedListNode<int>? StackNode;

            public readonly Action MaskClick;

            public ScreenEntry() => MaskClick = OnMaskClick;

            private void OnMaskClick() => CloseById(Id);

            public void Clear()
            {
                Id = 0;
                View = null!;
                Reactive = null;
                Completion = null!;
                Root = null!;
                Mask = null;
                Meta = null!;
                StackNode = null;
            }
        }
    }
}

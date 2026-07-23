using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LuminThread;
using LuminThread.Interface;
using LuminUI.Event;
using LuminUI.Interface;
using LuminUI.Internal;
using LuminUI.Localization;

namespace LuminUI
{
    // 唯一的视图基类。一种视图，两种角色：
    //   被 LuminUi.OpenAsync 打开 → “屏”(IsRoot=true)：PlayerLoop 项，参与层级/栈/动画/返回值/覆盖。
    //   被父视图 AddWidget 挂载   → “组件”(IsRoot=false)：同步挂载，不单独进循环，由父向下驱动 Update。
    // 用 [Screen] 标记可打开，仅 [View] 则为组件。纯 C#，零 MonoBehaviour，零反射。
    public abstract class LuminView : IPlayLoopItem
    {
        public LuminViewState State { get; internal set; } = LuminViewState.None;

        public bool IsOpen => State == LuminViewState.Open || State == LuminViewState.Hidden;
        public bool IsVisible => State == LuminViewState.Open;
        public bool IsClosing => State == LuminViewState.Closing;

        // 作为屏打开（true）还是作为组件挂载（false）
        public bool IsRoot { get; internal set; }
        // 是否正被栈上层覆盖（覆盖时停 Update）。仅对屏有意义。
        public bool IsCovered => _covered;
        // 父视图（组件有效，屏为 null）
        public LuminView? Parent { get; internal set; }

        public UILayer Layer { get; internal set; }
        public UIMode Mode { get; internal set; }

        // OnInit 之后可用
        protected IUiBridge Bridge { get; private set; } = null!;
        protected object Root { get; private set; } = null!;

        private LuminReactive? _reactiveContext;

        // 生成器填充
        public virtual void __Bind(IUiBridge bridge, object root) { }
        public virtual void __WireEvents() { }
        public virtual void __UnwireEvents() { }
        public virtual void __WireReactive() { }
        public virtual void __UnwireReactive() { }
        public virtual void __BuildWidgets() { }
        public virtual void __ClearWidgets() { }
        public virtual bool __RequiresReactive => false;
        public virtual void __SetReactiveObj(LuminReactive reactive) { }
        public virtual void __ClearReactiveObj() { }


        // 生命周期钩子
        protected virtual void OnInit() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnDestroy() { }
        // 被栈上层覆盖 / 重新露出（屏专用，区别于用户主动 Hide 的 OnHide/OnShow）
        protected virtual void OnCovered() { }
        protected virtual void OnReveal() { }
        protected virtual void OnLanguageChanged() { }
        // 入场/退场动画，默认立即完成（屏专用，组件同步挂载不走动画）
        protected virtual LuminTask<bool> OnOpenAnimation(CancellationToken ct) => LuminTask.FromResult(true);
        protected virtual LuminTask<bool> OnCloseAnimation(CancellationToken ct) => LuminTask.FromResult(true);

        // 绑定登记。回池/销毁时统一退订，无内存泄漏。这些列表在视图实例上复用（Clear 而非置空）。
        private List<(object prop, object handler, Action<object, object> unsub)>? _propSubs;
        private List<(object handler, Action<object> unsub)>? _eventSubs;
        private List<LuminWidgetListBase>? _lists;
        private List<LuminView>? _children;

        public IReadOnlyList<LuminView> Children
            => (IReadOnlyList<LuminView>?)_children ?? Array.Empty<LuminView>();

        // 手动订阅入口；常规代码优先用 [Observe] 让生成器完成订阅与退订。
        protected void Bind<T>(IReadOnlyReactiveProperty<T> prop, Action<T> onChanged)
        {
            _propSubs ??= new List<(object, object, Action<object, object>)>();
            _propSubs.Add((prop, onChanged, PropHelper<T>.Do));
            prop.Subscribe(onChanged);
        }

        // 可选跨系统事件订阅（struct），关闭时自动退订。
        protected void Listen<T>(Action<T> handler) where T : struct
        {
            _eventSubs ??= new List<(object, Action<object>)>();
            _eventSubs.Add((handler, UnsubHelper<T>.Do));
            EventBus.Subscribe(handler);
        }

        protected string L(string key) => LocalizationManager.Get(key);
        protected string LFormat(string key, params object[] args) => LocalizationManager.Format(key, args);

        // 在父视图节点下创建并挂载子组件。父 OnInit 中调用，子组件的绑定一并同步完成。
        // 传 new TView() 而非泛型 new()，构造发生在调用处的具体类型，保持零反射、IL2CPP 安全。
        protected TW AddWidget<TW>(TW widget, string childPath) where TW : LuminView
        {
            var node = Bridge.FindNode(Root, childPath)
                       ?? throw new InvalidOperationException("[LuminUI] Child node not found: " + childPath);
            return AddWidget(widget, node);
        }

        protected TW AddWidget<TW>(TW widget, object node) where TW : LuminView
        {
            _children ??= new List<LuminView>();
            _children.Add(widget);
            widget.__Mount(Bridge, node, this);
            return widget;
        }


        // 可复用 cell 列表（cell 即组件），ReactiveCollection 增量驱动
        protected LuminWidgetList<TW, TI> BindList<TW, TI>(
            string containerPath, string templatePath,
            Func<TW> factory, Action<TW, TI, int> binder, int maxIdle = 8)
            where TW : LuminView
        {
            var container = Bridge.FindNode(Root, containerPath)
                            ?? throw new InvalidOperationException("[LuminUI] List container not found: " + containerPath);
            var template = Bridge.FindNode(Root, templatePath)
                           ?? throw new InvalidOperationException("[LuminUI] List template not found: " + templatePath);
            var list = new LuminWidgetList<TW, TI>(this, Bridge, container, template, factory, binder, maxIdle);
            RegisterList(list);
            return list;
        }

        protected void RegisterList(LuminWidgetListBase list)
        {
            _lists ??= new List<LuminWidgetListBase>();
            _lists.Add(list);
        }

        // PlayerLoop —— 仅屏被打开后进入循环；组件不进入。
        private bool _inLoop;
        private bool _covered;
        private int _screenId;
        private LuminViewState _stateBeforeClose;
        private object? _result;
        private ScreenCompletion? _completion;

        bool IPlayLoopItem.MoveNext()
        {
            if (State != LuminViewState.Open) { _inLoop = false; return false; }
            if (_covered) return true;   // 覆盖时停 Update，仍留在循环（仅一次分支开销）
            __Tick();
            return true;
        }

        private void EnterLoop()
        {
            if (_inLoop) return;
            _inLoop = true;
            if (LuminUi.IsTesting) return;
            PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, this);
        }

        // 返回值（弹窗结果，屏专用）
        protected void SetResult(object? result) => _result = result;
        protected void Close()
        {
            if (!IsRoot || _screenId == 0)
                throw new InvalidOperationException("[LuminUI] Only an opened root screen can close itself.");
            LuminUi.CloseById(_screenId);
        }

        protected void Close(object? result)
        {
            SetResult(result);
            Close();
        }

        protected LuminTask<bool> CloseAsync(CancellationToken ct = default)
        {
            if (!IsRoot || _screenId == 0) return LuminTask.FromResult(false);
            return LuminUi.CloseByIdAsync(_screenId, ct);
        }

        internal void __SetScreen(int id, ScreenCompletion completion)
        {
            _screenId = id;
            _completion = completion;
        }
        internal object? __Result => _result;

        public LuminTask<bool> WaitForCloseAsync() => WaitClose();

        private async LuminTask<bool> WaitClose()
        {
            await ResultTask();
            return true;
        }

        // 关闭时（动画完成 + 回池/销毁）完成。唯一用到完成源的地方。
        internal Task<object?> ResultTask()
            => _completion?.WaitAsync() ?? Task.FromResult<object?>(null);

        // 屏生命周期（LuminUi 驱动）。新建与池复用共用 __Open 入口。
        internal async LuminTask<bool> __Open(IUiBridge bridge, object root, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            IsRoot = true;
            _covered = false;
            _result = null;
            State = LuminViewState.Opening;
            __BindCore(bridge, root);
            __WireEvents();
            __BuildWidgets();
            OnInit();
            __WireReactive();
            await OnOpenAnimation(ct);
            State = LuminViewState.Open;
            EnterLoop();
            return true;
        }

        internal void __Show()
        {
            if (State != LuminViewState.Hidden) return;
            State = LuminViewState.Open;
            EnterLoop();
            OnShow();
        }

        internal void __Hide()
        {
            if (State != LuminViewState.Open) return;
            State = LuminViewState.Hidden;   // MoveNext 下一帧返回 false，自动脱离循环
            OnHide();
        }

        internal void __Cover()  { if (_covered) return; _covered = true; OnCovered(); }
        internal void __Reveal() { if (!_covered) return; _covered = false; OnReveal(); }

        internal async LuminTask<bool> __BeginClose(CancellationToken ct)
        {
            if (State == LuminViewState.Closing) return false;
            _stateBeforeClose = State;
            State = LuminViewState.Closing;
            try
            {
                await OnCloseAnimation(ct);
                return true;
            }
            catch
            {
                State = _stateBeforeClose;
                if (State == LuminViewState.Open) EnterLoop();
                throw;
            }
        }

        // 回池前：退订、卸载子组件、解事件、重置状态（保留 C# 对象供复用）。
        internal void __PrepareForPool()
            => __Release(false);

        private void __Release(bool destroy)
        {
            __UnwireReactive();
            __UnwireEvents();
            __FlushBindings(destroy);
            if (destroy) __ClearWidgets();
            __ClearReactive();
            State = LuminViewState.None;
            _inLoop = false;
            _covered = false;
            _screenId = 0;
            _stateBeforeClose = LuminViewState.None;
            _completion = null;
            _result = null;
        }

        // 彻底销毁：在回池基础上再调 OnDestroy。
        internal void __DestroyImmediate()
        {
            __Release(true);
            OnDestroy();
        }

        // View 已经执行过 __PrepareForPool，池被清空或容量溢出时补发最终销毁生命周期。
        internal void __DestroyFromPool()
        {
            __ClearWidgets();
            OnDestroy();
        }

        // 组件生命周期（父 AddWidget / 列表驱动）。同步挂载，无动画，不进循环。
        internal void __Mount(IUiBridge bridge, object root, LuminView parent)
        {
            Parent = parent;
            IsRoot = false;
            State = LuminViewState.Opening;
            __AssignReactive(parent._reactiveContext);
            __BindCore(bridge, root);
            __WireEvents();
            __BuildWidgets();
            OnInit();
            __WireReactive();
            State = LuminViewState.Open;
            OnShow();
        }

        internal void __Unmount(bool destroy = false)
        {
            if (State == LuminViewState.None) return;
            if (State == LuminViewState.Open) OnHide();
            __UnwireReactive();
            __UnwireEvents();
            __FlushBindings(destroy);
            if (destroy) __ClearWidgets();
            __ClearReactive();
            State = LuminViewState.None;
            Parent = null;
            OnDestroy();
        }

        internal void __BindCore(IUiBridge bridge, object root)
        {
            Bridge = bridge;
            Root = root;
            __Bind(bridge, root);
        }

        internal void __AssignReactive(LuminReactive? reactive)
        {
            _reactiveContext = reactive;
            if (!__RequiresReactive) return;
            if (reactive == null)
                throw new InvalidOperationException("[LuminUI] View requires reactive context: " + GetType().Name);
            __SetReactiveObj(reactive);
        }

        private void __ClearReactive()
        {
            if (__RequiresReactive) __ClearReactiveObj();
            _reactiveContext = null;
        }

        // 每帧：自身 OnUpdate 后递归驱动 Open 状态的子组件。
        internal void __Tick()
        {
            OnUpdate();
            if (_children == null) return;
            for (int i = 0; i < _children.Count; i++)
            {
                var c = _children[i];
                if (c.State == LuminViewState.Open) c.__Tick();
            }
        }

        // 语言级联：自身 + 组件树 + 列表活跃 cell
        internal void __LanguageCascade()
        {
            OnLanguageChanged();
            if (_lists != null)
                for (int i = 0; i < _lists.Count; i++) _lists[i].__LanguageCascade();
            if (_children != null)
                for (int i = 0; i < _children.Count; i++) _children[i].__LanguageCascade();
        }

        // 退订全部绑定、卸载全部子组件、释放全部列表。回池与销毁都会调用。
        internal void __FlushBindings(bool destroy)
        {
            if (_propSubs != null)
            {
                for (int i = 0; i < _propSubs.Count; i++)
                {
                    var b = _propSubs[i];
                    b.unsub(b.prop, b.handler);
                }
                _propSubs.Clear();
            }
            if (_eventSubs != null)
            {
                for (int i = 0; i < _eventSubs.Count; i++)
                {
                    var b = _eventSubs[i];
                    b.unsub(b.handler);
                }
                _eventSubs.Clear();
            }
            if (_lists != null)
            {
                for (int i = 0; i < _lists.Count; i++)
                {
                    if (destroy) _lists[i].__Dispose();
                    else _lists[i].__Suspend();
                }
                _lists.Clear();
            }
            if (_children != null)
            {
                for (int i = 0; i < _children.Count; i++) _children[i].__Unmount(destroy);
                _children.Clear();
            }
        }
    }

    // 手写 Reactive 类型时可用的泛型基类；使用 [View(typeof(Model))]/[Screen(typeof(Model))]
    // 时源生成器会在普通 LuminView partial 上生成等价代码。
    public abstract class LuminView<TReactive> : LuminView
        where TReactive : LuminReactive
    {
        protected TReactive Reactive { get; private set; } = null!;

        public override bool __RequiresReactive => true;

        public override void __SetReactiveObj(LuminReactive reactive)
            => Reactive = (TReactive)reactive;

        public override void __ClearReactiveObj()
            => Reactive = null!;
    }
}

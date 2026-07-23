using System;
using System.Collections.Generic;

namespace LuminUI
{
    // 屏注册配置。生成器在 RegisterAll 中以对象初始化器填充，便于无痛扩展字段。
    public struct ScreenOptions
    {
        public string? ResourceName;
        public UILayer Layer;
        public UIMode Mode;
        public int PoolSize;
        public bool Modal;
        public bool CloseOnClickMask;
        public float MaskOpacity;
        public bool HideWhenCovered;
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

    // 每个屏类型的注册元数据 + View 实例池 + Reactive 上下文池。
    public sealed class ScreenMeta
    {
        public readonly string ResourceName;
        public readonly UILayer Layer;
        public readonly UIMode Mode;
        public readonly int PoolCapacity;
        public readonly bool Modal;
        public readonly bool CloseOnClickMask;
        public readonly float MaskOpacity;
        public readonly bool HideWhenCovered;
        public readonly float X, Y, Width, Height;

        public bool CanPool => PoolCapacity > 0;
        public bool HasReactive => _reactiveFactory != null;

        private readonly Func<LuminView> _viewFactory;
        private readonly Func<LuminReactive>? _reactiveFactory;

        private Queue<(LuminView view, object root)>? _pool;
        private Stack<LuminReactive>? _reactivePool;

        private const int ReactivePoolCap = 16;

        public ScreenMeta(in ScreenOptions opt,
                          Func<LuminView> viewFactory,
                          Func<LuminReactive>? reactiveFactory)
        {
            ResourceName = string.IsNullOrEmpty(opt.ResourceName) ? "" : opt.ResourceName!;
            Layer = opt.Layer;
            Mode = opt.Mode;
            PoolCapacity = opt.PoolSize < 0 ? 0 : opt.PoolSize;
            Modal = opt.Modal;
            CloseOnClickMask = opt.CloseOnClickMask;
            MaskOpacity = opt.MaskOpacity;
            HideWhenCovered = opt.HideWhenCovered;
            X = opt.X;
            Y = opt.Y;
            Width = opt.Width;
            Height = opt.Height;
            _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
            _reactiveFactory = reactiveFactory;
        }

        internal LuminView CreateView() => _viewFactory();

        internal LuminReactive? RentReactive(object model)
        {
            if (_reactiveFactory == null) return null;
            var reactive = _reactivePool != null && _reactivePool.Count > 0
                ? _reactivePool.Pop()
                : _reactiveFactory();
            reactive.__Attach(model);
            return reactive;
        }

        internal void ReturnReactive(LuminReactive reactive)
        {
            reactive.__Detach();
            if (_reactiveFactory == null) return;
            _reactivePool ??= new Stack<LuminReactive>();
            if (_reactivePool.Count < ReactivePoolCap) _reactivePool.Push(reactive);
        }

        internal bool TryRentFromPool(out LuminView view, out object root)
        {
            if (_pool != null && _pool.Count > 0)
            {
                var item = _pool.Dequeue();
                view = item.view;
                root = item.root;
                return true;
            }
            view = null!;
            root = null!;
            return false;
        }

        internal bool TryReturnToPool(LuminView view, object root)
        {
            if (!CanPool) return false;
            _pool ??= new Queue<(LuminView, object)>();
            if (_pool.Count >= PoolCapacity) return false;
            _pool.Enqueue((view, root));
            return true;
        }

        internal void DrainPool(Action<object> unload)
        {
            if (_pool != null)
                while (_pool.Count > 0)
                {
                    var item = _pool.Dequeue();
                    item.view.__DestroyFromPool();
                    unload(item.root);
                }
            _reactivePool?.Clear();
        }
    }
}

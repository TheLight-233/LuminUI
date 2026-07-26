using System.Collections.Generic;

namespace LuminUI.Notify
{
    /// <summary>
    /// 红点树节点。每个节点的聚合计数 Count = 自身计数 + 所有子节点 Count 之和；
    /// 叶子节点用 SetCount 设置自身计数，变化沿树自动向上传播。Count 是 ReactiveProperty，
    /// Reaction 在 OnBind 中 Subscribe(node.Count, View.RenderBadge) 即可。
    /// </summary>
    public sealed class RedDot
    {
        public string  Name   { get; }
        public RedDot? Parent { get; private set; }

        /// <summary>聚合计数（自身 + 子树）。绑定它即可驱动红点显隐与数字。</summary>
        public ReactiveProperty<int> Count { get; } = new ReactiveProperty<int>(0);
        /// <summary>是否激活（聚合计数 &gt; 0）。</summary>
        public bool Active => Count.Value > 0;

        private readonly Dictionary<string, RedDot> _children = new Dictionary<string, RedDot>();
        private int _self; // 本节点自身计数

        internal RedDot(string name, RedDot? parent) { Name = name; Parent = parent; }

        internal RedDot GetOrAddChild(string name)
        {
            if (!_children.TryGetValue(name, out var c))
            {
                c = new RedDot(name, this);
                _children[name] = c;
            }
            return c;
        }

        /// <summary>设置本节点自身计数（≥0），自动重算并向上传播。</summary>
        public void SetCount(int count)
        {
            if (count < 0) count = 0;
            if (_self == count) return;
            _self = count;
            Recalculate();
        }

        /// <summary>自身计数 +delta（可为负，结果不低于 0）。</summary>
        public void Add(int delta) => SetCount(_self + delta);

        private void Recalculate()
        {
            int sum = _self;
            foreach (var kv in _children) sum += kv.Value.Count.Value;
            if (sum != Count.Value)
            {
                Count.Value = sum;     // 触发订阅者（ReactiveProperty 仅在变化时通知）
                Parent?.Recalculate(); // 沿树向上
            }
        }
    }

    /// <summary>
    /// 全局红点树。按 "Mail/System" 这样的路径取/建节点。叶子 SetCount，父节点自动聚合。
    /// Domain Reload 关闭时在桥接复位里调用 Reset()。
    /// </summary>
    public static class RedDotTree
    {
        private static RedDot _root = new RedDot("", null);

        public static RedDot Root => _root;

        /// <summary>按路径取/建节点，如 "Mail/System"、"Mail/Friend"。</summary>
        public static RedDot Get(string path)
        {
            var node = _root;
            int start = 0;
            for (int i = 0; i <= path.Length; i++)
            {
                if (i == path.Length || path[i] == '/')
                {
                    if (i > start) node = node.GetOrAddChild(path.Substring(start, i - start));
                    start = i + 1;
                }
            }
            return node;
        }

        /// <summary>设置某路径叶子的自身计数。</summary>
        public static void SetCount(string path, int count) => Get(path).SetCount(count);

        /// <summary>清空整棵树（Domain Reload 复位）。</summary>
        public static void Reset() => _root = new RedDot("", null);
    }
}

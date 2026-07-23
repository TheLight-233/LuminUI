using System;
using LuminUI.Interface;

namespace LuminUI.Testing
{
    // 无引擎环境下代表一个视图节点，操作结果记录在字段上以便单元测试断言。
    public sealed class NullNode
    {
        public bool Active = true;
        public bool Visible = true;
        public int SiblingIndex;
        public int Order;
        public bool IsMask;
        public float Opacity;
        public Action? MaskClick;
        public float X, Y, Width, Height;
    }

    // 无引擎测试桥接：Find 返回 null（生成的 __WireEvents 用 if(field != null) 守卫，安全），
    // FindNode/Instantiate/CreateMask 返回 NullNode，使组件、列表、遮罩、布局逻辑都能在纯单测里跑。
    public sealed class NullUiBridge : IUiBridge
    {
        public bool InputLocked { get; private set; }

        public T? Find<T>(object root, string path) where T : class => null;
        public object? FindNode(object root, string path) => new NullNode();
        public object Instantiate(object template, object parent) => new NullNode();
        public void Destroy(object node) { }

        public void SetActive(object node, bool active)
        {
            if (node is NullNode n) n.Active = active;
        }

        public void SetSiblingIndex(object node, int index)
        {
            if (node is NullNode n) n.SiblingIndex = index;
        }

        public void SetVisible(object node, bool visible)
        {
            if (node is NullNode n) n.Visible = visible;
        }

        public void SetOrder(object node, UILayer layer, int order)
        {
            if (node is NullNode n) n.Order = order;
        }

        public void SetLayout(object node, float x, float y, float width, float height)
        {
            if (node is NullNode n) { n.X = x; n.Y = y; n.Width = width; n.Height = height; }
        }

        public object CreateMask(UILayer layer, int order, float opacity)
            => new NullNode { IsMask = true, Order = order, Opacity = opacity };

        public void SetMaskClickHandler(object mask, Action? onClick)
        {
            if (mask is NullNode n) n.MaskClick = onClick;
        }

        public void SetInputLock(bool locked) => InputLocked = locked;
    }
}

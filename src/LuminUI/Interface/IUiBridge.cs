using System;

namespace LuminUI.Interface
{
    // 平台视图桥接。核心层一切引擎相关的节点操作都走这里：查找、克隆、销毁、显隐、
    // 层内排序、布局，以及模态遮罩与全局输入锁。各平台实现一份（Unity 实现另行提供）。
    // 核心 DLL 不引用任何引擎类型。
    public interface IUiBridge
    {
        // 在 root 下按 path 查到 T 组件，找不到返回 null。path 即编辑器生成工具的钩子。
        T? Find<T>(object root, string path) where T : class;

        // 在 root 下按 path 查子节点，用于挂载组件或定位列表容器/模板。
        object? FindNode(object root, string path);

        // 以 template 克隆一个新节点挂到 parent 下，返回新节点（列表 cell 复用用）。
        object Instantiate(object template, object parent);

        void Destroy(object node);
        void SetActive(object node, bool active);

        // 同级序号，用于让列表 cell 的视觉顺序与数据顺序一致。
        void SetSiblingIndex(object node, int index);

        // 显隐一个屏/节点（不销毁）。打开/隐藏/覆盖时由 LuminUi 调用。
        void SetVisible(object node, bool visible);

        // 层内渲染排序（如 Canvas.sortingOrder = 层基准 + order）。每次打开按层递增分配。
        void SetOrder(object node, UILayer layer, int order);

        // 打开屏时应用布局：x/y 为锚定位移；width/height 大于 0 时覆盖尺寸，否则保留预制体原尺寸。
        void SetLayout(object node, float x, float y, float width, float height);

        // 在指定层创建一个全屏遮罩（拦截射线、可半透明压暗），返回遮罩节点。
        object CreateMask(UILayer layer, int order, float opacity);

        // 设置遮罩点击回调（点遮罩关闭）。传 null 表示不响应点击。
        void SetMaskClickHandler(object mask, Action? onClick);

        // 开/关全局输入拦截（最顶层全屏射线屏蔽）。过渡动画期间由 LuminUi 自动开启。
        void SetInputLock(bool locked);
    }
}

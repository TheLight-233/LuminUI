using System;

namespace LuminUI.Attributes
{
    // 通用视图标记：参与代码生成（字段绑定 / 事件接线）。
    // 只标 [View] 的视图是“组件”——不可被 LuminUi 打开，只能由父视图 AddWidget 挂载。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ViewAttribute : Attribute
    {
        public Type? ModelType { get; }
        public string? Name { get; set; }   // 资源名，默认用类名；组件一般不需要单独资源

        public ViewAttribute() { }
        public ViewAttribute(Type modelType)
            => ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
    }

    // 可打开的“屏”：参与代码生成 + 运行时注册（LuminUi.OpenAsync 可开）。
    // [Screen] 隐含 [View]，屏只写这一个特性即可。显示相关配置全部集中在此。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ScreenAttribute : Attribute
    {
        public Type? ModelType { get; }
        public string? Name { get; set; }
        public UILayer Layer { get; set; } = UILayer.Scene;
        public UIMode Mode { get; set; } = UIMode.Normal;

        // 对象池容量：默认 1（关闭后留 1 个供秒开），0 = 禁用池化，>1 = 多实例缓存
        public int PoolSize { get; set; } = 1;

        // 模态遮罩
        public bool Modal { get; set; }
        public bool CloseOnClickMask { get; set; }     // 仅 Modal 生效
        public float MaskOpacity { get; set; } = 0.5f; // 仅 Modal 生效，压暗 0..1

        // 被栈上层覆盖时是否隐藏以省 draw call（Stack 模式）；false 则仅停 Update、保持渲染
        public bool HideWhenCovered { get; set; } = true;

        // 显示布局，打开时由桥接 SetLayout 落地
        public float X { get; set; }              // 锚定位置 X 偏移，默认 0
        public float Y { get; set; }              // 锚定位置 Y 偏移，默认 0
        public float Width { get; set; }          // 覆盖宽度，0 = 用预制体原尺寸
        public float Height { get; set; }         // 覆盖高度，0 = 用预制体原尺寸

        public ScreenAttribute() { }
        public ScreenAttribute(Type modelType)
            => ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
    }

    // 标记一个 MVR Model，生成器据此产出可共享的只读 Reactive 投影。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class LuminModelAttribute : Attribute { }

    /// <summary>显式允许 View 通过生成的 Reactive 投影调用这个 Model Action。</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class LuminActionAttribute : Attribute { }

    /// <summary>当一个或多个 Model 响应式成员变化时调用标记的方法。</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ObserveAttribute : Attribute
    {
        public string[] Sources { get; }
        public ObserveAttribute(params string[] sources)
            => Sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    /// <summary>自动在指定路径挂载 Widget；相同 Model 的 Reactive 上下文自动共享。</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class UiWidgetAttribute : Attribute
    {
        public string Path { get; }
        public UiWidgetAttribute(string path)
            => Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>把 ReactiveCollection 自动连接到可池化 Widget 列表。</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class BindListAttribute : Attribute
    {
        public string Source { get; }
        public string ContainerPath { get; }
        public string TemplatePath { get; }
        public int MaxIdle { get; set; } = 8;

        public BindListAttribute(string source, string containerPath, string templatePath)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ContainerPath = containerPath ?? throw new ArgumentNullException(nameof(containerPath));
            TemplatePath = templatePath ?? throw new ArgumentNullException(nameof(templatePath));
        }
    }

    // 标记桥接实现类型（生成器装配默认桥接，可选）。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class LuminUIBridgeAttribute : Attribute { }

    // 标记加载器实现类型（生成器装配默认加载器，可选）。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class LuminUILoaderAttribute : Attribute { }

    // 标记一个 UI 元素字段，生成器据 Path 在 root 下查找并赋值。
    // Path 同时是后续 Unity 编辑器生成工具的钩子（右键预制体 → 生成字段声明）。
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class UiElementAttribute : Attribute
    {
        public string? Path { get; set; }
        public UiElementAttribute() { }
        public UiElementAttribute(string path) => Path = path;
    }

    // 字段类型自带的直连事件名（如 Button → onClick），供生成器识别可直接 += 的事件。
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UiClickEventAttribute : Attribute
    {
        public string EventName { get; }
        public UiClickEventAttribute(string eventName) => EventName = eventName;
    }

    // 方法事件标记，Target 为 [UiElement] 字段名。
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnClickAttribute : Attribute { public string Target { get; } public OnClickAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnValueChangedAttribute : Attribute { public string Target { get; } public OnValueChangedAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnTextChangedAttribute : Attribute { public string Target { get; } public OnTextChangedAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnSubmitAttribute : Attribute { public string Target { get; } public OnSubmitAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnPointerEnterAttribute : Attribute { public string Target { get; } public OnPointerEnterAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnPointerExitAttribute : Attribute { public string Target { get; } public OnPointerExitAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnPointerDownAttribute : Attribute { public string Target { get; } public OnPointerDownAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnPointerUpAttribute : Attribute { public string Target { get; } public OnPointerUpAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnDragAttribute : Attribute { public string Target { get; } public OnDragAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnBeginDragAttribute : Attribute { public string Target { get; } public OnBeginDragAttribute(string t) => Target = t; }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnEndDragAttribute : Attribute { public string Target { get; } public OnEndDragAttribute(string t) => Target = t; }
}

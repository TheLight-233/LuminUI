using System;

namespace LuminUIGenerator.Models
{
    // ── View（[View] 组件 / [Screen] 屏）的字段/事件绑定信息（仅用于代码生成）─────
    internal sealed class ViewInfo : IEquatable<ViewInfo>
    {
        public string  Namespace { get; set; } = "";
        public string  ClassName { get; set; } = "";

        public FieldInfo[]    Fields { get; set; } = Array.Empty<FieldInfo>();
        public EventBinding[] Events { get; set; } = Array.Empty<EventBinding>();

        public bool Equals(ViewInfo? o)
        {
            if (o == null) return false;
            if (Namespace != o.Namespace || ClassName != o.ClassName) return false;
            if (Fields.Length != o.Fields.Length || Events.Length != o.Events.Length) return false;
            for (int i = 0; i < Fields.Length; i++) if (!Fields[i].Equals(o.Fields[i])) return false;
            for (int i = 0; i < Events.Length; i++) if (!Events[i].Equals(o.Events[i])) return false;
            return true;
        }
        public override bool Equals(object? obj) => Equals(obj as ViewInfo);
        public override int GetHashCode()
        {
            int h = HC.Combine(Namespace, ClassName);
            foreach (var f in Fields) h = HC.Mix(h, f.GetHashCode());
            foreach (var e in Events) h = HC.Mix(h, e.GetHashCode());
            return h;
        }
    }

    internal sealed class FieldInfo : IEquatable<FieldInfo>
    {
        public string  FieldName      { get; set; } = "";
        public string  TypeFullName   { get; set; } = "";
        public string? ExplicitPath   { get; set; }
        public string? ClickEventName { get; set; }

        public bool Equals(FieldInfo? o) =>
            o != null && FieldName == o.FieldName && TypeFullName == o.TypeFullName
            && ExplicitPath == o.ExplicitPath && ClickEventName == o.ClickEventName;
        public override bool Equals(object? obj) => Equals(obj as FieldInfo);
        public override int GetHashCode() =>
            HC.Combine(FieldName, TypeFullName, ExplicitPath, ClickEventName);
    }

    internal sealed class EventBinding : IEquatable<EventBinding>
    {
        public string  FieldName        { get; set; } = "";
        public string  TypeFullName     { get; set; } = "";
        public string  MethodName       { get; set; } = "";
        public string  EventKind        { get; set; } = "";
        public string? DirectEventName  { get; set; }
        public int     MethodParamCount { get; set; }

        public bool Equals(EventBinding? o) =>
            o != null && FieldName == o.FieldName && MethodName == o.MethodName
            && EventKind == o.EventKind && DirectEventName == o.DirectEventName
            && MethodParamCount == o.MethodParamCount;
        public override bool Equals(object? obj) => Equals(obj as EventBinding);
        public override int GetHashCode() =>
            HC.Combine(FieldName, MethodName, EventKind, DirectEventName);
    }

    // ── HashCode 辅助（兼容 netstandard2.0）─────────────────────────────────
    internal static class HC
    {
        internal static int Mix(int h, int v) { unchecked { return h * 397 ^ v; } }

        internal static int Combine(params object?[] values)
        {
            unchecked
            {
                int h = 17;
                foreach (var v in values) h = h * 31 + (v != null ? v.GetHashCode() : 0);
                return h;
            }
        }
    }
}

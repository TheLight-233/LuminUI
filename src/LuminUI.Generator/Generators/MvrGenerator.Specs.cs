using Microsoft.CodeAnalysis;

namespace LuminUIGenerator.Generators
{
    internal sealed partial class MvrGenerator
    {
        private enum ReactiveKind { Property, Collection, Dictionary }

        private sealed class ModelSpec
        {
            public ModelSpec(string ns, string className, string fullModelType, string reactiveName,
                string accessibility, ReactiveMemberSpec[] members, ActionSpec[] actions)
            {
                Namespace = ns;
                ClassName = className;
                FullModelType = fullModelType;
                ReactiveName = reactiveName;
                Accessibility = accessibility;
                Members = members;
                Actions = actions;
            }

            public string Namespace { get; }
            public string ClassName { get; }
            public string FullModelType { get; }
            public string ReactiveName { get; }
            public string Accessibility { get; }
            public ReactiveMemberSpec[] Members { get; }
            public ActionSpec[] Actions { get; }
        }

        private sealed class ReactiveMemberSpec
        {
            public ReactiveMemberSpec(string name, ReactiveKind kind, string type1, string? type2)
            { Name = name; Kind = kind; Type1 = type1; Type2 = type2; }
            public string Name { get; }
            public ReactiveKind Kind { get; }
            public string Type1 { get; }
            public string? Type2 { get; }
        }

        private sealed class ParameterSpec
        {
            public ParameterSpec(string name, string typeName, RefKind refKind, bool isParams)
            { Name = name; TypeName = typeName; RefKind = refKind; IsParams = isParams; }
            public string Name { get; }
            public string TypeName { get; }
            public RefKind RefKind { get; }
            public bool IsParams { get; }
        }

        private sealed class ActionSpec
        {
            public ActionSpec(string name, string returnType, ParameterSpec[] parameters)
            { Name = name; ReturnType = returnType; Parameters = parameters; }
            public string Name { get; }
            public string ReturnType { get; }
            public ParameterSpec[] Parameters { get; }
        }

        private sealed class ViewSpec
        {
            public ViewSpec(string ns, string className, string accessibility, string? modelType,
                string? reactiveType, ObserverSpec[] observers, WidgetSpec[] widgets, ListSpec[] lists)
            { Namespace = ns; ClassName = className; Accessibility = accessibility; ModelType = modelType; ReactiveType = reactiveType; Observers = observers; Widgets = widgets; Lists = lists; }
            public string Namespace { get; }
            public string ClassName { get; }
            public string Accessibility { get; }
            public string? ModelType { get; }
            public string? ReactiveType { get; }
            public ObserverSpec[] Observers { get; }
            public WidgetSpec[] Widgets { get; }
            public ListSpec[] Lists { get; }
        }

        private sealed class ObserveSourceSpec
        {
            public ObserveSourceSpec(string name, string valueType) { Name = name; ValueType = valueType; }
            public string Name { get; }
            public string ValueType { get; }
        }

        private sealed class ObserverSpec
        {
            public ObserverSpec(string methodName, int parameterCount, ObserveSourceSpec[] sources, int ordinal)
            { MethodName = methodName; ParameterCount = parameterCount; Sources = sources; Ordinal = ordinal; }
            public string MethodName { get; }
            public int ParameterCount { get; }
            public ObserveSourceSpec[] Sources { get; }
            public int Ordinal { get; }
        }

        private sealed class WidgetSpec
        {
            public WidgetSpec(string fieldName, string typeName, string path)
            { FieldName = fieldName; TypeName = typeName; Path = path; }
            public string FieldName { get; }
            public string TypeName { get; }
            public string Path { get; }
        }

        private sealed class ListSpec
        {
            public ListSpec(string methodName, string source, string widgetType, string itemType,
                string containerPath, string templatePath, int maxIdle, int ordinal)
            { MethodName = methodName; Source = source; WidgetType = widgetType; ItemType = itemType; ContainerPath = containerPath; TemplatePath = templatePath; MaxIdle = maxIdle; Ordinal = ordinal; }
            public string MethodName { get; }
            public string Source { get; }
            public string WidgetType { get; }
            public string ItemType { get; }
            public string ContainerPath { get; }
            public string TemplatePath { get; }
            public int MaxIdle { get; }
            public int Ordinal { get; }
        }

        private sealed class ScreenSpec
        {
            public ScreenSpec(string ns, string className, string fullType, string accessibility,
                string? modelType, string? reactiveType, string resourceName, string layer, string mode,
                int poolSize, bool modal, bool closeOnClickMask, float maskOpacity, bool hideWhenCovered,
                float x, float y, float width, float height)
            { Namespace = ns; ClassName = className; FullType = fullType; Accessibility = accessibility; ModelType = modelType; ReactiveType = reactiveType; ResourceName = resourceName; Layer = layer; Mode = mode; PoolSize = poolSize; Modal = modal; CloseOnClickMask = closeOnClickMask; MaskOpacity = maskOpacity; HideWhenCovered = hideWhenCovered; X = x; Y = y; Width = width; Height = height; }
            public string Namespace { get; }
            public string ClassName { get; }
            public string FullType { get; }
            public string Accessibility { get; }
            public string? ModelType { get; }
            public string? ReactiveType { get; }
            public string ResourceName { get; }
            public string Layer { get; }
            public string Mode { get; }
            public int PoolSize { get; }
            public bool Modal { get; }
            public bool CloseOnClickMask { get; }
            public float MaskOpacity { get; }
            public bool HideWhenCovered { get; }
            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }
        }

        private sealed class AdapterSpec
        {
            public AdapterSpec(string fullType, bool bridge) { FullType = fullType; Bridge = bridge; }
            public string FullType { get; }
            public bool Bridge { get; }
        }
    }
}

namespace LuminUIGenerator.Generators
{
    internal sealed partial class MvrGenerator
    {
        private enum ReactiveKind { Property, Collection, Dictionary }

        private sealed class ModelFieldSpec
        {
            public ModelFieldSpec(string fieldName, string propertyName, ReactiveKind kind,
                string type1, string? type2)
            {
                FieldName = fieldName;
                PropertyName = propertyName;
                Kind = kind;
                Type1 = type1;
                Type2 = type2;
            }

            public string FieldName { get; }
            public string PropertyName { get; }
            public ReactiveKind Kind { get; }
            public string Type1 { get; }
            public string? Type2 { get; }
        }

        private sealed class ModelSpec
        {
            public ModelSpec(string ns, string className, ModelFieldSpec[] fields)
            {
                Namespace = ns;
                ClassName = className;
                Fields = fields;
            }

            public string Namespace { get; }
            public string ClassName { get; }
            public ModelFieldSpec[] Fields { get; }
        }

        private sealed class WidgetSpec
        {
            public WidgetSpec(string fieldName, string typeName, string path)
            {
                FieldName = fieldName;
                TypeName = typeName;
                Path = path;
            }

            public string FieldName { get; }
            public string TypeName { get; }
            public string Path { get; }
        }

        private sealed class ViewSpec
        {
            public ViewSpec(string ns, string className, WidgetSpec[] widgets)
            {
                Namespace = ns;
                ClassName = className;
                Widgets = widgets;
            }

            public string Namespace { get; }
            public string ClassName { get; }
            public WidgetSpec[] Widgets { get; }
        }

        private sealed class ScreenSpec
        {
            public ScreenSpec(string ns, string className, string fullType, string resourceName,
                string layer, string mode, int poolSize, bool modal, bool closeOnClickMask,
                float maskOpacity, bool hideWhenCovered, float x, float y, float width, float height)
            {
                Namespace = ns;
                ClassName = className;
                FullType = fullType;
                ResourceName = resourceName;
                Layer = layer;
                Mode = mode;
                PoolSize = poolSize;
                Modal = modal;
                CloseOnClickMask = closeOnClickMask;
                MaskOpacity = maskOpacity;
                HideWhenCovered = hideWhenCovered;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public string Namespace { get; }
            public string ClassName { get; }
            public string FullType { get; }
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
            public AdapterSpec(string fullType, bool bridge)
            {
                FullType = fullType;
                Bridge = bridge;
            }

            public string FullType { get; }
            public bool Bridge { get; }
        }
    }
}

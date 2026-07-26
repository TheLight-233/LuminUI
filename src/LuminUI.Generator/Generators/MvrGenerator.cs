using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminUIGenerator.Generators
{
    /// <summary>
    /// Generates read-only projections for [LuminModel], structural Widget mounting,
    /// zero-argument Screen opening, and static runtime registration.
    /// </summary>
    [Generator]
    internal sealed partial class MvrGenerator : IIncrementalGenerator
    {
        private static readonly SymbolDisplayFormat NullableTypeFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        private const string ModelAttr = "LuminUI.Attributes.LuminModelAttribute";
        private const string ViewAttr = "LuminUI.Attributes.ViewAttribute";
        private const string ScreenAttr = "LuminUI.Attributes.ScreenAttribute";
        private const string WidgetAttr = "LuminUI.Attributes.WidgetAttribute";
        private const string BridgeAttr = "LuminUI.Attributes.LuminUIBridgeAttribute";
        private const string LoaderAttr = "LuminUI.Attributes.LuminUILoaderAttribute";

        private static readonly DiagnosticDescriptor ModelMustBePartial = new DiagnosticDescriptor(
            "LUIN100", "LuminModel must be partial",
            "Class '{0}' marked [LuminModel] must be declared partial",
            "LuminUI.Model", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor ModelFieldMustBePrivate = new DiagnosticDescriptor(
            "LUIN101", "LuminModel fields must be private",
            "Field '{0}' in [LuminModel] class '{1}' must be private",
            "LuminUI.Model", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor UnsupportedModelShape = new DiagnosticDescriptor(
            "LUIN102", "Unsupported LuminModel declaration",
            "[LuminModel] class '{0}' must be a non-generic top-level class",
            "LuminUI.Model", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor ProjectionNameConflict = new DiagnosticDescriptor(
            "LUIN103", "Generated model property name conflicts",
            "Reactive field '{0}' in model '{1}' would generate property '{2}', which already exists",
            "LuminUI.Model", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidAdapter = new DiagnosticDescriptor(
            "LUIN107", "Invalid UI adapter",
            "'{0}' must be concrete, implement '{1}', and provide a public parameterless constructor",
            "LuminUI.Runtime", DiagnosticSeverity.Error, true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var modelResults = context.SyntaxProvider.ForAttributeWithMetadataName(
                ModelAttr,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => ExtractModel(ctx, ct));

            context.RegisterSourceOutput(modelResults, static (spc, result) =>
            {
                foreach (var diagnostic in result.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);
                if (result.Spec != null)
                    spc.AddSource(SafeName(result.Spec.Namespace, result.Spec.ClassName + ".Model"),
                        EmitModel(result.Spec));
            });

            var views = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ViewAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractView(ctx, ct, false))
                .Where(static spec => spec != null)
                .Select(static (spec, _) => spec!);

            var screenViews = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ScreenAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractView(ctx, ct, true))
                .Where(static spec => spec != null)
                .Select(static (spec, _) => spec!);

            context.RegisterSourceOutput(views, static (spc, spec) =>
                spc.AddSource(SafeName(spec.Namespace, spec.ClassName + ".Widgets"), EmitView(spec)));
            context.RegisterSourceOutput(screenViews, static (spc, spec) =>
                spc.AddSource(SafeName(spec.Namespace, spec.ClassName + ".Widgets"), EmitView(spec)));

            var screens = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ScreenAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractScreen(ctx, ct))
                .Where(static spec => spec != null)
                .Select(static (spec, _) => spec!);

            context.RegisterSourceOutput(screens, static (spc, screen) =>
                spc.AddSource(SafeName(screen.Namespace, screen.ClassName + ".Open"), EmitTypedOpen(screen)));

            var bridges = context.SyntaxProvider.ForAttributeWithMetadataName(
                BridgeAttr,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => ExtractAdapter(ctx, ct, true));
            var loaders = context.SyntaxProvider.ForAttributeWithMetadataName(
                LoaderAttr,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => ExtractAdapter(ctx, ct, false));

            context.RegisterSourceOutput(bridges, static (spc, result) =>
            {
                if (result.Diagnostic != null) spc.ReportDiagnostic(result.Diagnostic);
            });
            context.RegisterSourceOutput(loaders, static (spc, result) =>
            {
                if (result.Diagnostic != null) spc.ReportDiagnostic(result.Diagnostic);
            });

            var validBridges = bridges.Where(static result => result.Spec != null)
                .Select(static (result, _) => result.Spec!);
            var validLoaders = loaders.Where(static result => result.Spec != null)
                .Select(static (result, _) => result.Spec!);
            var runtime = screens.Collect().Combine(validBridges.Collect()).Combine(validLoaders.Collect());

            context.RegisterSourceOutput(runtime, static (spc, input) =>
            {
                var ((allScreens, allBridges), allLoaders) = input;
                if (allScreens.Length != 0 || allBridges.Length != 0 || allLoaders.Length != 0)
                    spc.AddSource("LuminUIRuntime.g.cs", EmitRuntime(allScreens, allBridges, allLoaders));
            });
        }

        private static ModelResult ExtractModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var type = (INamedTypeSymbol)ctx.TargetSymbol;
            var syntax = (ClassDeclarationSyntax)ctx.TargetNode;
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            bool validShape = !type.IsGenericType && type.ContainingType == null;
            if (!validShape)
                diagnostics.Add(Diagnostic.Create(UnsupportedModelShape,
                    syntax.Identifier.GetLocation(), type.Name));

            bool partial = syntax.Modifiers.Any(SyntaxKind.PartialKeyword);
            if (!partial)
                diagnostics.Add(Diagnostic.Create(ModelMustBePartial,
                    syntax.Identifier.GetLocation(), type.Name));

            var fields = new List<ModelFieldSpec>();
            var generatedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in type.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is not IFieldSymbol field || field.IsImplicitlyDeclared) continue;

                if (field.DeclaredAccessibility != Accessibility.Private)
                    diagnostics.Add(Diagnostic.Create(ModelFieldMustBePrivate,
                        field.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                        field.Name, type.Name));

                if (field.IsStatic || field.DeclaredAccessibility != Accessibility.Private) continue;
                if (!TryGetReactiveField(field, out var kind, out var type1, out var type2)) continue;

                string propertyName = FieldToProperty(field.Name);
                bool conflict = propertyName.Length == 0
                    || type.GetMembers(propertyName).Length != 0
                    || !generatedNames.Add(propertyName);
                if (conflict)
                {
                    diagnostics.Add(Diagnostic.Create(ProjectionNameConflict,
                        field.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                        field.Name, type.Name, propertyName));
                    continue;
                }

                fields.Add(new ModelFieldSpec(field.Name, propertyName, kind, type1, type2));
            }

            ModelSpec? spec = validShape && partial
                ? new ModelSpec(NamespaceOf(type), type.Name, fields.ToArray())
                : null;
            return new ModelResult(spec, diagnostics.ToImmutable());
        }

        private static bool TryGetReactiveField(IFieldSymbol field, out ReactiveKind kind,
            out string type1, out string? type2)
        {
            kind = default;
            type1 = string.Empty;
            type2 = null;
            if (field.Type is not INamedTypeSymbol named) return false;

            string generic = named.ConstructedFrom.ToDisplayString();
            if (generic == "LuminUI.ReactiveProperty<T>" && named.TypeArguments.Length == 1)
            {
                kind = ReactiveKind.Property;
                type1 = TypeName(named.TypeArguments[0]);
                return true;
            }
            if (generic == "LuminUI.ReactiveCollection<T>" && named.TypeArguments.Length == 1)
            {
                kind = ReactiveKind.Collection;
                type1 = TypeName(named.TypeArguments[0]);
                return true;
            }
            if (generic == "LuminUI.ReactiveDictionary<TKey, TValue>" && named.TypeArguments.Length == 2)
            {
                kind = ReactiveKind.Dictionary;
                type1 = TypeName(named.TypeArguments[0]);
                type2 = TypeName(named.TypeArguments[1]);
                return true;
            }
            return false;
        }

        private static ViewSpec? ExtractView(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool fromScreen)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
            if (fromScreen && FindAttribute(type, ViewAttr) != null) return null;

            var widgets = new List<WidgetSpec>();
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field) continue;
                var attribute = FindAttribute(field, WidgetAttr);
                string? path = attribute == null ? null : GetStringCtor(attribute, 0);
                if (path != null)
                    widgets.Add(new WidgetSpec(field.Name, TypeName(field.Type), Escape(path)));
            }

            return widgets.Count == 0
                ? null
                : new ViewSpec(NamespaceOf(type), type.Name, widgets.ToArray());
        }

        private static ScreenSpec? ExtractScreen(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
            var attribute = FindAttribute(type, ScreenAttr);
            if (attribute == null) return null;

            return new ScreenSpec(
                NamespaceOf(type), type.Name, TypeName(type),
                GetNamedString(attribute, "Name") ?? type.Name,
                LayerName(GetNamedInt(attribute, "Layer", 100)),
                ModeName(GetNamedInt(attribute, "Mode", 0)),
                GetNamedInt(attribute, "PoolSize", 1),
                GetNamedBool(attribute, "Modal", false),
                GetNamedBool(attribute, "CloseOnClickMask", false),
                GetNamedFloat(attribute, "MaskOpacity", 0.5f),
                GetNamedBool(attribute, "HideWhenCovered", true),
                GetNamedFloat(attribute, "X", 0f),
                GetNamedFloat(attribute, "Y", 0f),
                GetNamedFloat(attribute, "Width", 0f),
                GetNamedFloat(attribute, "Height", 0f));
        }

        private static AdapterResult ExtractAdapter(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool bridge)
        {
            ct.ThrowIfCancellationRequested();
            var type = (INamedTypeSymbol)ctx.TargetSymbol;
            string contract = bridge ? "LuminUI.Interface.IUiBridge" : "LuminUI.Interface.IUiLoader";
            bool implements = type.AllInterfaces.Any(i => i.ToDisplayString() == contract);
            bool hasCtor = type.InstanceConstructors.Any(c => c.Parameters.Length == 0
                && c.DeclaredAccessibility == Accessibility.Public);
            if (type.IsAbstract || !implements || !hasCtor)
            {
                return new AdapterResult(null, Diagnostic.Create(InvalidAdapter,
                    type.Locations.FirstOrDefault(), type.Name, contract));
            }
            return new AdapterResult(new AdapterSpec(TypeName(type), bridge), null);
        }

        private static string EmitModel(ModelSpec model)
        {
            var sb = Header(model.Namespace);
            sb.AppendLine("partial class " + model.ClassName);
            sb.AppendLine("{");
            foreach (var field in model.Fields)
            {
                switch (field.Kind)
                {
                    case ReactiveKind.Property:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveProperty<" + field.Type1
                            + "> " + field.PropertyName + " => " + field.FieldName + ";");
                        break;
                    case ReactiveKind.Collection:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveCollection<" + field.Type1
                            + "> " + field.PropertyName + " => " + field.FieldName + ";");
                        break;
                    case ReactiveKind.Dictionary:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveDictionary<" + field.Type1
                            + ", " + field.Type2 + "> " + field.PropertyName + " => " + field.FieldName + ";");
                        break;
                }
            }
            sb.AppendLine("}");
            Footer(sb, model.Namespace);
            return sb.ToString();
        }

        private static string EmitView(ViewSpec view)
        {
            var sb = Header(view.Namespace);
            sb.AppendLine("partial class " + view.ClassName);
            sb.AppendLine("{");
            sb.AppendLine("    public override void __BuildWidgets()");
            sb.AppendLine("    {");
            foreach (var widget in view.Widgets)
            {
                sb.AppendLine("        " + widget.FieldName + " ??= new " + widget.TypeName + "();");
                sb.AppendLine("        AddWidget(" + widget.FieldName + ", \"" + widget.Path + "\");");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            Footer(sb, view.Namespace);
            return sb.ToString();
        }

        private static string EmitTypedOpen(ScreenSpec screen)
        {
            var sb = Header(screen.Namespace);
            sb.AppendLine("partial class " + screen.ClassName);
            sb.AppendLine("{");
            sb.AppendLine("    public static global::LuminThread.LuminTask<global::LuminUI.ScreenHandle<"
                + screen.FullType + ">> OpenAsync(global::System.Threading.CancellationToken ct = default)");
            sb.AppendLine("        => global::LuminUI.LuminUi.OpenAsync<" + screen.FullType + ">(ct);");
            sb.AppendLine("}");
            Footer(sb, screen.Namespace);
            return sb.ToString();
        }

        private static string EmitRuntime(
            ImmutableArray<ScreenSpec> screens,
            ImmutableArray<AdapterSpec> bridges,
            ImmutableArray<AdapterSpec> loaders)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("public static class LuminUIRuntime");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll()");
            sb.AppendLine("    {");
            foreach (var bridge in bridges)
                sb.AppendLine("        global::LuminUI.UiBridgeRegistry.SetBridge(new " + bridge.FullType + "());");
            foreach (var loader in loaders)
                sb.AppendLine("        global::LuminUI.LuminUi.SetLoader(new " + loader.FullType + "());");
            foreach (var screen in screens)
            {
                sb.AppendLine("        global::LuminUI.LuminUi.RegisterScreen<" + screen.FullType + ">(");
                sb.AppendLine("            new global::LuminUI.ScreenOptions");
                sb.AppendLine("            {");
                sb.AppendLine("                ResourceName = \"" + Escape(screen.ResourceName) + "\",");
                sb.AppendLine("                Layer = global::LuminUI.UILayer." + screen.Layer + ",");
                sb.AppendLine("                Mode = global::LuminUI.UIMode." + screen.Mode + ",");
                sb.AppendLine("                PoolSize = " + screen.PoolSize + ",");
                sb.AppendLine("                Modal = " + Bool(screen.Modal) + ",");
                sb.AppendLine("                CloseOnClickMask = " + Bool(screen.CloseOnClickMask) + ",");
                sb.AppendLine("                MaskOpacity = " + Float(screen.MaskOpacity) + ",");
                sb.AppendLine("                HideWhenCovered = " + Bool(screen.HideWhenCovered) + ",");
                sb.AppendLine("                X = " + Float(screen.X) + ", Y = " + Float(screen.Y) + ",");
                sb.AppendLine("                Width = " + Float(screen.Width) + ", Height = " + Float(screen.Height));
                sb.AppendLine("            },");
                sb.AppendLine("            () => new " + screen.FullType + "());");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string FieldToProperty(string fieldName)
        {
            string name = fieldName.TrimStart('_');
            if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
            if (name.Length == 0) return string.Empty;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static string LayerName(int value)
        {
            switch (value)
            {
                case 0: return "Background";
                case 100: return "Scene";
                case 200: return "HUD";
                case 300: return "Popup";
                case 400: return "Loading";
                case 500: return "Toast";
                default: return "Scene";
            }
        }

        private static string ModeName(int value)
        {
            switch (value)
            {
                case 1: return "Stack";
                case 2: return "Overlay";
                default: return "Normal";
            }
        }

        private static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
            => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == metadataName);

        private static string NamespaceOf(INamedTypeSymbol type)
            => type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();

        private static string TypeName(ITypeSymbol type)
            => type.ToDisplayString(NullableTypeFormat);

        private static string? GetStringCtor(AttributeData attr, int index)
            => attr.ConstructorArguments.Length > index
               && attr.ConstructorArguments[index].Value is string value ? value : null;

        private static string? GetNamedString(AttributeData attr, string name)
        {
            foreach (var pair in attr.NamedArguments)
                if (pair.Key == name) return pair.Value.Value as string;
            return null;
        }

        private static int GetNamedInt(AttributeData attr, string name, int fallback)
        {
            foreach (var pair in attr.NamedArguments)
                if (pair.Key == name && pair.Value.Value != null)
                    return Convert.ToInt32(pair.Value.Value, CultureInfo.InvariantCulture);
            return fallback;
        }

        private static bool GetNamedBool(AttributeData attr, string name, bool fallback)
        {
            foreach (var pair in attr.NamedArguments)
                if (pair.Key == name && pair.Value.Value is bool value) return value;
            return fallback;
        }

        private static float GetNamedFloat(AttributeData attr, string name, float fallback)
        {
            foreach (var pair in attr.NamedArguments)
                if (pair.Key == name && pair.Value.Value != null)
                    return Convert.ToSingle(pair.Value.Value, CultureInfo.InvariantCulture);
            return fallback;
        }

        private static StringBuilder Header(string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine("namespace " + ns);
                sb.AppendLine("{");
            }
            return sb;
        }

        private static void Footer(StringBuilder sb, string ns)
        {
            if (!string.IsNullOrEmpty(ns)) sb.AppendLine("}");
        }

        private static string SafeName(string ns, string name)
            => (string.IsNullOrEmpty(ns) ? name : ns + "." + name).Replace('.', '_') + ".g.cs";

        private static string Escape(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Float(float value)
            => value.ToString("0.0######", CultureInfo.InvariantCulture) + "f";

        private readonly struct ModelResult
        {
            public ModelResult(ModelSpec? spec, ImmutableArray<Diagnostic> diagnostics)
            {
                Spec = spec;
                Diagnostics = diagnostics;
            }

            public ModelSpec? Spec { get; }
            public ImmutableArray<Diagnostic> Diagnostics { get; }
        }

        private readonly struct AdapterResult
        {
            public AdapterResult(AdapterSpec? spec, Diagnostic? diagnostic)
            {
                Spec = spec;
                Diagnostic = diagnostic;
            }

            public AdapterSpec? Spec { get; }
            public Diagnostic? Diagnostic { get; }
        }
    }
}

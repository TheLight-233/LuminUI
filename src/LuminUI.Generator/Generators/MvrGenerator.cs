using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminUIGenerator.Generators
{
    /// <summary>生成 MVR Reactive 投影、View 响应绑定及屏幕注册。</summary>
    [Generator]
    internal sealed partial class MvrGenerator : IIncrementalGenerator
    {
        private const string ModelAttr = "LuminUI.Attributes.LuminModelAttribute";
        private const string ActionAttr = "LuminUI.Attributes.LuminActionAttribute";
        private const string ViewAttr = "LuminUI.Attributes.ViewAttribute";
        private const string ScreenAttr = "LuminUI.Attributes.ScreenAttribute";
        private const string ObserveAttr = "LuminUI.Attributes.ObserveAttribute";
        private const string WidgetAttr = "LuminUI.Attributes.UiWidgetAttribute";
        private const string BindListAttr = "LuminUI.Attributes.BindListAttribute";
        private const string BridgeAttr = "LuminUI.Attributes.LuminUIBridgeAttribute";
        private const string LoaderAttr = "LuminUI.Attributes.LuminUILoaderAttribute";

        private static readonly DiagnosticDescriptor MissingModel = new DiagnosticDescriptor(
            "LUIN100", "MVR view has no model",
            "'{0}' uses [{1}] but its [View]/[Screen] does not declare a [LuminModel] type",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidObserveSource = new DiagnosticDescriptor(
            "LUIN101", "Invalid observe source",
            "'{0}' is not a public ReactiveProperty<T> member of model '{1}'",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidObserveSignature = new DiagnosticDescriptor(
            "LUIN102", "Invalid observe method signature",
            "Observe method '{0}' must have zero parameters or one matching parameter for every source",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidListBinding = new DiagnosticDescriptor(
            "LUIN103", "Invalid reactive list binding",
            "BindList method '{0}' must target a ReactiveCollection<T> and have signature (TWidget, T, int)",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidAction = new DiagnosticDescriptor(
            "LUIN104", "Invalid reactive action",
            "LuminAction method '{0}' must be an accessible, non-static, non-generic method",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidModelLink = new DiagnosticDescriptor(
            "LUIN105", "Invalid MVR model link",
            "Model type '{0}' used by '{1}' must be marked [LuminModel]",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor UnsupportedTypeShape = new DiagnosticDescriptor(
            "LUIN106", "Unsupported generated type shape",
            "'{0}' must be a non-abstract, non-generic top-level class for LuminUI generation",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidAdapter = new DiagnosticDescriptor(
            "LUIN107", "Invalid UI adapter",
            "'{0}' must be concrete, implement '{1}', and provide a public parameterless constructor",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor ReactiveContextMismatch = new DiagnosticDescriptor(
            "LUIN108", "Widget reactive context mismatch",
            "Widget '{0}' uses model '{1}', but parent view '{2}' uses model '{3}'",
            "LuminUI.MVR", DiagnosticSeverity.Error, true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var models = context.SyntaxProvider.ForAttributeWithMetadataName(
                ModelAttr,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => ExtractModel(ctx, ct));

            context.RegisterSourceOutput(models, static (spc, model) =>
                spc.AddSource(SafeName(model.Namespace, model.ReactiveName), EmitReactive(model)));

            var views = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ViewAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractView(ctx, ct, false))
                .Where(static view => view != null)
                .Select(static (view, _) => view!);

            var screenViews = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ScreenAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractView(ctx, ct, true))
                .Where(static view => view != null)
                .Select(static (view, _) => view!);

            context.RegisterSourceOutput(views, static (spc, view) =>
            {
                if (view.ModelType != null || view.Observers.Length != 0
                    || view.Widgets.Length != 0 || view.Lists.Length != 0)
                    spc.AddSource(SafeName(view.Namespace, view.ClassName + ".Mvr"), EmitView(view));
            });

            context.RegisterSourceOutput(screenViews, static (spc, view) =>
            {
                if (view.ModelType != null || view.Observers.Length != 0
                    || view.Widgets.Length != 0 || view.Lists.Length != 0)
                    spc.AddSource(SafeName(view.Namespace, view.ClassName + ".Mvr"), EmitView(view));
            });

            var screens = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ScreenAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ExtractScreen(ctx, ct))
                .Where(static screen => screen != null)
                .Select(static (screen, _) => screen!);

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

            var runtime = screens.Collect().Combine(bridges.Collect()).Combine(loaders.Collect());
            context.RegisterSourceOutput(runtime, static (spc, input) =>
            {
                var ((allScreens, allBridges), allLoaders) = input;
                if (allScreens.Length > 0 || allBridges.Length > 0 || allLoaders.Length > 0)
                    spc.AddSource("LuminUIRuntime.g.cs", EmitRuntime(allScreens, allBridges, allLoaders));
            });

            var observeDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ObserveAttr,
                    static (node, _) => node is MethodDeclarationSyntax,
                    static (ctx, ct) => ValidateObserve(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(observeDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var listDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    BindListAttr,
                    static (node, _) => node is MethodDeclarationSyntax,
                    static (ctx, ct) => ValidateList(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(listDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var actionDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ActionAttr,
                    static (node, _) => node is MethodDeclarationSyntax,
                    static (ctx, ct) => ValidateAction(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(actionDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var modelDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ModelAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ValidateGeneratedType(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(modelDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var viewModelDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ViewAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ValidateModelLink(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(viewModelDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var screenModelDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    ScreenAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ValidateModelLink(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(screenModelDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var widgetDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    WidgetAttr,
                    static (node, _) => node is VariableDeclaratorSyntax,
                    static (ctx, ct) => ValidateWidget(ctx, ct))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(widgetDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var bridgeDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    BridgeAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ValidateAdapter(ctx, ct, true))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(bridgeDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

            var loaderDiagnostics = context.SyntaxProvider.ForAttributeWithMetadataName(
                    LoaderAttr,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, ct) => ValidateAdapter(ctx, ct, false))
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!);
            context.RegisterSourceOutput(loaderDiagnostics,
                static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));
        }

        private static Diagnostic? ValidateGeneratedType(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var type = (INamedTypeSymbol)ctx.TargetSymbol;
            bool valid = !type.IsAbstract && !type.IsGenericType && type.ContainingType == null;
            return valid ? null : Diagnostic.Create(UnsupportedTypeShape,
                type.Locations.FirstOrDefault(), type.Name);
        }

        private static Diagnostic? ValidateModelLink(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var view = (INamedTypeSymbol)ctx.TargetSymbol;
            if (view.IsGenericType || view.ContainingType != null)
                return Diagnostic.Create(UnsupportedTypeShape,
                    view.Locations.FirstOrDefault(), view.Name);
            var model = GetModelType(ctx.Attributes.FirstOrDefault());
            if (model == null || FindAttribute(model, ModelAttr) != null) return null;
            return Diagnostic.Create(InvalidModelLink, view.Locations.FirstOrDefault(),
                model.Name, view.Name);
        }

        private static Diagnostic? ValidateWidget(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not IFieldSymbol field
                || field.Type is not INamedTypeSymbol widget) return null;
            var parent = field.ContainingType;
            var parentAttr = FindAttribute(parent, ScreenAttr) ?? FindAttribute(parent, ViewAttr);
            var widgetAttr = FindAttribute(widget, ScreenAttr) ?? FindAttribute(widget, ViewAttr);
            var parentModel = GetModelType(parentAttr);
            var widgetModel = GetModelType(widgetAttr);
            if (widgetModel == null || SymbolEqualityComparer.Default.Equals(parentModel, widgetModel)) return null;
            return Diagnostic.Create(ReactiveContextMismatch, field.Locations.FirstOrDefault(),
                widget.Name, widgetModel.Name, parent.Name, parentModel?.Name ?? "<none>");
        }

        private static Diagnostic? ValidateAdapter(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool bridge)
        {
            ct.ThrowIfCancellationRequested();
            var type = (INamedTypeSymbol)ctx.TargetSymbol;
            string contract = bridge ? "LuminUI.Interface.IUiBridge" : "LuminUI.Interface.IUiLoader";
            bool implements = type.AllInterfaces.Any(i => i.ToDisplayString() == contract);
            bool hasCtor = type.InstanceConstructors.Any(c => c.Parameters.Length == 0
                && c.DeclaredAccessibility == Accessibility.Public);
            bool valid = !type.IsAbstract && implements && hasCtor;
            return valid ? null : Diagnostic.Create(InvalidAdapter,
                type.Locations.FirstOrDefault(), type.Name, contract);
        }

        private static Diagnostic? ValidateObserve(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not IMethodSymbol method) return null;
            var view = method.ContainingType;
            var viewAttr = FindAttribute(view, ScreenAttr) ?? FindAttribute(view, ViewAttr);
            var model = GetModelType(viewAttr);
            if (model == null)
                return Diagnostic.Create(MissingModel, method.Locations.FirstOrDefault(),
                    view.Name, "Observe");

            var observe = FindAttribute(method, ObserveAttr);
            var sources = observe == null ? Array.Empty<string>() : GetStringArrayCtor(observe);
            var valueTypes = new List<ITypeSymbol>();
            foreach (var source in sources)
            {
                var symbol = model.GetMembers(source).FirstOrDefault();
                var type = MemberType(symbol) as INamedTypeSymbol;
                if (type == null || type.TypeArguments.Length != 1
                    || !type.ConstructedFrom.ToDisplayString().StartsWith(
                        "LuminUI.ReactiveProperty<", StringComparison.Ordinal))
                    return Diagnostic.Create(InvalidObserveSource, method.Locations.FirstOrDefault(),
                        source, model.Name);
                valueTypes.Add(type.TypeArguments[0]);
            }

            if (sources.Length == 0 || (method.Parameters.Length != 0
                && method.Parameters.Length != valueTypes.Count))
                return Diagnostic.Create(InvalidObserveSignature, method.Locations.FirstOrDefault(), method.Name);

            if (method.Parameters.Length == valueTypes.Count)
                for (int i = 0; i < valueTypes.Count; i++)
                    if (!SymbolEqualityComparer.Default.Equals(method.Parameters[i].Type, valueTypes[i]))
                        return Diagnostic.Create(InvalidObserveSignature,
                            method.Parameters[i].Locations.FirstOrDefault(), method.Name);
            return null;
        }

        private static Diagnostic? ValidateList(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not IMethodSymbol method) return null;
            var view = method.ContainingType;
            var viewAttr = FindAttribute(view, ScreenAttr) ?? FindAttribute(view, ViewAttr);
            var model = GetModelType(viewAttr);
            if (model == null)
                return Diagnostic.Create(MissingModel, method.Locations.FirstOrDefault(),
                    view.Name, "BindList");

            var attr = FindAttribute(method, BindListAttr);
            var source = attr != null ? GetStringCtor(attr, 0) : null;
            var sourceSymbol = source != null ? model.GetMembers(source).FirstOrDefault() : null;
            var sourceType = MemberType(sourceSymbol) as INamedTypeSymbol;
            bool valid = sourceType != null && sourceType.TypeArguments.Length == 1
                && sourceType.ConstructedFrom.ToDisplayString().StartsWith(
                    "LuminUI.ReactiveCollection<", StringComparison.Ordinal)
                && method.Parameters.Length == 3
                && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, sourceType.TypeArguments[0])
                && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32
                && InheritsLuminView(method.Parameters[0].Type as INamedTypeSymbol);
            if (!valid) return Diagnostic.Create(InvalidListBinding,
                method.Locations.FirstOrDefault(), method.Name);

            var cell = (INamedTypeSymbol)method.Parameters[0].Type;
            var cellAttr = FindAttribute(cell, ScreenAttr) ?? FindAttribute(cell, ViewAttr);
            var cellModel = GetModelType(cellAttr);
            if (cellModel != null && !SymbolEqualityComparer.Default.Equals(model, cellModel))
                return Diagnostic.Create(ReactiveContextMismatch,
                    method.Parameters[0].Locations.FirstOrDefault(), cell.Name, cellModel.Name,
                    view.Name, model.Name);
            return null;
        }

        private static Diagnostic? ValidateAction(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not IMethodSymbol method) return null;
            bool valid = !method.IsStatic && !method.IsGenericMethod
                && method.DeclaredAccessibility != Accessibility.Private;
            return valid ? null : Diagnostic.Create(InvalidAction,
                method.Locations.FirstOrDefault(), method.Name);
        }

        private static bool InheritsLuminView(INamedTypeSymbol? type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.ConstructedFrom.ToDisplayString().StartsWith(
                    "LuminUI.LuminView", StringComparison.Ordinal)) return true;
            return false;
        }

        private static ModelSpec ExtractModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var type = (INamedTypeSymbol)ctx.TargetSymbol;
            var ns = NamespaceOf(type);
            var members = new List<ReactiveMemberSpec>();
            var actions = new List<ActionSpec>();

            foreach (var member in type.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                ITypeSymbol? memberType = null;
                string? memberName = null;
                if (member is IFieldSymbol field && !field.IsStatic
                    && field.DeclaredAccessibility == Accessibility.Public)
                {
                    memberType = field.Type;
                    memberName = field.Name;
                }
                else if (member is IPropertySymbol property && !property.IsStatic
                         && property.DeclaredAccessibility == Accessibility.Public)
                {
                    memberType = property.Type;
                    memberName = property.Name;
                }

                if (memberType is INamedTypeSymbol named && memberName != null)
                {
                    var generic = named.ConstructedFrom.ToDisplayString();
                    if (generic.StartsWith("LuminUI.ReactiveProperty<", StringComparison.Ordinal)
                        && named.TypeArguments.Length == 1)
                    {
                        members.Add(new ReactiveMemberSpec(memberName, ReactiveKind.Property,
                            TypeName(named.TypeArguments[0]), null));
                    }
                    else if (generic.StartsWith("LuminUI.ReactiveCollection<", StringComparison.Ordinal)
                             && named.TypeArguments.Length == 1)
                    {
                        members.Add(new ReactiveMemberSpec(memberName, ReactiveKind.Collection,
                            TypeName(named.TypeArguments[0]), null));
                    }
                    else if (generic.StartsWith("LuminUI.ReactiveDictionary<", StringComparison.Ordinal)
                             && named.TypeArguments.Length == 2)
                    {
                        members.Add(new ReactiveMemberSpec(memberName, ReactiveKind.Dictionary,
                            TypeName(named.TypeArguments[0]), TypeName(named.TypeArguments[1])));
                    }
                }

                if (member is IMethodSymbol method && !method.IsStatic && !method.IsGenericMethod
                    && FindAttribute(method, ActionAttr) != null
                    && method.DeclaredAccessibility != Accessibility.Private)
                {
                    var parameters = method.Parameters.Select(static p => new ParameterSpec(
                        p.Name, TypeName(p.Type), p.RefKind, p.IsParams)).ToArray();
                    actions.Add(new ActionSpec(method.Name, TypeName(method.ReturnType), parameters));
                }
            }

            var reactiveName = type.Name.EndsWith("Model", StringComparison.Ordinal)
                ? type.Name.Substring(0, type.Name.Length - 5) + "Reactive"
                : type.Name + "Reactive";
            return new ModelSpec(ns, type.Name, TypeName(type), reactiveName,
                AccessibilityText(type.DeclaredAccessibility),
                members.ToArray(), actions.ToArray());
        }

        private static ViewSpec? ExtractView(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool fromScreen)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
            if (fromScreen && FindAttribute(type, ViewAttr) != null) return null;

            var attribute = FindAttribute(type, fromScreen ? ScreenAttr : ViewAttr);
            var model = GetModelType(attribute);
            var observers = new List<ObserverSpec>();
            var widgets = new List<WidgetSpec>();
            var lists = new List<ListSpec>();

            foreach (var member in type.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is IFieldSymbol field)
                {
                    var widget = FindAttribute(field, WidgetAttr);
                    if (widget != null && GetStringCtor(widget, 0) is string path)
                        widgets.Add(new WidgetSpec(field.Name, TypeName(field.Type), Escape(path)));
                    continue;
                }

                if (member is not IMethodSymbol method) continue;
                var observe = FindAttribute(method, ObserveAttr);
                if (observe != null && model != null)
                {
                    var sources = GetStringArrayCtor(observe);
                    var sourceSpecs = new List<ObserveSourceSpec>();
                    foreach (var source in sources)
                    {
                        var sourceMember = model.GetMembers(source).FirstOrDefault();
                        var sourceType = MemberType(sourceMember) as INamedTypeSymbol;
                        if (sourceType == null || sourceType.TypeArguments.Length != 1) continue;
                        var generic = sourceType.ConstructedFrom.ToDisplayString();
                        if (!generic.StartsWith("LuminUI.ReactiveProperty<", StringComparison.Ordinal)) continue;
                        sourceSpecs.Add(new ObserveSourceSpec(source, TypeName(sourceType.TypeArguments[0])));
                    }
                    if (sourceSpecs.Count > 0)
                        observers.Add(new ObserverSpec(method.Name, method.Parameters.Length,
                            sourceSpecs.ToArray(), observers.Count));
                }

                var bindList = FindAttribute(method, BindListAttr);
                if (bindList != null && model != null && method.Parameters.Length == 3)
                {
                    var source = GetStringCtor(bindList, 0);
                    var container = GetStringCtor(bindList, 1);
                    var template = GetStringCtor(bindList, 2);
                    if (source == null || container == null || template == null) continue;
                    var sourceMember = model.GetMembers(source).FirstOrDefault();
                    var sourceType = MemberType(sourceMember) as INamedTypeSymbol;
                    if (sourceType == null || sourceType.TypeArguments.Length != 1
                        || !sourceType.ConstructedFrom.ToDisplayString().StartsWith(
                            "LuminUI.ReactiveCollection<", StringComparison.Ordinal)) continue;
                    int maxIdle = GetNamedInt(bindList, "MaxIdle", 8);
                    lists.Add(new ListSpec(method.Name, source,
                        TypeName(method.Parameters[0].Type), TypeName(sourceType.TypeArguments[0]),
                        Escape(container), Escape(template), maxIdle, lists.Count));
                }
            }

            string? modelType = model != null ? TypeName(model) : null;
            string? reactiveType = model != null ? ReactiveTypeName(model) : null;
            return new ViewSpec(NamespaceOf(type), type.Name, AccessibilityText(type.DeclaredAccessibility),
                modelType, reactiveType, observers.ToArray(), widgets.ToArray(), lists.ToArray());
        }

        private static ScreenSpec? ExtractScreen(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
            var attribute = FindAttribute(type, ScreenAttr);
            if (attribute == null) return null;
            var model = GetModelType(attribute);
            return new ScreenSpec(
                NamespaceOf(type), type.Name, TypeName(type), AccessibilityText(type.DeclaredAccessibility),
                model != null ? TypeName(model) : null,
                model != null ? ReactiveTypeName(model) : null,
                GetNamedString(attribute, "Name") ?? type.Name,
                EnumName(GetNamedInt(attribute, "Layer", 100), true),
                EnumName(GetNamedInt(attribute, "Mode", 0), false),
                GetNamedInt(attribute, "PoolSize", 1),
                GetNamedBool(attribute, "Modal", false),
                GetNamedBool(attribute, "CloseOnClickMask", false),
                GetNamedFloat(attribute, "MaskOpacity", 0.5f),
                GetNamedBool(attribute, "HideWhenCovered", true),
                GetNamedFloat(attribute, "X", 0f), GetNamedFloat(attribute, "Y", 0f),
                GetNamedFloat(attribute, "Width", 0f), GetNamedFloat(attribute, "Height", 0f));
        }

        private static AdapterSpec ExtractAdapter(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool bridge)
        {
            ct.ThrowIfCancellationRequested();
            return new AdapterSpec(TypeName((INamedTypeSymbol)ctx.TargetSymbol), bridge);
        }

        private static string EmitReactive(ModelSpec model)
        {
            var sb = Header(model.Namespace);
            sb.AppendLine(model.Accessibility + " sealed class " + model.ReactiveName + " : global::LuminUI.LuminReactive");
            sb.AppendLine("{");
            sb.AppendLine("    private " + model.FullModelType + "? _model;");
            sb.AppendLine("    private " + model.FullModelType + " Model => _model ?? throw new global::System.InvalidOperationException(\"Reactive context is detached.\");");
            sb.AppendLine();
            foreach (var member in model.Members)
            {
                switch (member.Kind)
                {
                    case ReactiveKind.Property:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveProperty<" + member.Type1 + "> " + member.Name + " => Model." + member.Name + ";");
                        break;
                    case ReactiveKind.Collection:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveCollection<" + member.Type1 + "> " + member.Name + " => Model." + member.Name + ";");
                        break;
                    case ReactiveKind.Dictionary:
                        sb.AppendLine("    public global::LuminUI.IReadOnlyReactiveDictionary<" + member.Type1 + ", " + member.Type2 + "> " + member.Name + " => Model." + member.Name + ";");
                        break;
                }
            }
            if (model.Actions.Length > 0) sb.AppendLine();
            foreach (var action in model.Actions)
            {
                sb.Append("    public ").Append(action.ReturnType).Append(' ').Append(action.Name).Append('(');
                for (int i = 0; i < action.Parameters.Length; i++)
                {
                    if (i != 0) sb.Append(", ");
                    AppendParameter(sb, action.Parameters[i]);
                }
                sb.Append(") => Model.").Append(action.Name).Append('(');
                for (int i = 0; i < action.Parameters.Length; i++)
                {
                    if (i != 0) sb.Append(", ");
                    AppendArgument(sb, action.Parameters[i]);
                }
                sb.AppendLine(");");
            }
            sb.AppendLine();
            sb.AppendLine("    protected override void OnAttach(object model) => _model = (" + model.FullModelType + ")model;");
            sb.AppendLine("    protected override void OnDetach() => _model = null;");
            sb.AppendLine("}");
            Footer(sb, model.Namespace);
            return sb.ToString();
        }

        private static string EmitView(ViewSpec view)
        {
            var sb = Header(view.Namespace);
            sb.AppendLine(view.Accessibility + " partial class " + view.ClassName);
            sb.AppendLine("{");
            if (view.ReactiveType != null)
            {
                sb.AppendLine("    protected " + view.ReactiveType + " Reactive { get; private set; } = null!;");
                sb.AppendLine("    public override bool __RequiresReactive => true;");
                sb.AppendLine("    public override void __SetReactiveObj(global::LuminUI.LuminReactive reactive) => Reactive = (" + view.ReactiveType + ")reactive;");
                sb.AppendLine("    public override void __ClearReactiveObj() => Reactive = null!;");
                sb.AppendLine();
            }

            foreach (var observer in view.Observers)
            {
                for (int i = 0; i < observer.Sources.Length; i++)
                {
                    var source = observer.Sources[i];
                    sb.AppendLine("    private global::System.Action<" + source.ValueType + ">? __obs_" + observer.Ordinal + "_" + i + ";");
                    sb.AppendLine("    private void __on_" + observer.Ordinal + "_" + i + "(" + source.ValueType + " _) => " + ObserverCall(observer) + ";");
                }
            }
            foreach (var list in view.Lists)
            {
                sb.AppendLine("    private global::LuminUI.LuminWidgetList<" + list.WidgetType + ", " + list.ItemType + ">? __list_" + list.Ordinal + ";");
                sb.AppendLine("    private global::System.Action<" + list.WidgetType + ", " + list.ItemType + ", int>? __listBinder_" + list.Ordinal + ";");
                sb.AppendLine("    private static " + list.WidgetType + " __createListWidget_" + list.Ordinal + "() => new " + list.WidgetType + "();");
                sb.AppendLine("    private static readonly global::System.Func<" + list.WidgetType + "> __listFactory_" + list.Ordinal + " = __createListWidget_" + list.Ordinal + ";");
            }

            if (view.Observers.Length != 0 || view.Lists.Length != 0)
            {
                sb.AppendLine();
                sb.AppendLine("    public override void __WireReactive()");
                sb.AppendLine("    {");
                foreach (var observer in view.Observers)
                {
                    for (int i = 0; i < observer.Sources.Length; i++)
                    {
                        var source = observer.Sources[i];
                        sb.AppendLine("        __obs_" + observer.Ordinal + "_" + i + " ??= __on_" + observer.Ordinal + "_" + i + ";");
                        sb.AppendLine("        Reactive." + source.Name + ".SubscribeNoPush(__obs_" + observer.Ordinal + "_" + i + ");");
                    }
                    sb.AppendLine("        " + ObserverCall(observer) + ";");
                }
                foreach (var list in view.Lists)
                {
                    sb.AppendLine("        __listBinder_" + list.Ordinal + " ??= " + list.MethodName + ";");
                    sb.AppendLine("        if (__list_" + list.Ordinal + " == null)");
                    sb.AppendLine("            __list_" + list.Ordinal + " = BindList<" + list.WidgetType + ", " + list.ItemType + ">(\"" + list.ContainerPath + "\", \"" + list.TemplatePath + "\", __listFactory_" + list.Ordinal + ", __listBinder_" + list.Ordinal + ", " + list.MaxIdle + ");");
                    sb.AppendLine("        else RegisterList(__list_" + list.Ordinal + ");");
                    sb.AppendLine("        __list_" + list.Ordinal + ".Bind(Reactive." + list.Source + ");");
                }
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public override void __UnwireReactive()");
                sb.AppendLine("    {");
                foreach (var observer in view.Observers)
                    for (int i = 0; i < observer.Sources.Length; i++)
                        sb.AppendLine("        if (__obs_" + observer.Ordinal + "_" + i + " != null) Reactive." + observer.Sources[i].Name + ".Unsubscribe(__obs_" + observer.Ordinal + "_" + i + ");");
                foreach (var list in view.Lists)
                {
                    sb.AppendLine("        __list_" + list.Ordinal + "?.Unbind();");
                }
                sb.AppendLine("    }");
            }

            if (view.Widgets.Length != 0)
            {
                sb.AppendLine();
                sb.AppendLine("    public override void __BuildWidgets()");
                sb.AppendLine("    {");
                foreach (var widget in view.Widgets)
                {
                    sb.AppendLine("        " + widget.FieldName + " ??= new " + widget.TypeName + "();");
                    sb.AppendLine("        AddWidget(" + widget.FieldName + ", \"" + widget.Path + "\");");
                }
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
            Footer(sb, view.Namespace);
            return sb.ToString();
        }

        private static string EmitTypedOpen(ScreenSpec screen)
        {
            var sb = Header(screen.Namespace);
            sb.AppendLine(screen.Accessibility + " partial class " + screen.ClassName);
            sb.AppendLine("{");
            if (screen.ModelType != null)
            {
                sb.AppendLine("    public static global::LuminThread.LuminTask<global::LuminUI.ScreenHandle<" + screen.FullType + ">> OpenAsync(" + screen.ModelType + " model, global::System.Threading.CancellationToken ct = default)");
                sb.AppendLine("        => global::LuminUI.LuminUi.OpenAsync<" + screen.FullType + ">(model, ct);");
            }
            else
            {
                sb.AppendLine("    public static global::LuminThread.LuminTask<global::LuminUI.ScreenHandle<" + screen.FullType + ">> OpenAsync(global::System.Threading.CancellationToken ct = default)");
                sb.AppendLine("        => global::LuminUI.LuminUi.OpenAsync<" + screen.FullType + ">(null, ct);");
            }
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
                sb.AppendLine("            () => new " + screen.FullType + "(),");
                sb.AppendLine(screen.ReactiveType != null
                    ? "            () => new " + screen.ReactiveType + "());"
                    : "            null);");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ObserverCall(ObserverSpec observer)
        {
            if (observer.ParameterCount == 0) return observer.MethodName + "()";
            return observer.MethodName + "(" + string.Join(", ", observer.Sources.Select(static s => "Reactive." + s.Name + ".Value")) + ")";
        }

        private static INamedTypeSymbol? GetModelType(AttributeData? attribute)
            => attribute != null && attribute.ConstructorArguments.Length > 0
               && attribute.ConstructorArguments[0].Value is INamedTypeSymbol model ? model : null;

        private static ITypeSymbol? MemberType(ISymbol? member)
            => member is IFieldSymbol field ? field.Type
                : member is IPropertySymbol property ? property.Type : null;

        private static string ReactiveTypeName(INamedTypeSymbol model)
        {
            var name = model.Name.EndsWith("Model", StringComparison.Ordinal)
                ? model.Name.Substring(0, model.Name.Length - 5) + "Reactive"
                : model.Name + "Reactive";
            var ns = NamespaceOf(model);
            return string.IsNullOrEmpty(ns) ? "global::" + name : "global::" + ns + "." + name;
        }

        private static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
            => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == metadataName);

        private static string NamespaceOf(INamedTypeSymbol type)
            => type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

        private static string TypeName(ITypeSymbol type)
            => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private static string AccessibilityText(Accessibility accessibility)
            => accessibility == Accessibility.Public ? "public" : "internal";

        private static string? GetStringCtor(AttributeData attr, int index)
            => attr.ConstructorArguments.Length > index && attr.ConstructorArguments[index].Value is string value
                ? value : null;

        private static string[] GetStringArrayCtor(AttributeData attr)
        {
            if (attr.ConstructorArguments.Length == 0) return Array.Empty<string>();
            var arg = attr.ConstructorArguments[0];
            if (arg.Kind == TypedConstantKind.Array)
                return arg.Values.Select(static value => value.Value as string).Where(static value => value != null).Select(static value => value!).ToArray();
            return arg.Value is string single ? new[] { single } : Array.Empty<string>();
        }

        private static int GetNamedInt(AttributeData attr, string name, int fallback)
            => attr.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value is int value ? value : fallback;

        private static float GetNamedFloat(AttributeData attr, string name, float fallback)
            => attr.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value is float value ? value : fallback;

        private static bool GetNamedBool(AttributeData attr, string name, bool fallback)
            => attr.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value is bool value ? value : fallback;

        private static string? GetNamedString(AttributeData attr, string name)
            => attr.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

        private static string EnumName(int value, bool layer)
        {
            if (!layer) return value == 1 ? "Stack" : value == 2 ? "Overlay" : "Normal";
            return value == 0 ? "Background" : value == 200 ? "HUD" : value == 300 ? "Popup"
                : value == 400 ? "Loading" : value == 500 ? "Toast" : "Scene";
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
            => (string.IsNullOrEmpty(ns) ? "" : ns.Replace('.', '_') + "_") + name + ".g.cs";

        private static string Escape(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string Bool(bool value) => value ? "true" : "false";
        private static string Float(float value)
            => value.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + "f";

        private static void AppendParameter(StringBuilder sb, ParameterSpec parameter)
        {
            if (parameter.IsParams) sb.Append("params ");
            if (parameter.RefKind == RefKind.Ref) sb.Append("ref ");
            else if (parameter.RefKind == RefKind.Out) sb.Append("out ");
            else if (parameter.RefKind == RefKind.In) sb.Append("in ");
            sb.Append(parameter.TypeName).Append(" @").Append(parameter.Name);
        }

        private static void AppendArgument(StringBuilder sb, ParameterSpec parameter)
        {
            if (parameter.RefKind == RefKind.Ref) sb.Append("ref ");
            else if (parameter.RefKind == RefKind.Out) sb.Append("out ");
            else if (parameter.RefKind == RefKind.In) sb.Append("in ");
            sb.Append('@').Append(parameter.Name);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminUIGenerator.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminUIGenerator.Generators
{
    /// <summary>
    /// Generates the mechanical half of a top-level [ReactionFor] class and wires
    /// one cached Reaction instance into its target View without runtime reflection.
    /// </summary>
    [Generator]
    internal sealed class ReactionGenerator : IIncrementalGenerator
    {
        private static readonly SymbolDisplayFormat NullableTypeFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        private const string ReactionForAttr = "LuminUI.Attributes.ReactionForAttribute";
        private const string ViewAttr = "LuminUI.Attributes.ViewAttribute";
        private const string ScreenAttr = "LuminUI.Attributes.ScreenAttribute";
        private const string ElementAttr = "LuminUI.Attributes.ElementAttribute";
        private const string UiClickEventAttr = "LuminUI.Attributes.UiClickEventAttribute";

        private const string OnClickAttr = "LuminUI.Attributes.OnClickAttribute";
        private const string OnValueChangedAttr = "LuminUI.Attributes.OnValueChangedAttribute";
        private const string OnTextChangedAttr = "LuminUI.Attributes.OnTextChangedAttribute";
        private const string OnSubmitAttr = "LuminUI.Attributes.OnSubmitAttribute";
        private const string OnPointerEnterAttr = "LuminUI.Attributes.OnPointerEnterAttribute";
        private const string OnPointerExitAttr = "LuminUI.Attributes.OnPointerExitAttribute";
        private const string OnPointerDownAttr = "LuminUI.Attributes.OnPointerDownAttribute";
        private const string OnPointerUpAttr = "LuminUI.Attributes.OnPointerUpAttribute";
        private const string OnDragAttr = "LuminUI.Attributes.OnDragAttribute";
        private const string OnBeginDragAttr = "LuminUI.Attributes.OnBeginDragAttribute";
        private const string OnEndDragAttr = "LuminUI.Attributes.OnEndDragAttribute";

        private static readonly Dictionary<string, string> EventKinds = new Dictionary<string, string>
        {
            [OnClickAttr] = "Click",
            [OnValueChangedAttr] = "ValueChanged",
            [OnTextChangedAttr] = "TextChanged",
            [OnSubmitAttr] = "Submit",
            [OnPointerEnterAttr] = "PointerEnter",
            [OnPointerExitAttr] = "PointerExit",
            [OnPointerDownAttr] = "PointerDown",
            [OnPointerUpAttr] = "PointerUp",
            [OnDragAttr] = "Drag",
            [OnBeginDragAttr] = "BeginDrag",
            [OnEndDragAttr] = "EndDrag",
        };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReactionForAttr,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => Extract(ctx, ct));

            context.RegisterSourceOutput(candidates, static (spc, candidate) =>
            {
                foreach (var diagnostic in candidate.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);
            });

            var specs = candidates.Where(static candidate => candidate.Spec != null)
                .Select(static (candidate, _) => candidate.Spec!);

            context.RegisterSourceOutput(specs.Collect(), static (spc, allSpecs) =>
                EmitAll(spc, allSpecs));
        }

        private static ReactionCandidate Extract(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var reaction = (INamedTypeSymbol)ctx.TargetSymbol;
            var syntax = (ClassDeclarationSyntax)ctx.TargetNode;
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ReactionMustBePartial,
                    syntax.Identifier.GetLocation(), reaction.Name));
                return new ReactionCandidate(null, diagnostics.ToImmutable());
            }

            bool supportedBase = reaction.BaseType == null
                || reaction.BaseType.SpecialType == SpecialType.System_Object
                || IsLuminReaction(reaction.BaseType);
            bool validShape = !reaction.IsAbstract && !reaction.IsGenericType
                && reaction.ContainingType == null && supportedBase;
            if (!validShape)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedReactionShape,
                    syntax.Identifier.GetLocation(), reaction.Name));
                return new ReactionCandidate(null, diagnostics.ToImmutable());
            }

            var attribute = reaction.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == ReactionForAttr);
            var view = attribute?.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
                : null;
            if (view == null || !InheritsLuminView(view)
                || (FindAttribute(view, ViewAttr) == null && FindAttribute(view, ScreenAttr) == null))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidReactionTarget,
                    syntax.Identifier.GetLocation(), reaction.Name));
                return new ReactionCandidate(null, diagnostics.ToImmutable());
            }

            bool hasConstructor = reaction.InstanceConstructors.Any(c =>
                c.Parameters.Length == 0 && c.DeclaredAccessibility != Accessibility.Private);
            if (!hasConstructor)
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ReactionNeedsConstructor,
                    syntax.Identifier.GetLocation(), reaction.Name));

            foreach (var field in reaction.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsImplicitlyDeclared || !IsReactiveContainer(field.Type)) continue;
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ReactionOwnsReactiveState,
                    field.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                    field.Name, reaction.Name));
            }

            foreach (var method in view.GetMembers().OfType<IMethodSymbol>())
            {
                if (!method.GetAttributes().Any(a =>
                    a.AttributeClass != null && EventKinds.ContainsKey(a.AttributeClass.ToDisplayString())))
                    continue;
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ViewEventConflictsWithReaction,
                    method.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                    view.Name, method.Name));
            }

            var elements = ExtractElements(view);
            var events = new List<ReactionEventSpec>();
            var eventKeys = new HashSet<string>(StringComparer.Ordinal);
            int eventIndex = 0;
            foreach (var method in reaction.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (var eventAttribute in method.GetAttributes())
                {
                    string? attributeName = eventAttribute.AttributeClass?.ToDisplayString();
                    if (attributeName == null || !EventKinds.TryGetValue(attributeName, out var kind))
                        continue;
                    string? target = GetStringConstructorArgument(eventAttribute, 0);
                    if (target == null || !elements.TryGetValue(target, out var element))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.ReactionEventTargetNotFound,
                            method.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                            target ?? string.Empty, method.Name, view.Name));
                        continue;
                    }

                    if (!ValidHandler(method, kind))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.InvalidReactionEventHandler,
                            method.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                            method.Name, kind));
                        continue;
                    }

                    string eventKey = target + "|" + kind;
                    if (!eventKeys.Add(eventKey))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateEventBinding,
                            method.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                            target, kind, view.Name));
                        continue;
                    }

                    events.Add(new ReactionEventSpec(
                        element, method.Name, kind, method.Parameters.Length, eventIndex++));
                }
            }

            var spec = new ReactionSpec(
                NamespaceOf(reaction), reaction.Name, TypeName(reaction),
                NamespaceOf(view), view.Name, TypeName(view),
                syntax.Identifier.GetLocation(), hasConstructor, events.ToArray());
            return new ReactionCandidate(spec, diagnostics.ToImmutable());
        }

        private static Dictionary<string, ReactionElementSpec> ExtractElements(INamedTypeSymbol view)
        {
            var result = new Dictionary<string, ReactionElementSpec>(StringComparer.Ordinal);
            foreach (var field in view.GetMembers().OfType<IFieldSymbol>())
            {
                if (FindAttribute(field, ElementAttr) == null) continue;
                string? directEvent = field.Type is INamedTypeSymbol fieldType
                    ? GetStringConstructorArgument(FindAttribute(fieldType, UiClickEventAttr), 0)
                    : null;
                result[field.Name] = new ReactionElementSpec(
                    field.Name, TypeName(field.Type), directEvent);
            }
            return result;
        }

        private static void EmitAll(
            SourceProductionContext context, ImmutableArray<ReactionSpec> specs)
        {
            var byView = new Dictionary<string, List<ReactionSpec>>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                context.AddSource(SafeName(spec.ReactionNamespace, spec.ReactionName + ".Reaction"),
                    EmitReaction(spec));
                if (!byView.TryGetValue(spec.ViewFullType, out var group))
                {
                    group = new List<ReactionSpec>();
                    byView.Add(spec.ViewFullType, group);
                }
                group.Add(spec);
            }

            foreach (var pair in byView)
            {
                var group = pair.Value;
                if (group.Count != 1)
                {
                    foreach (var spec in group)
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateReaction, spec.Location, spec.ViewName));
                    continue;
                }

                var single = group[0];
                if (!single.CanInstantiate) continue;
                context.AddSource(SafeName(single.ViewNamespace,
                    single.ViewName + ".ReactionHost"), EmitViewHost(single));
            }
        }

        private static string EmitReaction(ReactionSpec spec)
        {
            var sb = Header(spec.ReactionNamespace);
            sb.AppendLine("partial class " + spec.ReactionName
                + " : global::LuminUI.LuminReaction<" + spec.ViewFullType + ">");
            sb.AppendLine("{");
            foreach (var evt in spec.Events)
                sb.AppendLine("    " + EmitReactionWrapper(evt));
            sb.AppendLine("}");
            Footer(sb, spec.ReactionNamespace);
            return sb.ToString();
        }

        private static string EmitViewHost(ReactionSpec spec)
        {
            var sb = Header(spec.ViewNamespace);
            sb.AppendLine("partial class " + spec.ViewName);
            sb.AppendLine("{");
            sb.AppendLine("    private " + spec.ReactionFullType + "? __luminReaction;");
            sb.AppendLine("    internal " + spec.ReactionFullType
                + " __Reaction => __luminReaction!;");
            foreach (var evt in spec.Events)
            {
                if (!IsEventTriggerKind(evt.EventKind)) continue;
                sb.AppendLine("    private UnityEngine.EventSystems.EventTrigger.Entry? "
                    + evt.TriggerEntryName + ";");
            }
            sb.AppendLine();
            sb.AppendLine("    public override void __AttachReaction()");
            sb.AppendLine("    {");
            sb.AppendLine("        __luminReaction ??= new " + spec.ReactionFullType + "();");
            sb.AppendLine("        __luminReaction.__Attach(this);");
            if (spec.Events.Length != 0)
            {
                sb.AppendLine("        try");
                sb.AppendLine("        {");
                foreach (var evt in spec.Events) sb.Append(EmitWire(evt, "            "));
                sb.AppendLine("        }");
                sb.AppendLine("        catch");
                sb.AppendLine("        {");
                sb.AppendLine("            __luminReaction.__Detach();");
                sb.AppendLine("            throw;");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void __DetachReaction()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (__luminReaction == null) return;");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            foreach (var evt in spec.Events) sb.Append(EmitUnwire(evt, "            "));
            sb.AppendLine("        }");
            sb.AppendLine("        finally");
            sb.AppendLine("        {");
            sb.AppendLine("            __luminReaction.__Detach();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            Footer(sb, spec.ViewNamespace);
            return sb.ToString();
        }

        private static string EmitReactionWrapper(ReactionEventSpec evt)
        {
            string call = evt.MethodName + "(" + (evt.ParameterCount == 0 ? "" : "value") + ");";
            switch (evt.EventKind)
            {
                case "Click":
                    return "internal void " + evt.WrapperName + "() => " + call;
                case "ValueChanged":
                    return "internal void " + evt.WrapperName + "("
                        + ValueChangedType(evt.Element.TypeName) + " value) => " + call;
                case "TextChanged":
                case "Submit":
                    return "internal void " + evt.WrapperName + "(string value) => " + call;
                default:
                    return "internal void " + evt.WrapperName
                        + "(UnityEngine.EventSystems.BaseEventData value) => " + call;
            }
        }

        private static string EmitWire(ReactionEventSpec evt, string indent)
        {
            string field = evt.Element.FieldName;
            string handler = "__luminReaction." + evt.WrapperName;
            if (evt.Element.DirectEventName != null)
                return indent + "if (" + field + " != null) " + field + "."
                    + evt.Element.DirectEventName + " += " + handler + ";\n";

            switch (evt.EventKind)
            {
                case "Click":
                    return indent + "if (" + field + " != null) " + field
                        + ".onClick.AddListener(" + handler + ");\n";
                case "ValueChanged":
                case "TextChanged":
                    return indent + "if (" + field + " != null) " + field
                        + ".onValueChanged.AddListener(" + handler + ");\n";
                case "Submit":
                    return indent + "if (" + field + " != null) " + field
                        + ".onSubmit.AddListener(" + handler + ");\n";
                default:
                    return EmitTriggerWire(evt, indent, handler);
            }
        }

        private static string EmitUnwire(ReactionEventSpec evt, string indent)
        {
            string field = evt.Element.FieldName;
            string handler = "__luminReaction." + evt.WrapperName;
            if (evt.Element.DirectEventName != null)
                return indent + "if (" + field + " != null) " + field + "."
                    + evt.Element.DirectEventName + " -= " + handler + ";\n";
            if (IsEventTriggerKind(evt.EventKind))
                return EmitTriggerUnwire(evt, indent);

            switch (evt.EventKind)
            {
                case "Click":
                    return indent + "if (" + field + " != null) " + field
                        + ".onClick.RemoveListener(" + handler + ");\n";
                case "ValueChanged":
                case "TextChanged":
                    return indent + "if (" + field + " != null) " + field
                        + ".onValueChanged.RemoveListener(" + handler + ");\n";
                case "Submit":
                    return indent + "if (" + field + " != null) " + field
                        + ".onSubmit.RemoveListener(" + handler + ");\n";
                default:
                    return string.Empty;
            }
        }

        private static string EmitTriggerWire(
            ReactionEventSpec evt, string indent, string handler)
        {
            string field = evt.Element.FieldName;
            string trigger = "__luminTrigger" + evt.Index;
            return
                indent + "if (" + field + " != null)\n" + indent + "{\n" +
                indent + "    var " + trigger + " = " + field
                    + ".GetComponent<UnityEngine.EventSystems.EventTrigger>()\n" +
                indent + "        ?? " + field
                    + ".gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();\n" +
                indent + "    " + evt.TriggerEntryName
                    + " = new UnityEngine.EventSystems.EventTrigger.Entry();\n" +
                indent + "    " + evt.TriggerEntryName
                    + ".eventID = UnityEngine.EventSystems.EventTriggerType."
                    + evt.EventKind + ";\n" +
                indent + "    " + evt.TriggerEntryName
                    + ".callback.AddListener(" + handler + ");\n" +
                indent + "    " + trigger + ".triggers.Add("
                    + evt.TriggerEntryName + ");\n" + indent + "}\n";
        }

        private static string EmitTriggerUnwire(ReactionEventSpec evt, string indent)
        {
            string field = evt.Element.FieldName;
            string trigger = "__luminTrigger" + evt.Index;
            return
                indent + "if (" + field + " != null && "
                    + evt.TriggerEntryName + " != null)\n" + indent + "{\n" +
                indent + "    var " + trigger + " = " + field
                    + ".GetComponent<UnityEngine.EventSystems.EventTrigger>();\n" +
                indent + "    if (" + trigger + " != null) " + trigger
                    + ".triggers.Remove(" + evt.TriggerEntryName + ");\n" +
                indent + "    " + evt.TriggerEntryName + " = null;\n" + indent + "}\n";
        }

        private static bool ValidHandler(IMethodSymbol method, string kind)
        {
            if (method.IsStatic || method.ReturnsVoid == false || method.IsGenericMethod)
                return false;
            if (kind == "Click") return method.Parameters.Length == 0;
            return method.Parameters.Length <= 1;
        }

        private static bool IsEventTriggerKind(string kind)
        {
            switch (kind)
            {
                case "PointerEnter":
                case "PointerExit":
                case "PointerDown":
                case "PointerUp":
                case "Drag":
                case "BeginDrag":
                case "EndDrag":
                    return true;
                default:
                    return false;
            }
        }

        private static string ValueChangedType(string fieldType)
        {
            if (fieldType.Contains("Toggle")) return "bool";
            if (fieldType.Contains("Dropdown")) return "int";
            return "float";
        }

        private static bool IsReactiveContainer(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named) return false;
            string definition = named.ConstructedFrom.ToDisplayString();
            return definition == "LuminUI.ReactiveProperty<T>"
                || definition == "LuminUI.ReactiveCollection<T>"
                || definition == "LuminUI.ReactiveDictionary<TKey, TValue>";
        }

        private static bool IsLuminReaction(INamedTypeSymbol type)
            => type.ConstructedFrom.ToDisplayString() == "LuminUI.LuminReaction<TView>";

        private static bool InheritsLuminView(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.ToDisplayString() == "LuminUI.LuminView") return true;
            return false;
        }

        private static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
            => symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == metadataName);

        private static string? GetStringConstructorArgument(AttributeData? attribute, int index)
            => attribute != null && attribute.ConstructorArguments.Length > index
                && attribute.ConstructorArguments[index].Value is string value ? value : null;

        private static string NamespaceOf(INamedTypeSymbol type)
            => type.ContainingNamespace.IsGlobalNamespace
                ? string.Empty : type.ContainingNamespace.ToDisplayString();

        private static string TypeName(ITypeSymbol type)
            => type.ToDisplayString(NullableTypeFormat);

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
            => (string.IsNullOrEmpty(ns) ? name : ns + "." + name)
                .Replace('.', '_') + ".g.cs";

        private sealed class ReactionCandidate
        {
            public ReactionCandidate(ReactionSpec? spec, ImmutableArray<Diagnostic> diagnostics)
            {
                Spec = spec;
                Diagnostics = diagnostics;
            }

            public ReactionSpec? Spec { get; }
            public ImmutableArray<Diagnostic> Diagnostics { get; }
        }

        private sealed class ReactionSpec
        {
            public ReactionSpec(
                string reactionNamespace, string reactionName, string reactionFullType,
                string viewNamespace, string viewName, string viewFullType,
                Location location, bool canInstantiate, ReactionEventSpec[] events)
            {
                ReactionNamespace = reactionNamespace;
                ReactionName = reactionName;
                ReactionFullType = reactionFullType;
                ViewNamespace = viewNamespace;
                ViewName = viewName;
                ViewFullType = viewFullType;
                Location = location;
                CanInstantiate = canInstantiate;
                Events = events;
            }

            public string ReactionNamespace { get; }
            public string ReactionName { get; }
            public string ReactionFullType { get; }
            public string ViewNamespace { get; }
            public string ViewName { get; }
            public string ViewFullType { get; }
            public Location Location { get; }
            public bool CanInstantiate { get; }
            public ReactionEventSpec[] Events { get; }
        }

        private sealed class ReactionElementSpec
        {
            public ReactionElementSpec(string fieldName, string typeName, string? directEventName)
            {
                FieldName = fieldName;
                TypeName = typeName;
                DirectEventName = directEventName;
            }

            public string FieldName { get; }
            public string TypeName { get; }
            public string? DirectEventName { get; }
        }

        private sealed class ReactionEventSpec
        {
            public ReactionEventSpec(
                ReactionElementSpec element, string methodName, string eventKind,
                int parameterCount, int index)
            {
                Element = element;
                MethodName = methodName;
                EventKind = eventKind;
                ParameterCount = parameterCount;
                Index = index;
            }

            public ReactionElementSpec Element { get; }
            public string MethodName { get; }
            public string EventKind { get; }
            public int ParameterCount { get; }
            public int Index { get; }
            public string WrapperName => "__LuminEvent" + Index;
            public string TriggerEntryName => "__luminTriggerEntry" + Index;
        }
    }
}

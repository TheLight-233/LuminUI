using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using LuminUIGenerator.Diagnostics;
using LuminUIGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminUIGenerator.Generators
{
    /// <summary>
    /// 为 [View]（组件）/ [Screen]（可打开的屏）标注的视图生成 __Bind / __WireEvents / __UnwireEvents 分部实现。
    /// [Screen] 隐含 [View]：若同一类两者都标，仅 [View] 分支产出，[Screen] 分支跳过（避免重复 __Bind）。
    /// 生成的分部类不重述基类（基类由用户那一份分部声明），避免泛型基类不匹配。
    /// 事件接线对 Unity 组件直连（性能最优），并全部以 if(field != null) 守卫（空桥接测试安全）。
    /// </summary>
    [Generator]
    internal sealed class ViewGenerator : IIncrementalGenerator
    {
        private const string ViewAttr      = "LuminUI.Attributes.ViewAttribute";
        private const string ScreenAttr    = "LuminUI.Attributes.ScreenAttribute";
        private const string ElementAttr   = "LuminUI.Attributes.ElementAttribute";
        private const string UiClickEvtAttr= "LuminUI.Attributes.UiClickEventAttribute";

        private const string OnClickAttr    = "LuminUI.Attributes.OnClickAttribute";
        private const string OnValueChAttr  = "LuminUI.Attributes.OnValueChangedAttribute";
        private const string OnTextChAttr   = "LuminUI.Attributes.OnTextChangedAttribute";
        private const string OnSubmitAttr   = "LuminUI.Attributes.OnSubmitAttribute";
        private const string OnPtrEnterAttr = "LuminUI.Attributes.OnPointerEnterAttribute";
        private const string OnPtrExitAttr  = "LuminUI.Attributes.OnPointerExitAttribute";
        private const string OnPtrDownAttr  = "LuminUI.Attributes.OnPointerDownAttribute";
        private const string OnPtrUpAttr    = "LuminUI.Attributes.OnPointerUpAttribute";
        private const string OnDragAttr     = "LuminUI.Attributes.OnDragAttribute";
        private const string OnBeginDragAttr= "LuminUI.Attributes.OnBeginDragAttribute";
        private const string OnEndDragAttr  = "LuminUI.Attributes.OnEndDragAttribute";

        private static readonly Dictionary<string, string> EventAttrMap = new Dictionary<string, string>
        {
            [OnClickAttr]     = "Click",
            [OnValueChAttr]   = "ValueChanged",
            [OnTextChAttr]    = "TextChanged",
            [OnSubmitAttr]    = "Submit",
            [OnPtrEnterAttr]  = "PointerEnter",
            [OnPtrExitAttr]   = "PointerExit",
            [OnPtrDownAttr]   = "PointerDown",
            [OnPtrUpAttr]     = "PointerUp",
            [OnDragAttr]      = "Drag",
            [OnBeginDragAttr] = "BeginDrag",
            [OnEndDragAttr]   = "EndDrag",
        };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var views = context.SyntaxProvider.ForAttributeWithMetadataName(
                ViewAttr,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) => Extract(ctx, ct, fromScreen: false));

            var screens = context.SyntaxProvider.ForAttributeWithMetadataName(
                ScreenAttr,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, ct) => Extract(ctx, ct, fromScreen: true));

            Register(context, views);
            Register(context, screens);
        }

        private static void Register(IncrementalGeneratorInitializationContext context,
            Microsoft.CodeAnalysis.IncrementalValuesProvider<(ViewInfo? Info, Diagnostic? Diagnostic)> results)
        {
            context.RegisterSourceOutput(results, static (spc, r) =>
            {
                if (r.Diagnostic != null) spc.ReportDiagnostic(r.Diagnostic);
            });

            var infos = results.Where(static r => r.Info != null).Select(static (r, _) => r.Info!);

            context.RegisterSourceOutput(infos, static (spc, info) =>
            {
                var src = Emit(info);
                var safeFileName = string.IsNullOrEmpty(info.Namespace)
                    ? info.ClassName + ".g.cs"
                    : info.Namespace.Replace('.', '_') + "_" + info.ClassName + ".g.cs";
                spc.AddSource(safeFileName, src);
            });
        }

        private static (ViewInfo? Info, Diagnostic? Diagnostic) Extract(
            GeneratorAttributeSyntaxContext ctx, CancellationToken ct, bool fromScreen)
        {
            ct.ThrowIfCancellationRequested();
            if (ctx.TargetSymbol is not INamedTypeSymbol cls) return (null, null);
            var syntax = (ClassDeclarationSyntax)ctx.TargetNode;

            // 去重：[Screen] 隐含 [View]；若同时标了 [View]，交给 [View] 分支产出，避免重复 __Bind。
            if (fromScreen && FindAttr(cls, ViewAttr) != null) return (null, null);

            if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                return (null, Diag(DiagnosticDescriptors.ViewMustBePartial,
                                   syntax.Identifier.GetLocation(), cls.Name));

            if (!InheritsLuminView(cls))
                return (null, Diag(DiagnosticDescriptors.ViewMustInheritLuminView,
                                   syntax.Identifier.GetLocation(), cls.Name));

            var ns        = Ns(cls);
            var fields    = new List<FieldInfo>();
            var pathTrack = new Dictionary<string, string>();
            var diags     = new List<Diagnostic>();

            foreach (var m in cls.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (m is not IFieldSymbol field) continue;
                var uiElem = FindAttr(field, ElementAttr);
                if (uiElem == null) continue;

                var ft        = field.Type as INamedTypeSymbol;
                var clickAttr = ft != null ? FindAttr(ft, UiClickEvtAttr) : null;
                var explicitPath = GetElementPath(uiElem);
                var resolved  = explicitPath ?? FieldToPath(field.Name);

                if (pathTrack.TryGetValue(resolved, out var prev))
                    diags.Add(Diag(DiagnosticDescriptors.UiElementPathConflict,
                        syntax.Identifier.GetLocation(), prev, field.Name, cls.Name, resolved));
                else
                    pathTrack[resolved] = field.Name;

                fields.Add(new FieldInfo
                {
                    FieldName      = field.Name,
                    TypeFullName   = field.Type.ToDisplayString(),
                    ExplicitPath   = explicitPath,
                    ClickEventName = clickAttr != null ? GetCtor(clickAttr, 0) : null,
                });
            }

            var events   = new List<EventBinding>();
            var evtTrack = new Dictionary<string, bool>();
            var fieldMap = new Dictionary<string, FieldInfo>();
            foreach (var f in fields) fieldMap[f.FieldName] = f;

            foreach (var m in cls.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (m is not IMethodSymbol method) continue;

                foreach (var attr in method.GetAttributes())
                {
                    var an = attr.AttributeClass?.ToDisplayString();
                    if (an == null || !EventAttrMap.TryGetValue(an, out var kind)) continue;
                    var fn = GetCtor(attr, 0);
                    if (fn == null) continue;

                    if (!fieldMap.ContainsKey(fn))
                    {
                        var loc = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
                        diags.Add(Diag(DiagnosticDescriptors.UiElementFieldNotFound,
                            loc, fn, an.Substring(an.LastIndexOf('.') + 1), method.Name));
                        continue;
                    }

                    var tk = fn + "|" + kind;
                    if (evtTrack.ContainsKey(tk))
                    {
                        var loc = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
                        diags.Add(Diag(DiagnosticDescriptors.DuplicateEventBinding, loc, fn, kind, cls.Name));
                        continue;
                    }
                    evtTrack[tk] = true;

                    fieldMap.TryGetValue(fn, out var fi);
                    events.Add(new EventBinding
                    {
                        FieldName        = fn,
                        TypeFullName     = fi?.TypeFullName ?? "",
                        MethodName       = method.Name,
                        EventKind        = kind,
                        DirectEventName  = fi?.ClickEventName,
                        MethodParamCount = method.Parameters.Length,
                    });
                }
            }

            return (new ViewInfo
            {
                Namespace = ns,
                ClassName = cls.Name,
                Fields    = fields.ToArray(),
                Events    = events.ToArray(),
            }, null);
        }

        private static string Emit(ViewInfo p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// LuminUIGenerator");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            bool hasNs = !string.IsNullOrEmpty(p.Namespace);
            if (hasNs) { sb.AppendLine("namespace " + p.Namespace); sb.AppendLine("{"); }

            // 不重述基类：由用户那份分部声明决定基类，规避泛型基类写法不一致
            sb.AppendLine("partial class " + p.ClassName);
            sb.AppendLine("{");

            foreach (var e in p.Events)
            {
                if (!IsEventTriggerKind(e.EventKind)) continue;
                var ev = "__et_" + e.FieldName.TrimStart('_') + "_" + e.EventKind;
                sb.AppendLine("    private UnityEngine.EventSystems.EventTrigger.Entry? " + ev + ";");
            }
            sb.AppendLine();

            // __Bind
            sb.AppendLine("    public override void __Bind(LuminUI.Interface.IUiBridge bridge, object root)");
            sb.AppendLine("    {");
            foreach (var f in p.Fields)
                sb.AppendLine("        " + f.FieldName + " = bridge.Find<" + f.TypeFullName
                              + ">(root, \"" + (f.ExplicitPath ?? FieldToPath(f.FieldName)) + "\")!;");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var e in p.Events)
            {
                var wrapper = GetWrapperMethodName(e);
                if (wrapper == null) continue;
                sb.Append(EmitWrapperMethod(e, wrapper));
            }

            sb.AppendLine("    public override void __WireEvents()");
            sb.AppendLine("    {");
            foreach (var e in p.Events) sb.Append(EmitWire(e));
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    public override void __UnwireEvents()");
            sb.AppendLine("    {");
            foreach (var e in p.Events) sb.Append(EmitUnwire(e));
            sb.AppendLine("    }");

            sb.AppendLine("}");
            if (hasNs) sb.AppendLine("}");
            return sb.ToString();
        }

        // ── wrapper / wire / unwire ───────────────────────────────────────
        private static string? GetWrapperMethodName(EventBinding e)
        {
            switch (e.EventKind)
            {
                case "Click": return null;
                case "ValueChanged":
                case "TextChanged":
                case "Submit":
                    return e.MethodParamCount == 0
                        ? "__w_" + e.FieldName.TrimStart('_') + "_" + e.EventKind
                        : null;
                default:
                    return "__w_" + e.FieldName.TrimStart('_') + "_" + e.EventKind;
            }
        }

        private static string EmitWrapperMethod(EventBinding e, string wrapperName)
        {
            switch (e.EventKind)
            {
                case "ValueChanged":
                    var vcType = GetValueChangedParamType(e.TypeFullName);
                    return "    private void " + wrapperName + "(" + vcType + " __) => " + e.MethodName + "();\n";
                case "TextChanged":
                case "Submit":
                    return "    private void " + wrapperName + "(string __) => " + e.MethodName + "();\n";
                default:
                    return "    private void " + wrapperName
                           + "(UnityEngine.EventSystems.BaseEventData __) => " + e.MethodName + "();\n";
            }
        }

        private static string EmitWire(EventBinding e)
        {
            var f = e.FieldName;
            var m = e.MethodName;

            if (e.DirectEventName != null)
                return "        if (" + f + " != null) " + f + "." + e.DirectEventName + " += " + m + ";\n";

            var callTarget = GetWrapperMethodName(e) ?? m;
            switch (e.EventKind)
            {
                case "Click":        return "        if (" + f + " != null) " + f + ".onClick.AddListener(" + callTarget + ");\n";
                case "ValueChanged": return "        if (" + f + " != null) " + f + ".onValueChanged.AddListener(" + callTarget + ");\n";
                case "TextChanged":  return "        if (" + f + " != null) " + f + ".onValueChanged.AddListener(" + callTarget + ");\n";
                case "Submit":       return "        if (" + f + " != null) " + f + ".onSubmit.AddListener(" + callTarget + ");\n";
                default:             return EmitTriggerWire(f, callTarget, e.EventKind);
            }
        }

        private static string EmitUnwire(EventBinding e)
        {
            var f = e.FieldName;
            var m = e.MethodName;

            if (e.DirectEventName != null)
                return "        if (" + f + " != null) " + f + "." + e.DirectEventName + " -= " + m + ";\n";

            var callTarget = GetWrapperMethodName(e) ?? m;
            if (IsEventTriggerKind(e.EventKind)) return EmitTriggerUnwire(f, callTarget, e.EventKind);

            switch (e.EventKind)
            {
                case "Click":        return "        if (" + f + " != null) " + f + ".onClick.RemoveListener(" + callTarget + ");\n";
                case "ValueChanged": return "        if (" + f + " != null) " + f + ".onValueChanged.RemoveListener(" + callTarget + ");\n";
                case "TextChanged":  return "        if (" + f + " != null) " + f + ".onValueChanged.RemoveListener(" + callTarget + ");\n";
                case "Submit":       return "        if (" + f + " != null) " + f + ".onSubmit.RemoveListener(" + callTarget + ");\n";
                default:             return "";
            }
        }

        private static string EmitTriggerWire(string field, string method, string evtType)
        {
            var tv = "__tr_" + field.TrimStart('_');
            var ev = "__et_" + field.TrimStart('_') + "_" + evtType;
            return
                "        if (" + field + " != null)\n        {\n" +
                "            var " + tv + " = " + field + ".GetComponent<UnityEngine.EventSystems.EventTrigger>()\n" +
                "                          ?? " + field + ".gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();\n" +
                "            " + ev + " = new UnityEngine.EventSystems.EventTrigger.Entry();\n" +
                "            " + ev + ".eventID = UnityEngine.EventSystems.EventTriggerType." + evtType + ";\n" +
                "            " + ev + ".callback.AddListener(" + method + ");\n" +
                "            " + tv + ".triggers.Add(" + ev + ");\n        }\n";
        }

        private static string EmitTriggerUnwire(string field, string method, string evtType)
        {
            var tv = "__tr_" + field.TrimStart('_');
            var ev = "__et_" + field.TrimStart('_') + "_" + evtType;
            return
                "        if (" + field + " != null && " + ev + " != null)\n        {\n" +
                "            var " + tv + " = " + field + ".GetComponent<UnityEngine.EventSystems.EventTrigger>();\n" +
                "            if (" + tv + " != null) " + tv + ".triggers.Remove(" + ev + ");\n" +
                "            " + ev + " = null;\n        }\n";
        }

        private static bool IsEventTriggerKind(string kind)
        {
            switch (kind)
            {
                case "PointerEnter": case "PointerExit": case "PointerDown": case "PointerUp":
                case "Drag": case "BeginDrag": case "EndDrag": return true;
                default: return false;
            }
        }

        private static string GetValueChangedParamType(string typeFull)
        {
            if (typeFull.Contains("Toggle"))   return "bool";
            if (typeFull.Contains("Dropdown")) return "int";
            return "float";
        }

        private static bool InheritsLuminView(INamedTypeSymbol cls)
        {
            for (var t = cls.BaseType; t != null; t = t.BaseType)
            {
                var n = t.ConstructedFrom.ToDisplayString();
                if (n == "LuminUI.LuminView" || n.StartsWith("LuminUI.LuminView<", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string FieldToPath(string name)
        {
            var s = name.TrimStart('_');
            if (s.StartsWith("m_", StringComparison.Ordinal)) s = s.Substring(2);
            if (s.Length == 0) return name;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string Ns(INamedTypeSymbol cls)
            => cls.ContainingNamespace.IsGlobalNamespace ? "" : cls.ContainingNamespace.ToDisplayString();

        private static AttributeData? FindAttr(ISymbol sym, string full)
        {
            foreach (var a in sym.GetAttributes())
                if (a.AttributeClass?.ToDisplayString() == full) return a;
            return null;
        }

        // 兼容 [Element(Path="...")]（命名参数）与 [Element("...")]（位置参数）两种写法
        private static string? GetElementPath(AttributeData a)
        {
            foreach (var kv in a.NamedArguments)
                if (kv.Key == "Path" && kv.Value.Value is string s) return s;
            if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string cs) return cs;
            return null;
        }

        private static string? GetCtor(AttributeData a, int idx) =>
            a.ConstructorArguments.Length > idx && a.ConstructorArguments[idx].Value is string s ? s : null;

        private static Diagnostic Diag(DiagnosticDescriptor d, Location loc, params object?[] args)
            => Diagnostic.Create(d, loc, args);
    }
}

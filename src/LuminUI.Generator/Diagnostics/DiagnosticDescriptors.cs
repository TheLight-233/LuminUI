using Microsoft.CodeAnalysis;

namespace LuminUIGenerator.Diagnostics
{
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor ViewMustBePartial = new DiagnosticDescriptor(
            "LUIN001", "View class must be partial",
            "Class '{0}' marked with [View]/[Screen] must be declared as partial",
            "LuminUI.Generator", DiagnosticSeverity.Error, true,
            "Add the 'partial' modifier to the class declaration.");

        public static readonly DiagnosticDescriptor ViewMustInheritLuminView = new DiagnosticDescriptor(
            "LUIN002", "View class must inherit LuminView",
            "Class '{0}' marked with [View]/[Screen] must inherit LuminView",
            "LuminUI.Generator", DiagnosticSeverity.Error, true,
            "Change the base class to LuminView.");

        public static readonly DiagnosticDescriptor UiElementFieldNotFound = new DiagnosticDescriptor(
            "LUIN003", "Event target field not found",
            "Field '{0}' referenced by [{1}] on method '{2}' was not found or not marked with [UiElement]",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Ensure the field exists and is marked with [UiElement].");

        public static readonly DiagnosticDescriptor DuplicateEventBinding = new DiagnosticDescriptor(
            "LUIN004", "Duplicate event binding",
            "Field '{0}' is bound to event '{1}' multiple times in class '{2}'",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Each field should only have one binding per event type.");

        public static readonly DiagnosticDescriptor UiElementPathConflict = new DiagnosticDescriptor(
            "LUIN008", "Multiple UiElement fields resolve to the same path",
            "Fields '{0}' and '{1}' in class '{2}' both resolve to path '{3}'",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Use explicit Path= on [UiElement] to avoid ambiguity.");

    }
}

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
            "Field '{0}' referenced by [{1}] on method '{2}' was not found or not marked with [Element]",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Ensure the field exists and is marked with [Element].");

        public static readonly DiagnosticDescriptor DuplicateEventBinding = new DiagnosticDescriptor(
            "LUIN004", "Duplicate event binding",
            "Field '{0}' is bound to event '{1}' multiple times in class '{2}'",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Each field should only have one binding per event type.");

        public static readonly DiagnosticDescriptor UiElementPathConflict = new DiagnosticDescriptor(
            "LUIN008", "Multiple Element fields resolve to the same path",
            "Fields '{0}' and '{1}' in class '{2}' both resolve to path '{3}'",
            "LuminUI.Generator", DiagnosticSeverity.Warning, true,
            "Use explicit Path= on [Element] to avoid ambiguity.");

        public static readonly DiagnosticDescriptor ReactionMustBePartial = new DiagnosticDescriptor(
            "LUIN200", "Reaction class must be partial",
            "Class '{0}' marked with [ReactionFor] must be declared partial",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor UnsupportedReactionShape = new DiagnosticDescriptor(
            "LUIN201", "Unsupported Reaction declaration",
            "Reaction '{0}' must be a concrete non-generic top-level class without an explicit base class",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor InvalidReactionTarget = new DiagnosticDescriptor(
            "LUIN202", "Invalid Reaction target",
            "Reaction '{0}' must target a class marked [View] or [Screen] that inherits LuminView",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor DuplicateReaction = new DiagnosticDescriptor(
            "LUIN203", "View has multiple Reactions",
            "View '{0}' is targeted by multiple [ReactionFor] classes",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor ReactionOwnsReactiveState = new DiagnosticDescriptor(
            "LUIN204", "Reaction cannot own reactive state",
            "Field '{0}' in Reaction '{1}' cannot be a ReactiveProperty, ReactiveCollection, or ReactiveDictionary",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor ReactionNeedsConstructor = new DiagnosticDescriptor(
            "LUIN205", "Reaction needs a parameterless constructor",
            "Reaction '{0}' must provide a non-private parameterless constructor",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor ReactionEventTargetNotFound = new DiagnosticDescriptor(
            "LUIN206", "Reaction event target not found",
            "Element field '{0}' referenced by Reaction method '{1}' was not found on View '{2}'",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor ViewEventConflictsWithReaction = new DiagnosticDescriptor(
            "LUIN207", "View event logic belongs in Reaction",
            "View '{0}' has a Reaction, so event method '{1}' must move to that Reaction",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor InvalidReactionEventHandler = new DiagnosticDescriptor(
            "LUIN208", "Invalid Reaction event handler",
            "Reaction event method '{0}' has an unsupported parameter list for event '{1}'",
            "LuminUI.Reaction", DiagnosticSeverity.Error, true);

    }
}

using LuminUIGenerator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace LuminUI.Generator.Tests;

public sealed class ReactionGeneratorTests
{
    [Fact]
    public void GeneratesReactionBaseLifecycleHostAndTypedEventBridge()
    {
        const string source = """
            using System;
            using LuminUI;
            using LuminUI.Attributes;

            namespace Demo;

            [UiClickEvent(nameof(Clicked))]
            public sealed class TestButton
            {
                public event Action? Clicked;
                public void Click() => Clicked?.Invoke();
            }

            [LuminModel]
            public sealed partial class CounterModel
            {
                private readonly ReactiveProperty<int> _count = new();
                public void Add() => _count.Value++;
            }

            [View]
            public partial class CounterView : LuminView
            {
                [Element("Add")]
                internal TestButton AddButton = null!;

                internal void RenderCount(int value) { }
            }

            [ReactionFor(typeof(CounterView))]
            public sealed partial class CounterReaction
            {
                private static CounterModel Model { get; } = new();

                protected override void OnBind()
                    => Subscribe(Model.Count, View.RenderCount);

                [OnClick(nameof(CounterView.AddButton))]
                private void Add() => Model.Add();
            }
            """;

        var result = Run(source, out var output);
        var generated = string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        Assert.Contains("LuminReaction<global::Demo.CounterView>", generated);
        Assert.Contains("__luminReaction.__Attach(this)", generated);
        Assert.Contains("Clicked += __luminReaction.__LuminEvent0", generated);
        Assert.Contains("Clicked -= __luminReaction.__LuminEvent0", generated);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void NonPartialReactionReportsDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [View]
            public partial class CounterView : LuminView { }

            [ReactionFor(typeof(CounterView))]
            public sealed class CounterReaction { }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN200");
    }

    [Fact]
    public void ReactionReactiveStateReportsDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [View]
            public partial class CounterView : LuminView { }

            [ReactionFor(typeof(CounterView))]
            public sealed partial class CounterReaction
            {
                private readonly ReactiveProperty<int> _localState = new();
            }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN204");
    }

    [Fact]
    public void MultipleReactionsForOneViewReportDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [View]
            public partial class CounterView : LuminView { }

            [ReactionFor(typeof(CounterView))]
            public sealed partial class FirstReaction { }

            [ReactionFor(typeof(CounterView))]
            public sealed partial class SecondReaction { }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN203");
    }

    private static GeneratorDriverRunResult Run(string source, out Compilation output)
    {
        var compilation = CSharpCompilation.Create(
            "ReactionFixture",
            new[] { CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Latest)) },
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[]
            {
                new MvrGenerator().AsSourceGenerator(),
                new ReactionGenerator().AsSourceGenerator()
            },
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
        return driver.GetRunResult();
    }

    private static IEnumerable<MetadataReference> References()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
            if (seen.Add(path)) yield return MetadataReference.CreateFromFile(path);

        var lumin = typeof(LuminView).Assembly.Location;
        if (seen.Add(lumin)) yield return MetadataReference.CreateFromFile(lumin);
    }
}

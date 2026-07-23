using LuminThread;
using LuminUIGenerator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace LuminUI.Generator.Tests;

public sealed class MvrGeneratorTests
{
    [Fact]
    public void GeneratesReactiveObserverActionRegistrationAndTypedOpen()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            namespace Demo;

            [LuminModel]
            public sealed class CounterModel
            {
                public ReactiveProperty<int> Count { get; } = new(1);
                [LuminAction] public void Add() => Count.Value++;
            }

            [Screen(typeof(CounterModel))]
            public partial class CounterView : LuminView
            {
                [Observe(nameof(CounterModel.Count))]
                private void Render(int value) { }
                public void Click() => Reactive.Add();
            }
            """;

        var result = Run(source, out var output);
        var generated = string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        Assert.Contains("class CounterReactive", generated);
        Assert.Contains("SubscribeNoPush", generated);
        Assert.Contains("public static", generated);
        Assert.Contains("OpenAsync", generated);
        Assert.Contains("RegisterScreen", generated);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvalidObserveSource_ReportsFocusedDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;
            [LuminModel] public sealed class Model
            {
                public ReactiveProperty<int> Value { get; } = new();
            }
            [View(typeof(Model))] public partial class BadView : LuminView
            {
                [Observe("Missing")] private void Render(int value) { }
            }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN101");
    }

    [Fact]
    public void InvalidMvrContracts_ReportActionableDiagnostics()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            public sealed class NotAModel { }

            [LuminModel] public sealed class MainModel
            {
                public ReactiveProperty<int> Value { get; } = new();
                [LuminAction] private void Hidden() { }
            }

            [LuminModel] public sealed class OtherModel
            {
                public ReactiveProperty<int> Value { get; } = new();
            }

            [View(typeof(OtherModel))] public partial class OtherWidget : LuminView { }

            [View(typeof(MainModel))] public partial class InvalidView : LuminView
            {
                [UiWidget("Child")] private OtherWidget _child = null!;
                [Observe(nameof(MainModel.Value))] private void Render(string value) { }
                [BindList(nameof(MainModel.Value), "Items", "Template")]
                private void Bind(OtherWidget cell, int value, int index) { }
            }

            [View(typeof(NotAModel))] public partial class MissingModelView : LuminView { }
            """;

        var result = Run(source, out _);
        var ids = result.Diagnostics.Select(d => d.Id).ToHashSet();

        Assert.Contains("LUIN102", ids);
        Assert.Contains("LUIN103", ids);
        Assert.Contains("LUIN104", ids);
        Assert.Contains("LUIN105", ids);
        Assert.Contains("LUIN108", ids);
    }

    private static GeneratorDriverRunResult Run(string source, out Compilation output)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorFixture",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MvrGenerator().AsSourceGenerator() },
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
        var task = typeof(LuminTask).Assembly.Location;
        if (seen.Add(task)) yield return MetadataReference.CreateFromFile(task);
    }
}

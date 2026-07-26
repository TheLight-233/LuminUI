using LuminThread;
using LuminUIGenerator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace LuminUI.Generator.Tests;

public sealed class MvrGeneratorTests
{
    [Fact]
    public void GeneratesReadOnlyModelProjectionWidgetRegistrationAndZeroArgumentOpen()
    {
        const string source = """
            #nullable enable
            using LuminUI;
            using LuminUI.Attributes;

            namespace Demo;

            [LuminModel]
            public sealed partial class CounterModel
            {
                private readonly ReactiveProperty<int> _count = new(1);
                private readonly ReactiveProperty<string?> _selected = new(null);
                private readonly ReactiveCollection<string> _items = new();
                private readonly ReactiveDictionary<int, string> _byId = new();
                public void Add() => _count.Value++;
            }

            [View]
            public partial class CounterWidget : LuminView { }

            [Screen]
            public partial class CounterView : LuminView
            {
                [Widget("Counter")]
                private CounterWidget _counter = null!;
            }
            """;

        var result = Run(source, out var output);
        var generated = string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        Assert.Contains("IReadOnlyReactiveProperty", generated);
        Assert.Contains("Count => _count", generated);
        Assert.Contains("IReadOnlyReactiveProperty<string?> Selected", generated);
        Assert.Contains("IReadOnlyReactiveCollection", generated);
        Assert.Contains("Items => _items", generated);
        Assert.Contains("IReadOnlyReactiveDictionary", generated);
        Assert.Contains("ById => _byId", generated);
        Assert.Contains("AddWidget(_counter, \"Counter\")", generated);
        Assert.Contains("OpenAsync(global::System.Threading.CancellationToken ct = default)", generated);
        Assert.Contains("RegisterScreen", generated);
        Assert.DoesNotContain("CounterReactive", generated);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void NonPartialModelReportsDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [LuminModel]
            public sealed class CounterModel
            {
                private readonly ReactiveProperty<int> _count = new();
            }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN100");
    }

    [Fact]
    public void NonPrivateFieldReportsDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [LuminModel]
            public sealed partial class CounterModel
            {
                public readonly ReactiveProperty<int> Count = new();
            }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN101");
    }

    [Fact]
    public void GeneratedPropertyConflictReportsDiagnostic()
    {
        const string source = """
            using LuminUI;
            using LuminUI.Attributes;

            [LuminModel]
            public sealed partial class CounterModel
            {
                private readonly ReactiveProperty<int> _count = new();
                public int Count => 0;
            }
            """;

        var result = Run(source, out _);

        Assert.Contains(result.Diagnostics, d => d.Id == "LUIN103");
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

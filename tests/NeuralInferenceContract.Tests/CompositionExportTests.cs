using System.Xml.Linq;

namespace NeuralInferenceContract.Tests;

public sealed class CompositionExportTests
{
    [Fact(DisplayName = "Команда анализа экспортирует переходы, Mermaid и геометрические точки")]
    public void Analyze_exports_composition_results()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var directory = Directory.CreateTempSubdirectory("neural-composition-");

        try
        {
            var exitCode = ConsoleApplication.Run(["--analyze", "no-initiator", directory.FullName], output, error);

            Assert.Equal(0, exitCode);
            Assert.Empty(error.ToString());
            Assert.Contains("критическая", output.ToString());
            Assert.Contains("Reserve → Accept → DeadlineReached", output.ToString());

            var transitions = File.ReadAllLines(Path.Combine(directory.FullName, "no-initiator.transitions.csv"));
            Assert.Equal(5, transitions.Length);
            Assert.Equal("s3,s3,Wait,Observer,True,0,e3,Processing,True,2,0,0,critical", transitions[^1]);

            var mermaid = File.ReadAllText(Path.Combine(directory.FullName, "no-initiator.mmd"));
            Assert.StartsWith("stateDiagram-v2", mermaid);
            Assert.Contains("s3 --> s3: Wait / 0", mermaid);

            var points = File.ReadAllLines(Path.Combine(directory.FullName, "no-initiator.points.csv"));
            Assert.Equal(9, points.Length);
            Assert.StartsWith("4,1.875,", points[4]);
            Assert.EndsWith(",s3,0,True,True", points[4]);

            var svg = XDocument.Load(Path.Combine(directory.FullName, "no-initiator.svg"));
            XNamespace ns = "http://www.w3.org/2000/svg";
            Assert.Equal(ns + "svg", svg.Root!.Name);
            Assert.Equal(8, svg.Descendants(ns + "circle").Count(circle => circle.Element(ns + "title") is not null));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory(DisplayName = "Команда анализа отклоняет неизвестный или отсутствующий сценарий")]
    [InlineData(null)]
    [InlineData("unknown")]
    public void Analyze_requires_a_known_scenario(string? scenarioName)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = scenarioName is null ? ["--analyze"] : ["--analyze", scenarioName];

        Assert.Equal(2, ConsoleApplication.Run(args, output, error));
        Assert.Empty(output.ToString());
        Assert.NotEmpty(error.ToString());
    }
}

using System.Globalization;
using System.Xml.Linq;

namespace NeuralInferenceContract.Tests;

public sealed class ProbabilityExportTests
{
    [Fact(DisplayName = "Вероятностный анализ экспортирует нормированные пути и дендрограмму независимо от культуры")]
    public void Probability_command_exports_paths_and_dendrogram()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var directory = Directory.CreateTempSubdirectory("neural-probability-");
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var exitCode = ConsoleApplication.Run(["--probability", directory.FullName], output, error);

            Assert.Equal(0, exitCode);
            Assert.Empty(error.ToString());
            Assert.Contains("блокировка: 0.40625", output.ToString());
            Assert.Contains("a=1: оплата=0.375; возврат=0.625; блокировка=0", output.ToString());

            var csv = File.ReadAllLines(Path.Combine(directory.FullName, "probability.paths.csv"));
            Assert.Equal(8, csv.Length);
            var rows = csv.Skip(1).Select(line => line.Split(',')).ToArray();
            Assert.All(rows, row => Assert.Equal(9, row.Length));
            Assert.Equal(1, rows.Sum(row => decimal.Parse(row[2], CultureInfo.InvariantCulture)));
            Assert.Contains(rows, row => row[0] == "10101" && row[2] == "0.03125" && row[3] == "Refunded" && row[4] == "7");

            var mermaid = File.ReadAllText(Path.Combine(directory.FullName, "probability.mmd"));
            Assert.StartsWith("flowchart LR", mermaid);
            Assert.Contains("n1010 -->|1 / 0.5| n10101", mermaid);
            Assert.Contains("n10100[\"10100 · блокировка", mermaid);

            var svg = XDocument.Load(Path.Combine(directory.FullName, "probability.svg"));
            XNamespace ns = "http://www.w3.org/2000/svg";
            Assert.Equal(ns + "svg", svg.Root!.Name);
            Assert.Equal(12, svg.Descendants(ns + "polyline").Count());
            Assert.Contains(svg.Descendants(ns + "text"), text => text.Value == "10100 · блокировка");
            Assert.Contains(svg.Descendants(ns + "text"), text => text.Value == "P = 0.03125; B = 2");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            directory.Delete(recursive: true);
        }
    }

    [Fact(DisplayName = "Вероятностный анализ выполняется без каталога экспорта")]
    public void Probability_command_runs_without_export()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, ConsoleApplication.Run(["--probability"], output, error));
        Assert.Contains("Сумма вероятностей: 1", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact(DisplayName = "Вероятностный анализ отклоняет лишние аргументы")]
    public void Probability_command_rejects_extra_arguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(2, ConsoleApplication.Run(["--probability", "one", "two"], output, error));
        Assert.Empty(output.ToString());
        Assert.NotEmpty(error.ToString());
    }

    [Fact(DisplayName = "Ошибка записи вероятностного экспорта возвращается без аварийного завершения")]
    public void Probability_command_reports_export_errors()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var file = Path.GetTempFileName();

        try
        {
            Assert.Equal(1, ConsoleApplication.Run(["--probability", file], output, error));
            Assert.Empty(output.ToString());
            Assert.Contains("Ошибка экспорта", error.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }
}

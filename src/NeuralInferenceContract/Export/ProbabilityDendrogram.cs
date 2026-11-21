using System.Globalization;
using System.Xml.Linq;
using NeuralInferenceContract.Analysis;

namespace NeuralInferenceContract.Export;

internal static class ProbabilityDendrogram
{
    public static string Render(ProbabilityAnalysis analysis)
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        var height = 170 + analysis.Paths.Count * 84;
        var svg = new XElement(ns + "svg", new XAttribute("viewBox", $"0 0 1200 {height}"),
            new XAttribute("role", "img"), new XAttribute("aria-labelledby", "title description"),
            new XElement(ns + "title", new XAttribute("id", "title"), "Дендрограмма вероятностной среды"),
            new XElement(ns + "desc", new XAttribute("id", "description"),
                "Двоичные пути к оплате, возврату и замкнутым компонентам с заблокированными средствами."),
            new XElement(ns + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"), new XAttribute("fill", "white")));

        void Text(double x, double y, string text, int size = 13, string anchor = "middle") =>
            svg.Add(new XElement(ns + "text", new XAttribute("x", x), new XAttribute("y", y),
                new XAttribute("font-family", "Arial, sans-serif"), new XAttribute("font-size", size),
                new XAttribute("text-anchor", anchor), new XAttribute("fill", "#0f172a"), text));

        Text(28, 36, "Дендрограмма вероятностной среды", 23, "start");
        Text(28, 62, "0 — действие не выполнено; 1 — выполнено. В листьях указаны вероятность пути и остаток резерва B.", 14, "start");

        var positions = new Dictionary<string, (double X, double Y)>();
        for (var index = 0; index < analysis.Paths.Count; index++)
        {
            positions.Add(analysis.Paths[index].Environment.Code, (1060, 120 + index * 84));
        }

        foreach (var decision in analysis.Decisions)
        {
            var descendants = analysis.Paths.Where(path => path.Environment.Code.StartsWith(decision.Code, StringComparison.Ordinal)).ToArray();
            var y = (positions[descendants[0].Environment.Code].Y + positions[descendants[^1].Environment.Code].Y) / 2;
            positions.Add(decision.Code, (80 + decision.Code.Length * 180, y));
        }

        foreach (var decision in analysis.Decisions)
        {
            var parent = positions[decision.Code];
            foreach (var bit in new[] { "0", "1" })
            {
                var code = decision.Code + bit;
                var child = positions[code];
                var childWidth = analysis.Decisions.Any(candidate => candidate.Code == code) ? 120 : 220;
                var middle = parent.X + 90;
                var points = FormattableString.Invariant($"{parent.X + 60},{parent.Y} {middle},{parent.Y} {middle},{child.Y} {child.X - childWidth / 2},{child.Y}");
                svg.Add(new XElement(ns + "polyline", new XAttribute("points", points),
                    new XAttribute("fill", "none"), new XAttribute("stroke", "#94a3b8"), new XAttribute("stroke-width", 2)));
                Text(middle + 10, child.Y - 7, bit);
            }
        }

        void Box(string code, int width, string fill, string stroke)
        {
            var position = positions[code];
            svg.Add(new XElement(ns + "rect", new XAttribute("x", position.X - width / 2), new XAttribute("y", position.Y - 26),
                new XAttribute("width", width), new XAttribute("height", 52), new XAttribute("rx", 7),
                new XAttribute("fill", fill), new XAttribute("stroke", stroke)));
        }

        foreach (var decision in analysis.Decisions)
        {
            var position = positions[decision.Code];
            Box(decision.Code, 120, "#f8fafc", "#64748b");
            Text(position.X, position.Y - 5, decision.Description, 11);
            Text(position.X, position.Y + 15, $"P(1) = {decision.OneProbability.ToString(CultureInfo.InvariantCulture)}", 12);
        }

        foreach (var path in analysis.Paths)
        {
            var position = positions[path.Environment.Code];
            var (fill, stroke) = path.Outcome switch
            {
                ProbabilityOutcome.Settled => ("#dcfce7", "#15803d"),
                ProbabilityOutcome.Refunded => ("#dbeafe", "#1d4ed8"),
                _ => ("#fee2e2", "#b91c1c")
            };
            Box(path.Environment.Code, 220, fill, stroke);
            Text(position.X, position.Y - 5, $"{path.Environment.Code} · {ProbabilityExport.OutcomeText(path.Outcome)}", 15);
            Text(position.X, position.Y + 15,
                FormattableString.Invariant($"P = {path.Environment.Probability}; B = {path.EntryState.Escrow}"));
        }

        Text(28, height - 28, "Отсутствие инициатора означает постоянное ожидание Wait на соответствующем этапе.", 14, "start");
        return svg.ToString().ReplaceLineEndings("\n") + "\n";
    }
}

using System.Globalization;
using System.Xml.Linq;
using NeuralInferenceContract.Analysis;

namespace NeuralInferenceContract.Export;

internal static class GeometricPlot
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static string Render(string scenario, IReadOnlyList<GeometricPoint> points)
    {
        var plot = new XElement(Svg + "svg",
            new XAttribute("viewBox", "0 0 660 350"),
            new XAttribute("width", 660),
            new XAttribute("height", 350),
            new XAttribute("role", "img"),
            new XAttribute("aria-labelledby", "plotTitle plotDescription"),
            new XElement(Svg + "title", new XAttribute("id", "plotTitle"), $"Геометрический образ {scenario}"),
            new XElement(Svg + "desc", new XAttribute("id", "plotDescription"),
                "Точки изображают префиксы тактов автономного сплетения. По оси X закодированы такты, по оси Y — выходы Мили."),
            new XElement(Svg + "style",
                "text{font-family:system-ui,sans-serif;fill:#172635;font-size:13px}.heading{font-size:20px;font-weight:600}.muted{fill:#4b5e70}.grid{stroke:#dae1e8}.axis{stroke:#617689;fill:none}"),
            new XElement(Svg + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"),
                new XAttribute("fill", "#ffffff")),
            Text(32, 32, $"Геометрический образ · {scenario}", "heading"),
            Text(32, 54, $"Префиксы из 1–{points.Count} тактов", "muted"));

        const double left = 68;
        const double width = 544;
        const double top = 80;
        const double height = 180;
        const double bottom = top + height;
        const double yMax = 1.75;

        for (var tick = 0; tick <= 4; tick++)
        {
            var x = left + tick * width / 4;
            plot.Add(Line(x, top, x, bottom), Text(x, bottom + 19, Tick(1 + tick / 4d), anchor: "middle"));
        }

        for (var tick = 0; tick <= 3; tick++)
        {
            var y = bottom - tick * height / 3;
            plot.Add(Line(left, y, left + width, y), Text(left - 10, y + 4, Tick(1 + tick / 4d), anchor: "end"));
        }

        plot.Add(new XElement(Svg + "path",
            new XAttribute("class", "axis"), new XAttribute("d", "M 68 80 V 260 H 612")));
        plot.Add(Text(left, top - 9, "Y", anchor: "middle"), Text(625, bottom + 4, "X"));

        foreach (var point in points)
        {
            var x = left + (point.X - 1) * width;
            var y = bottom - (point.Y - 1) / (yMax - 1) * height;
            var color = point.IsCritical ? "#b52737" : point.InClosedComponent ? "#137652" : "#345b9d";
            plot.Add(new XElement(Svg + "circle",
                new XAttribute("cx", Math.Round(x, 2)),
                new XAttribute("cy", Math.Round(y, 2)),
                new XAttribute("r", point.InClosedComponent ? 4.5 : 3.5),
                new XAttribute("fill", color),
                new XElement(Svg + "title", $"Длина {point.Length}, состояние s{point.StateIndex}, выход {point.Output}")));
        }

        AddLegendItem(plot, 68, "#345b9d", "До входа в компоненту");
        AddLegendItem(plot, 279, "#137652", "Обычная компонента");
        AddLegendItem(plot, 480, "#b52737", "Критическая");
        return plot.ToString().ReplaceLineEndings("\n") + "\n";
    }

    private static string Tick(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void AddLegendItem(XElement plot, int x, string color, string label) =>
        plot.Add(new XElement(Svg + "circle", new XAttribute("cx", x), new XAttribute("cy", 319),
            new XAttribute("r", 4), new XAttribute("fill", color)), Text(x + 11, 324, label));

    private static XElement Line(double x1, double y1, double x2, double y2) =>
        new(Svg + "line", new XAttribute("class", "grid"), new XAttribute("x1", x1), new XAttribute("y1", y1),
            new XAttribute("x2", x2), new XAttribute("y2", y2));

    private static XElement Text(double x, double y, string value, string cssClass = "", string anchor = "start") =>
        new(Svg + "text", new XAttribute("x", x), new XAttribute("y", y),
            new XAttribute("class", cssClass), new XAttribute("text-anchor", anchor), value);
}

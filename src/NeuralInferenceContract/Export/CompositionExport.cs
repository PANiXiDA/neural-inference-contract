using System.Globalization;
using System.Text;
using NeuralInferenceContract.Analysis;

namespace NeuralInferenceContract.Export;

internal static class CompositionExport
{
    public static void Write(string directory, string scenario, CompositionGraph graph, IReadOnlyList<GeometricPoint> points)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{scenario}.transitions.csv"), TransitionsCsv(graph));
        File.WriteAllText(Path.Combine(directory, $"{scenario}.mmd"), Mermaid(graph));
        File.WriteAllText(Path.Combine(directory, $"{scenario}.points.csv"), PointsCsv(points));
        File.WriteAllText(Path.Combine(directory, $"{scenario}.svg"), GeometricPlot.Render(scenario, points));
    }

    private static string TransitionsCsv(CompositionGraph graph)
    {
        var csv = new StringBuilder("from,to,event,actor,accepted,output,environment_state,contract_stage,deadline_reached,escrow,provider_paid,user_refunded,component\n");
        foreach (var edge in graph.Edges)
        {
            var next = graph.States[edge.To];
            var contract = next.Contract;
            var component = edge.To < graph.CycleStart ? "transient" : graph.IsCritical ? "critical" : "closed";
            csv.Append(CultureInfo.InvariantCulture,
                $"s{edge.From},s{edge.To},{edge.Event.Kind},{edge.Event.Actor},{edge.Result.Accepted},{edge.Result.Output},e{next.EnvironmentState},{contract.Stage},{contract.DeadlineReached},{contract.Escrow},{contract.ProviderPaid},{contract.UserRefunded},{component}\n");
        }

        return csv.ToString();
    }

    private static string PointsCsv(IReadOnlyList<GeometricPoint> points)
    {
        var csv = new StringBuilder("length,x,y,state,output,in_closed_component,critical\n");
        foreach (var point in points)
        {
            csv.Append(CultureInfo.InvariantCulture,
                $"{point.Length},{point.X:R},{point.Y:R},s{point.StateIndex},{point.Output},{point.InClosedComponent},{point.IsCritical}\n");
        }

        return csv.ToString();
    }

    private static string Mermaid(CompositionGraph graph)
    {
        var diagram = new StringBuilder("stateDiagram-v2\n    [*] --> s0\n");
        for (var index = 0; index < graph.States.Count; index++)
        {
            var state = graph.States[index];
            var deadline = state.Contract.DeadlineReached ? ", срок истёк" : "";
            var component = index < graph.CycleStart ? "" : graph.IsCritical ? " · критическая" : " · замкнутая";
            diagram.Append(CultureInfo.InvariantCulture,
                $"    state \"(e{state.EnvironmentState}, {state.Contract.Stage}{deadline}) · резерв {state.Contract.Escrow}{component}\" as s{index}\n");
        }

        foreach (var edge in graph.Edges)
        {
            var rejected = edge.Result.Accepted ? "" : " (отклонено)";
            diagram.Append(CultureInfo.InvariantCulture,
                $"    s{edge.From} --> s{edge.To}: {edge.Event.Kind} / {edge.Result.Output}{rejected}\n");
        }

        return diagram.ToString();
    }
}

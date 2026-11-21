using System.Globalization;
using System.Text;
using NeuralInferenceContract.Analysis;

namespace NeuralInferenceContract.Export;

internal static class ProbabilityExport
{
    public static void Write(string directory, ProbabilityAnalysis analysis)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "probability.paths.csv"), PathsCsv(analysis));
        File.WriteAllText(Path.Combine(directory, "probability.mmd"), Mermaid(analysis));
        File.WriteAllText(Path.Combine(directory, "probability.svg"), ProbabilityDendrogram.Render(analysis));
    }

    private static string PathsCsv(ProbabilityAnalysis analysis)
    {
        var csv = new StringBuilder("code,bit_length,probability,outcome,entry_step,escrow,provider_paid,user_refunded,events\n");
        foreach (var path in analysis.Paths)
        {
            var environment = path.Environment;
            var state = path.EntryState;
            var events = string.Join("|", environment.Events.Select(input => input.Kind));
            csv.Append(CultureInfo.InvariantCulture,
                $"{environment.Code},{environment.Code.Length},{environment.Probability},{path.Outcome},{path.Graph.CycleStart},{state.Escrow},{state.ProviderPaid},{state.UserRefunded},{events}\n");
        }

        return csv.ToString();
    }

    private static string Mermaid(ProbabilityAnalysis analysis)
    {
        var diagram = new StringBuilder("flowchart LR\n");
        foreach (var decision in analysis.Decisions)
        {
            diagram.Append(CultureInfo.InvariantCulture,
                $"    n{decision.Code}[\"{decision.Description}<br/>P(1) = {decision.OneProbability}\"]\n");
            diagram.Append(CultureInfo.InvariantCulture,
                $"    n{decision.Code} -->|0 / {1 - decision.OneProbability}| n{decision.Code}0\n");
            diagram.Append(CultureInfo.InvariantCulture,
                $"    n{decision.Code} -->|1 / {decision.OneProbability}| n{decision.Code}1\n");
        }

        foreach (var path in analysis.Paths)
        {
            diagram.Append(CultureInfo.InvariantCulture,
                $"    n{path.Environment.Code}[\"{path.Environment.Code} · {OutcomeText(path.Outcome)}<br/>P = {path.Environment.Probability}, B = {path.EntryState.Escrow}\"]:::{path.Outcome}\n");
        }

        diagram.Append("    classDef Settled fill:#dcfce7,stroke:#15803d\n");
        diagram.Append("    classDef Refunded fill:#dbeafe,stroke:#1d4ed8\n");
        diagram.Append("    classDef Locked fill:#fee2e2,stroke:#b91c1c\n");
        return diagram.ToString();
    }

    internal static string OutcomeText(ProbabilityOutcome outcome) => outcome switch
    {
        ProbabilityOutcome.Settled => "оплата",
        ProbabilityOutcome.Refunded => "возврат",
        _ => "блокировка"
    };
}

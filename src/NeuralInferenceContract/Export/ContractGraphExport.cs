using System.Globalization;
using System.Text;
using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Export;

internal static class ContractGraphExport
{
    public static string Mermaid()
    {
        var (states, edges) = Build();
        var diagram = new StringBuilder("stateDiagram-v2\n    [*] --> s0\n");
        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            diagram.Append(CultureInfo.InvariantCulture, $"    state \"{state.Stage} · {StateData(state)}\" as s{index}\n");
        }

        foreach (var (from, to, label) in edges)
        {
            var input = from == to ? "Wait, прочие входы" : label;
            diagram.Append(CultureInfo.InvariantCulture, $"    s{from} --> s{to}: {input}\n");
        }

        return diagram.ToString();
    }

    public static string Dot()
    {
        var (states, edges) = Build();
        var diagram = new StringBuilder("""
            digraph Contract {
                graph [rankdir=TB, fontname="Arial", pad=0.2, nodesep=0.3, ranksep=0.55, bgcolor="#ffffff", labelloc=b, fontcolor="#475569", fontsize=12, label="Петля Wait* включает Wait и все остальные входы без отдельной стрелки."];
                node [shape=box, style="rounded,filled", color="#64748b", fillcolor="#f1f5f9", fontcolor="#0f172a", fontname="Arial", fontsize=13, margin="0.14,0.10"];
                edge [fontname="Arial", fontsize=11, fontcolor="#334155", color="#64748b", arrowsize=0.7];
                start [shape=point, color="#0f172a", fillcolor="#0f172a", label="", width=0.12];
                start -> s0;
            """).Append('\n');

        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var colors = state.Stage switch
            {
                ContractStage.Settled => ", fillcolor=\"#dcfce7\", color=\"#15803d\"",
                ContractStage.Refunded => ", fillcolor=\"#dbeafe\", color=\"#1d4ed8\"",
                _ when state.DeadlineReached => ", fillcolor=\"#fef3c7\", color=\"#a16207\"",
                _ => ""
            };
            diagram.Append(CultureInfo.InvariantCulture,
                $"    s{index} [label=\"{state.Stage}\\n{StateData(state)}\"{colors}];\n");
        }

        foreach (var (from, to, label) in edges)
        {
            var style = from == to ? "label=\"Wait*\", color=\"#94a3b8\", fontcolor=\"#475569\"" : $"label=\"{label}\"";
            diagram.Append(CultureInfo.InvariantCulture, $"    s{from} -> s{to} [{style}];\n");
        }

        return diagram.Append("}\n").ToString();
    }

    private static string StateData(ContractState state)
    {
        var units = state.Receipt?.UsedTokens.ToString(CultureInfo.InvariantCulture) ?? "∅";
        var deadline = state.DeadlineReached ? 1 : 0;
        return FormattableString.Invariant($"d={deadline} · r={units} · ({state.Escrow},{state.ProviderPaid},{state.UserRefunded})");
    }

    private static (List<ContractState> States, List<(int From, int To, string Label)> Edges) Build()
    {
        var terms = new ContractTerms(pricePerToken: 1, maxTokens: 2);
        (string Label, ContractEvent Event)[] alphabet =
        [
            ("Reserve", new(EventKind.Reserve, ActorRole.User, Amount: terms.Reservation)),
            ("Accept", new(EventKind.Accept, ActorRole.Provider)),
            ("PublishReceipt(1)", new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: new Receipt(1, 1))),
            ("PublishReceipt(2)", new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: new Receipt(2, 2))),
            ("Confirm", new(EventKind.Confirm, ActorRole.User)),
            ("OpenDispute", new(EventKind.OpenDispute, ActorRole.User)),
            ("ResolvePay", new(EventKind.ResolvePay, ActorRole.Arbiter)),
            ("ResolveRefund", new(EventKind.ResolveRefund, ActorRole.Arbiter)),
            ("ClaimTimeout", new(EventKind.ClaimTimeout, ActorRole.Observer)),
            ("DeadlineReached", new(EventKind.DeadlineReached, ActorRole.Clock)),
            ("Wait", new(EventKind.Wait, ActorRole.Observer)),
            ("Invalid", new(EventKind.Wait, ActorRole.None))
        ];

        var initial = ContractState.Create(terms);
        var states = new List<ContractState> { initial };
        var indices = new Dictionary<ContractState, int> { [initial] = 0 };
        var edges = new List<(int From, int To, string Label)>();
        for (var from = 0; from < states.Count; from++)
        {
            var state = states[from];
            foreach (var (label, input) in alphabet)
            {
                var next = ContractModel.Apply(state, input).State;
                if (next == state)
                {
                    continue;
                }

                if (!indices.TryGetValue(next, out var to))
                {
                    to = states.Count;
                    indices.Add(next, to);
                    states.Add(next);
                }

                edges.Add((from, to, label));
            }

            edges.Add((from, from, "Wait"));
        }

        return (states, edges);
    }
}

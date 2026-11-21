using System.Text.RegularExpressions;

namespace NeuralInferenceContract.Tests;

public sealed class ContractGraphTests
{
    [Fact(DisplayName = "Граф контракта содержит 18 полных состояний и 5 поглощающих состояний расчёта")]
    public void Contract_graph_contains_reachable_states_and_only_terminal_sinks()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, ConsoleApplication.Run(["--contract-graph"], output, error));
        Assert.Empty(error.ToString());
        var diagram = output.ToString();

        var labels = Regex.Matches(diagram, """state "([^"]+)" as (s\d+)""")
            .ToDictionary(match => match.Groups[2].Value, match => match.Groups[1].Value);
        Assert.Equal(18, labels.Count);
        Assert.Equal(18, labels.Values.Distinct().Count());

        var edges = Regex.Matches(diagram, """(s\d+) --> (s\d+): ([^\n]+)""")
            .Select(match => (From: match.Groups[1].Value, To: match.Groups[2].Value, Input: match.Groups[3].Value))
            .ToArray();
        foreach (var (from, to, _) in edges)
        {
            Assert.Contains(from, labels.Keys);
            Assert.Contains(to, labels.Keys);
        }
        var changes = edges.Where(edge => edge.From != edge.To).ToArray();
        Assert.Equal(24, changes.Length);
        Assert.Equal(42, edges.Length);

        var reached = new List<string> { "s0" };
        for (var index = 0; index < reached.Count; index++)
        {
            foreach (var edge in changes.Where(edge => edge.From == reached[index]))
            {
                if (!reached.Contains(edge.To))
                {
                    reached.Add(edge.To);
                }
            }
        }

        Assert.Equal(labels.Count, reached.Count);
        foreach (var (id, label) in labels)
        {
            Assert.Contains((id, id, "Wait, прочие входы"), edges);
            var isFinal = label.StartsWith("Settled", StringComparison.Ordinal) ||
                label.StartsWith("Refunded", StringComparison.Ordinal);
            Assert.Equal(isFinal, changes.All(edge => edge.From != id));
        }
        Assert.Equal(5, reached.Count(id => changes.All(edge => edge.From != id)));

        var before = Assert.Single(labels, state => state.Value == "Processing · d=0 · r=∅ · (2,0,0)").Key;
        var after = Assert.Single(labels, state => state.Value == "Processing · d=1 · r=∅ · (2,0,0)").Key;
        var refund = Assert.Single(labels, state => state.Value == "Refunded · d=0 · r=∅ · (0,0,2)").Key;
        Assert.Contains((before, after, "DeadlineReached"), edges);
        Assert.DoesNotContain(changes, edge => edge.From == before && edge.Input == "ClaimTimeout");
        Assert.Contains((after, refund, "ClaimTimeout"), edges);
    }
}

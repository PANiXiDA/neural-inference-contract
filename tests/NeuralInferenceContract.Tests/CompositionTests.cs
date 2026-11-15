using NeuralInferenceContract.Analysis;
using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Model;
using NeuralInferenceContract.Scenarios;

namespace NeuralInferenceContract.Tests;

public sealed class CompositionTests
{
    [Theory(DisplayName = "Возврат и отсутствие инициатора приводят к разным замкнутым компонентам")]
    [InlineData("provider-no-result", 5, 4, ContractStage.Refunded, 0, false)]
    [InlineData("no-initiator", 4, 3, ContractStage.Processing, 2, true)]
    public void Timeout_scenarios_have_different_closed_components(
        string scenarioName, int states, int entry, ContractStage stage, long escrow, bool critical)
    {
        var graph = BuildScenario(scenarioName);

        Assert.Equal(states, graph.States.Count);
        Assert.Equal(states, graph.Edges.Count);
        Assert.Equal(entry, graph.CycleStart);
        Assert.Equal(1, graph.CycleLength);
        Assert.Equal(critical, graph.IsCritical);
        Assert.Equal(stage, graph.States[entry].Contract.Stage);
        Assert.Equal(escrow, graph.States[entry].Contract.Escrow);
        Assert.Equal(critical ? 0 : 2, graph.States[entry].Contract.UserRefunded);

        var loop = graph.Edges[^1];
        Assert.Equal(entry, loop.From);
        Assert.Equal(entry, loop.To);
        Assert.Equal(EventKind.Wait, loop.Event.Kind);
        Assert.Equal(critical ? 0 : 1, loop.Result.Output);
    }

    [Fact(DisplayName = "В сплетении повтором считается вся пара, включая состояние контракта")]
    public void Cycle_detection_uses_the_entire_pair()
    {
        var environment = new EnvironmentAutomaton([
            new(new(EventKind.Reserve, ActorRole.User, Amount: 2), 1),
            new(new(EventKind.Accept, ActorRole.Provider), 2),
            new(new(EventKind.DeadlineReached, ActorRole.Clock), 3),
            new(new(EventKind.Wait, ActorRole.Observer), 2)
        ]);

        var graph = CompositionGraph.Build(new ContractComposition(environment, new ContractTerms()));

        Assert.Equal(3, graph.CycleStart);
        Assert.Equal(2, graph.CycleLength);
        Assert.Equal(graph.States[2].EnvironmentState, graph.States[4].EnvironmentState);
        Assert.False(graph.States[2].Contract.DeadlineReached);
        Assert.True(graph.States[4].Contract.DeadlineReached);
        Assert.Equal(3, graph.Edges[^1].To);
        Assert.True(graph.IsCritical);
    }

    [Fact(DisplayName = "Среда делает следующий шаг даже после отклонения события контрактом")]
    public void Rejected_event_does_not_stop_environment()
    {
        var environment = EnvironmentAutomaton.FromEvents([
            new(EventKind.Accept, ActorRole.Provider),
            new(EventKind.Reserve, ActorRole.User, Amount: 2)
        ]);

        var graph = CompositionGraph.Build(new ContractComposition(environment, new ContractTerms()));

        Assert.False(graph.Edges[0].Result.Accepted);
        Assert.Same(graph.States[0].Contract, graph.States[1].Contract);
        Assert.Equal(1, graph.States[1].EnvironmentState);
        Assert.True(graph.Edges[1].Result.Accepted);
        Assert.Equal(ContractStage.Reserved, graph.States[2].Contract.Stage);
    }

    [Fact(DisplayName = "Пустой сценарий замыкается в ожидании без заблокированного резерва")]
    public void Empty_scenario_has_a_noncritical_wait_loop()
    {
        var environment = EnvironmentAutomaton.FromEvents([]);
        var graph = CompositionGraph.Build(new ContractComposition(environment, new ContractTerms()));

        Assert.Equal(0, graph.CycleStart);
        Assert.False(graph.IsCritical);
        Assert.Equal(ContractStage.Idle, Assert.Single(graph.States).Contract.Stage);
        Assert.Equal(EventKind.Wait, Assert.Single(graph.Edges).Event.Kind);
    }

    [Fact(DisplayName = "Среда отклоняет переход в несуществующее состояние")]
    public void Environment_requires_existing_destinations()
    {
        Assert.Throws<ArgumentException>(() => new EnvironmentAutomaton([
            new(new(EventKind.Wait, ActorRole.Observer), 1)
        ]));
    }

    [Fact(DisplayName = "Геометрический образ кодирует выходы и продолжает цикл на длинных префиксах")]
    public void Geometric_image_distinguishes_outputs_and_follows_cycles()
    {
        var refunded = GeometricImage.Build(BuildScenario("provider-no-result"), 8);
        var blocked = GeometricImage.Build(BuildScenario("no-initiator"), 8);

        Assert.Equal(8, refunded.Count);
        Assert.Equal(new GeometricPoint(1, 1, 1, 1, 0, false, false), refunded[0]);
        Assert.Equal(1.875, refunded[3].X);
        Assert.Equal(refunded[3].X, blocked[3].X);
        Assert.Equal(41d / 27, refunded[3].Y, precision: 12);
        Assert.Equal(40d / 27, blocked[3].Y, precision: 12);
        Assert.False(refunded[2].InClosedComponent);
        Assert.True(blocked[2].IsCritical);
        Assert.Equal(1, refunded[^1].Output);
        Assert.True(refunded[^1].InClosedComponent);
        Assert.False(refunded[^1].IsCritical);
        Assert.Equal(4, refunded[^1].StateIndex);
        Assert.Equal(0, blocked[^1].Output);
        Assert.True(blocked[^1].IsCritical);
        Assert.Equal(3, blocked[^1].StateIndex);
    }

    private static CompositionGraph BuildScenario(string name)
    {
        var terms = new ContractTerms();
        var scenario = Assert.Single(ScenarioCatalog.Create(terms), item => item.Name == name);
        var environment = EnvironmentAutomaton.FromEvents(scenario.Events);
        return CompositionGraph.Build(new ContractComposition(environment, terms));
    }
}

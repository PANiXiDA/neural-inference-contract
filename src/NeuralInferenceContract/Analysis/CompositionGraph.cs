using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Analysis;

public sealed record CompositionEdge(int From, int To, ContractEvent Event, TransitionResult Result);

public sealed class CompositionGraph
{
    private CompositionGraph(List<CompositionState> states, List<CompositionEdge> edges, int cycleStart)
    {
        States = states.AsReadOnly();
        Edges = edges.AsReadOnly();
        CycleStart = cycleStart;
        IsCritical = states.Skip(cycleStart).All(state => !state.Contract.IsFinal && state.Contract.Escrow > 0);
    }

    public IReadOnlyList<CompositionState> States { get; }
    public IReadOnlyList<CompositionEdge> Edges { get; }
    public int CycleStart { get; }
    public int CycleLength => States.Count - CycleStart;
    public bool IsCritical { get; }

    public static CompositionGraph Build(ContractComposition composition)
    {
        var states = new List<CompositionState> { composition.InitialState };
        var indices = new Dictionary<CompositionState, int> { [composition.InitialState] = 0 };
        var edges = new List<CompositionEdge>();

        while (true)
        {
            var from = states.Count - 1;
            var step = composition.Step(states[from]);
            if (indices.TryGetValue(step.NextState, out var to))
            {
                edges.Add(new CompositionEdge(from, to, step.Event, step.Result));
                return new CompositionGraph(states, edges, to);
            }

            to = states.Count;
            indices.Add(step.NextState, to);
            states.Add(step.NextState);
            edges.Add(new CompositionEdge(from, to, step.Event, step.Result));
        }
    }
}

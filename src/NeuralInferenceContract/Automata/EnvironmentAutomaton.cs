using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Automata;

public sealed record EnvironmentTransition(ContractEvent Output, int NextState);

public sealed class EnvironmentAutomaton
{
    private static readonly ContractEvent Wait = new(EventKind.Wait, ActorRole.Observer);
    private readonly EnvironmentTransition[] transitions;

    public EnvironmentAutomaton(IEnumerable<EnvironmentTransition> transitions)
    {
        this.transitions = transitions.ToArray();
        if (this.transitions.Length == 0 ||
            this.transitions.Any(step => step?.Output is null || step.NextState < 0 || step.NextState >= this.transitions.Length))
        {
            throw new ArgumentException("Для каждого состояния нужны событие и существующее следующее состояние.", nameof(transitions));
        }
    }

    public int InitialState => 0;

    public EnvironmentTransition GetTransition(int state) => transitions[state];

    public static EnvironmentAutomaton FromEvents(IReadOnlyList<ContractEvent> events)
    {
        var prefixLength = events.Count;
        while (prefixLength > 0 && events[prefixLength - 1] == Wait)
        {
            prefixLength--;
        }

        var transitions = new EnvironmentTransition[prefixLength + 1];
        for (var index = 0; index < prefixLength; index++)
        {
            transitions[index] = new EnvironmentTransition(events[index], index + 1);
        }

        transitions[prefixLength] = new EnvironmentTransition(Wait, prefixLength);
        return new EnvironmentAutomaton(transitions);
    }
}

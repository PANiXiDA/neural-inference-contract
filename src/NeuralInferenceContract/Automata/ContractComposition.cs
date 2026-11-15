using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Automata;

public sealed record CompositionState(int EnvironmentState, ContractState Contract);

public sealed record CompositionStep(CompositionState NextState, ContractEvent Event, TransitionResult Result);

public sealed class ContractComposition(EnvironmentAutomaton environment, ContractTerms terms)
{
    public CompositionState InitialState { get; } = new(environment.InitialState, ContractState.Create(terms));

    public CompositionStep Step(CompositionState state)
    {
        var transition = environment.GetTransition(state.EnvironmentState);
        var result = ContractModel.Apply(state.Contract, transition.Output);
        var next = new CompositionState(transition.NextState, result.State);
        return new CompositionStep(next, transition.Output, result);
    }
}

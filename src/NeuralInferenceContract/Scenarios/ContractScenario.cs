using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Scenarios;

public sealed record ContractScenario(string Name, string Description, IReadOnlyList<ContractEvent> Events);

public sealed record ScenarioStep(int Number, ContractEvent Event, ContractState PreviousState, TransitionResult Result);

public sealed record ScenarioRun(ContractState InitialState, IReadOnlyList<ScenarioStep> Steps)
{
    public ContractState FinalState => Steps.Count == 0 ? InitialState : Steps[^1].Result.State;
}

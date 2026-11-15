using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Scenarios;

public static class ScenarioRunner
{
    public static ScenarioRun Run(ContractScenario scenario, ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var environment = EnvironmentAutomaton.FromEvents(scenario.Events);
        var composition = new ContractComposition(environment, terms);
        var state = composition.InitialState;
        var steps = new List<ScenarioStep>();

        for (var index = 0; index < scenario.Events.Count; index++)
        {
            var step = composition.Step(state);
            steps.Add(new ScenarioStep(index + 1, step.Event, state.Contract, step.Result));
            state = step.NextState;
        }

        return new ScenarioRun(composition.InitialState.Contract, steps.AsReadOnly());
    }
}

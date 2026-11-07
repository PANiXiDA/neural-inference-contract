using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Scenarios;

public static class ScenarioRunner
{
    public static ScenarioRun Run(ContractScenario scenario, ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var initial = ContractState.Create(terms);
        var state = initial;
        var steps = new List<ScenarioStep>();

        foreach (var input in scenario.Events)
        {
            var result = ContractModel.Apply(state, input);
            steps.Add(new ScenarioStep(steps.Count + 1, input, state, result));
            state = result.State;
        }

        return new ScenarioRun(initial, steps.AsReadOnly());
    }
}

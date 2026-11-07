using NeuralInferenceContract.Model;
using NeuralInferenceContract.Scenarios;

namespace NeuralInferenceContract.Tests;

public sealed class ScenarioTests
{
    [Theory]
    [InlineData("success", ContractStage.Settled, 0, 1, 1)]
    [InlineData("full-cost", ContractStage.Settled, 0, 2, 0)]
    [InlineData("provider-no-accept", ContractStage.Refunded, 0, 0, 2)]
    [InlineData("provider-no-result", ContractStage.Refunded, 0, 0, 2)]
    [InlineData("user-silent", ContractStage.Settled, 0, 1, 1)]
    [InlineData("dispute-pay", ContractStage.Settled, 0, 1, 1)]
    [InlineData("dispute-refund", ContractStage.Refunded, 0, 0, 2)]
    [InlineData("arbiter-offline", ContractStage.Refunded, 0, 0, 2)]
    [InlineData("no-initiator", ContractStage.Processing, 2, 0, 0)]
    [InlineData("replay-and-invalid", ContractStage.Settled, 0, 1, 1)]
    public void Scenario_has_expected_distribution(
        string name, ContractStage stage, long escrow, long paid, long refunded)
    {
        var terms = new ContractTerms();
        var scenario = Assert.Single(ScenarioCatalog.Create(terms), scenario => scenario.Name == name);
        var run = ScenarioRunner.Run(scenario, terms);

        Assert.Equal(stage, run.FinalState.Stage);
        Assert.Equal(escrow, run.FinalState.Escrow);
        Assert.Equal(paid, run.FinalState.ProviderPaid);
        Assert.Equal(refunded, run.FinalState.UserRefunded);
    }

    [Fact]
    public void Successful_scenario_records_each_state_and_moore_mealy_output()
    {
        var terms = new ContractTerms();
        var scenario = Assert.Single(ScenarioCatalog.Create(terms), scenario => scenario.Name == "success");
        var run = ScenarioRunner.Run(scenario, terms);

        Assert.Equal(new[] { ContractStage.Reserved, ContractStage.Processing,
            ContractStage.AwaitingConfirmation, ContractStage.Settled },
            run.Steps.Select(step => step.Result.State.Stage));
        Assert.Equal(new[] { 0, 0, 0, 1 }, run.Steps.Select(step => step.Result.Output));
        Assert.Equal(ContractStage.Idle, run.InitialState.Stage);
        Assert.All(run.Steps, step => Assert.True(step.Result.Accepted));
    }

    [Fact]
    public void Missing_initiator_keeps_funds_reserved_after_deadline()
    {
        var terms = new ContractTerms();
        var scenario = Assert.Single(ScenarioCatalog.Create(terms), scenario => scenario.Name == "no-initiator");
        var run = ScenarioRunner.Run(scenario, terms);
        Assert.True(run.FinalState.DeadlineReached);
        Assert.False(run.FinalState.IsFinal);
        Assert.Equal(run.Steps[^2].Result.State, run.FinalState);
        Assert.Equal(0, run.FinalState.Output);
    }
}

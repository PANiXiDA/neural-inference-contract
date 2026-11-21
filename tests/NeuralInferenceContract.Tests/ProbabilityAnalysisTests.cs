using NeuralInferenceContract.Analysis;
using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Tests;

public sealed class ProbabilityAnalysisTests
{
    [Fact(DisplayName = "Равновероятные решения задают полное префиксное разбиение двоичных входов")]
    public void Fair_decisions_partition_all_binary_inputs()
    {
        var analysis = ProbabilityAnalysis.Build(new ContractTerms());

        Assert.Equal(7, analysis.Paths.Count);
        Assert.Equal(1, analysis.TotalProbability);
        foreach (var path in analysis.Paths)
        {
            Assert.Equal(1m / (1 << path.Environment.Code.Length), path.Environment.Probability);
            Assert.All(analysis.Paths.Where(other => other != path), other =>
                Assert.False(other.Environment.Code.StartsWith(path.Environment.Code, StringComparison.Ordinal)));
        }

        for (var value = 0; value < 32; value++)
        {
            var bits = Convert.ToString(value, 2).PadLeft(5, '0');
            Assert.Single(analysis.Paths, path => bits.StartsWith(path.Environment.Code, StringComparison.Ordinal));
        }
    }

    [Theory(DisplayName = "Листья вероятностной среды достигают ожидаемых замкнутых компонент")]
    [InlineData("00", ContractStage.Processing, 3, ProbabilityOutcome.Locked)]
    [InlineData("01", ContractStage.Refunded, 4, ProbabilityOutcome.Refunded)]
    [InlineData("100", ContractStage.AwaitingConfirmation, 4, ProbabilityOutcome.Locked)]
    [InlineData("10100", ContractStage.Disputed, 6, ProbabilityOutcome.Locked)]
    [InlineData("10101", ContractStage.Refunded, 7, ProbabilityOutcome.Refunded)]
    [InlineData("1011", ContractStage.Settled, 6, ProbabilityOutcome.Settled)]
    [InlineData("11", ContractStage.Settled, 4, ProbabilityOutcome.Settled)]
    public void Leaves_reach_expected_closed_components(string code, ContractStage stage, int entry, ProbabilityOutcome outcome)
    {
        var analysis = ProbabilityAnalysis.Build(new ContractTerms());
        var path = Assert.Single(analysis.Paths, item => item.Environment.Code == code);

        Assert.Equal(stage, path.EntryState.Stage);
        Assert.Equal(entry, path.Graph.CycleStart);
        Assert.Equal(outcome, path.Outcome);
        Assert.Equal(1, path.Graph.CycleLength);
        Assert.All(path.Graph.Edges, edge => Assert.True(edge.Result.Accepted));
        Assert.Equal(EventKind.Wait, path.Graph.Edges[^1].Event.Kind);
        Assert.Equal(path.Graph.CycleStart, path.Graph.Edges[^1].To);
        Assert.Equal(outcome == ProbabilityOutcome.Locked ? 2 : 0, path.EntryState.Escrow);
        Assert.All(path.Graph.States.Take(entry), state => Assert.False(state.Contract.IsFinal));
    }

    [Fact(DisplayName = "Базовый опыт даёт вероятности оплаты 5/16, возврата 9/32 и блокировки 13/32")]
    public void Default_outcomes_and_expected_payments_match_calculation()
    {
        var analysis = ProbabilityAnalysis.Build(new ContractTerms());

        Assert.Equal(5m / 16, analysis.SettledProbability);
        Assert.Equal(9m / 32, analysis.RefundedProbability);
        Assert.Equal(13m / 32, analysis.LockedProbability);
        Assert.Equal(13m / 16, analysis.ExpectedEscrow);
        Assert.Equal(5m / 16, analysis.ExpectedProviderPaid);
        Assert.Equal(7m / 8, analysis.ExpectedUserRefunded);
    }

    [Theory(DisplayName = "Доступность инициатора определяет вероятность блокировки средств")]
    [InlineData(0, 0.25, 0, 0.75)]
    [InlineData(0.5, 0.3125, 0.28125, 0.40625)]
    [InlineData(1, 0.375, 0.625, 0)]
    public void Initiator_availability_changes_outcome_probabilities(double initiator, double settled, double refunded, double locked)
    {
        var analysis = ProbabilityAnalysis.Build(new ContractTerms(),
            new EnvironmentProbabilities(timeoutInitiator: (decimal)initiator));

        Assert.Equal((decimal)settled, analysis.SettledProbability);
        Assert.Equal((decimal)refunded, analysis.RefundedProbability);
        Assert.Equal((decimal)locked, analysis.LockedProbability);
        Assert.Equal(1, analysis.TotalProbability);
    }

    [Fact(DisplayName = "Неравномерные вероятности перемножаются вдоль пути и сохраняют денежный инвариант")]
    public void Nonuniform_decisions_match_independent_formulas_and_conserve_reservation()
    {
        var terms = new ContractTerms(pricePerToken: 3, maxTokens: 4);
        var probabilities = new EnvironmentProbabilities(0.8m, 0.6m, 0.75m, 0.9m);
        var analysis = ProbabilityAnalysis.Build(terms, probabilities);

        Assert.Equal(0.696m, analysis.SettledProbability);
        Assert.Equal(0.2448m, analysis.RefundedProbability);
        Assert.Equal(0.0592m, analysis.LockedProbability);
        Assert.Equal(1, analysis.TotalProbability);
        Assert.Equal(terms.Reservation, analysis.ExpectedEscrow + analysis.ExpectedProviderPaid + analysis.ExpectedUserRefunded);
        Assert.Equal(terms.PricePerToken * analysis.SettledProbability, analysis.ExpectedProviderPaid);
        Assert.All(analysis.Paths, path => Assert.Equal(terms.Reservation,
            path.EntryState.Escrow + path.EntryState.ProviderPaid + path.EntryState.UserRefunded));
    }

    [Theory(DisplayName = "Вероятности ноль и один оставляют единственный возможный путь без потери массы")]
    [InlineData(0, "00", ProbabilityOutcome.Locked)]
    [InlineData(1, "11", ProbabilityOutcome.Settled)]
    public void Deterministic_limits_keep_one_positive_probability_path(int probability, string code, ProbabilityOutcome outcome)
    {
        var analysis = ProbabilityAnalysis.Build(new ContractTerms(),
            new EnvironmentProbabilities(probability, probability, probability, probability));
        var path = Assert.Single(analysis.Paths, item => item.Environment.Probability > 0);

        Assert.Equal(code, path.Environment.Code);
        Assert.Equal(outcome, path.Outcome);
        Assert.Equal(1, path.Environment.Probability);
        Assert.Equal(1, analysis.TotalProbability);
    }

    [Theory(DisplayName = "Вероятности вне отрезка от нуля до единицы отклоняются для всех участников")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Invalid_probabilities_are_rejected(double value)
    {
        var probability = (decimal)value;

        Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentProbabilities(providerResult: probability));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentProbabilities(userConfirmation: probability));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentProbabilities(arbiterDecision: probability));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentProbabilities(timeoutInitiator: probability));
    }
}

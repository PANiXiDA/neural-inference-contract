using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Analysis;

public enum ProbabilityOutcome
{
    Settled,
    Refunded,
    Locked
}

public sealed record ProbabilityPath(WeightedEnvironmentPath Environment, CompositionGraph Graph)
{
    public ContractState EntryState => Graph.States[Graph.CycleStart].Contract;
    public ProbabilityOutcome Outcome => EntryState.Stage switch
    {
        ContractStage.Settled => ProbabilityOutcome.Settled,
        ContractStage.Refunded => ProbabilityOutcome.Refunded,
        _ when Graph.IsCritical => ProbabilityOutcome.Locked,
        _ => throw new InvalidOperationException("Компонента не соответствует исходам вероятностного эксперимента.")
    };
}

public sealed class ProbabilityAnalysis
{
    private ProbabilityAnalysis(ProbabilisticEnvironment environment, ContractTerms terms)
    {
        Decisions = environment.Decisions;
        Paths = Array.AsReadOnly(environment.Paths.Select(path => new ProbabilityPath(path,
            CompositionGraph.Build(new ContractComposition(EnvironmentAutomaton.FromEvents(path.Events), terms)))).ToArray());
    }

    public IReadOnlyList<ProbabilityDecision> Decisions { get; }
    public IReadOnlyList<ProbabilityPath> Paths { get; }
    public decimal TotalProbability => Paths.Sum(path => path.Environment.Probability);
    public decimal SettledProbability => ProbabilityOf(ProbabilityOutcome.Settled);
    public decimal RefundedProbability => ProbabilityOf(ProbabilityOutcome.Refunded);
    public decimal LockedProbability => ProbabilityOf(ProbabilityOutcome.Locked);
    public decimal ExpectedEscrow => Paths.Sum(path => path.Environment.Probability * path.EntryState.Escrow);
    public decimal ExpectedProviderPaid => Paths.Sum(path => path.Environment.Probability * path.EntryState.ProviderPaid);
    public decimal ExpectedUserRefunded => Paths.Sum(path => path.Environment.Probability * path.EntryState.UserRefunded);

    public static ProbabilityAnalysis Build(ContractTerms terms, EnvironmentProbabilities? probabilities = null) =>
        new(new ProbabilisticEnvironment(terms, probabilities ?? new EnvironmentProbabilities()), terms);

    private decimal ProbabilityOf(ProbabilityOutcome outcome) =>
        Paths.Where(path => path.Outcome == outcome).Sum(path => path.Environment.Probability);
}

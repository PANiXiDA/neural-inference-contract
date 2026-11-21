using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Automata;

public sealed record ProbabilityDecision(string Code, string Description, decimal OneProbability);

public sealed record WeightedEnvironmentPath(string Code, decimal Probability, IReadOnlyList<ContractEvent> Events);

public sealed class ProbabilisticEnvironment
{
    private sealed record Branch(ContractEvent[] Events, Choice? Next = null);
    private sealed record Choice(string Description, decimal OneProbability, Branch Zero, Branch One);

    public ProbabilisticEnvironment(ContractTerms terms, EnvironmentProbabilities probabilities)
    {
        ContractEvent deadline = new(EventKind.DeadlineReached, ActorRole.Clock);
        ContractEvent claim = new(EventKind.ClaimTimeout, ActorRole.Observer);

        var fallback = new Choice("Инициатор возврата", probabilities.TimeoutInitiator,
            new([]), new([claim]));
        var arbitration = new Choice("Решение арбитра", probabilities.ArbiterDecision,
            new([deadline], fallback), new([new(EventKind.ResolvePay, ActorRole.Arbiter)]));
        var dispute = new Choice("Инициатор спора", probabilities.TimeoutInitiator,
            new([]), new([claim], arbitration));
        var confirmation = new Choice("Подтверждение", probabilities.UserConfirmation,
            new([deadline], dispute), new([new(EventKind.Confirm, ActorRole.User)]));
        var refund = new Choice("Инициатор возврата", probabilities.TimeoutInitiator,
            new([]), new([claim]));
        var result = new Choice("Результат провайдера", probabilities.ProviderResult,
            new([deadline], refund),
            new([new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: new Receipt(1, terms.PricePerToken))], confirmation));

        var paths = new List<WeightedEnvironmentPath>();
        var decisions = new List<ProbabilityDecision>();
        Visit(result, "", 1, [
            new(EventKind.Reserve, ActorRole.User, Amount: terms.Reservation),
            new(EventKind.Accept, ActorRole.Provider)
        ], paths, decisions);
        Paths = paths.AsReadOnly();
        Decisions = decisions.AsReadOnly();
    }

    public IReadOnlyList<WeightedEnvironmentPath> Paths { get; }
    public IReadOnlyList<ProbabilityDecision> Decisions { get; }

    private static void Visit(Choice choice, string code, decimal probability, ContractEvent[] events,
        List<WeightedEnvironmentPath> paths, List<ProbabilityDecision> decisions)
    {
        decisions.Add(new ProbabilityDecision(code, choice.Description, choice.OneProbability));
        Follow(choice.Zero, code + "0", probability * (1 - choice.OneProbability), events, paths, decisions);
        Follow(choice.One, code + "1", probability * choice.OneProbability, events, paths, decisions);
    }

    private static void Follow(Branch branch, string code, decimal probability, ContractEvent[] events,
        List<WeightedEnvironmentPath> paths, List<ProbabilityDecision> decisions)
    {
        ContractEvent[] next = [.. events, .. branch.Events];
        if (branch.Next is null)
        {
            paths.Add(new WeightedEnvironmentPath(code, probability, Array.AsReadOnly(next)));
        }
        else
        {
            Visit(branch.Next, code, probability, next, paths, decisions);
        }
    }
}

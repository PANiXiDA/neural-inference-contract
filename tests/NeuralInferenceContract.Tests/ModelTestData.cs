using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Tests;

internal static class ModelTestData
{
    public static ContractTerms Terms { get; } = new();
    public static Receipt ValidReceipt { get; } = new(1, 1);

    public static ContractState AtStage(ContractStage stage)
    {
        ContractEvent reserve = new(EventKind.Reserve, ActorRole.User, Amount: 2);
        ContractEvent accept = new(EventKind.Accept, ActorRole.Provider);
        ContractEvent publish = new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: ValidReceipt);
        ContractEvent[] prefix = stage switch
        {
            ContractStage.Idle => [],
            ContractStage.Reserved => [reserve],
            ContractStage.Processing => [reserve, accept],
            ContractStage.AwaitingConfirmation => [reserve, accept, publish],
            ContractStage.Disputed => [reserve, accept, publish, new(EventKind.OpenDispute, ActorRole.User)],
            ContractStage.Settled => [reserve, accept, publish, new(EventKind.Confirm, ActorRole.User)],
            ContractStage.Refunded => [reserve, new(EventKind.DeadlineReached, ActorRole.Clock),
                new(EventKind.ClaimTimeout, ActorRole.Observer)],
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

        return ApplyAccepted(ContractState.Create(Terms), prefix);
    }

    public static ContractState ApplyAccepted(ContractState state, params ContractEvent[] events)
    {
        foreach (var input in events)
        {
            var result = ContractModel.Apply(state, input);
            Assert.True(result.Accepted, $"{input.Kind}: {result.Description}");
            state = result.State;
        }

        return state;
    }

    public static IReadOnlyList<ContractEvent> FiniteAlphabet()
    {
        var events = new List<ContractEvent>();
        foreach (var kind in Enum.GetValues<EventKind>())
        {
            foreach (var actor in Enum.GetValues<ActorRole>())
            {
                var input = new ContractEvent(kind, actor, Amount: 2, Receipt: ValidReceipt);
                events.Add(input);
                events.Add(input with { RequestMatches = false });
                events.Add(input with { ReceiptMatches = false });
            }
        }

        foreach (var amount in new long?[] { null, -1, 0, 1, 3, long.MaxValue })
        {
            events.Add(new(EventKind.Reserve, ActorRole.User, Amount: amount));
        }

        foreach (var receipt in InvalidReceipts().Concat(new[] { new Receipt(2, 2) }))
        {
            events.Add(new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: receipt));
        }

        events.Add(new((EventKind)(-1), ActorRole.User));
        events.Add(new(EventKind.Reserve, (ActorRole)(-1), Amount: 2));
        return events.AsReadOnly();
    }

    public static IEnumerable<Receipt?> InvalidReceipts()
    {
        yield return null;
        yield return ValidReceipt with { UsedTokens = 0 };
        yield return ValidReceipt with { UsedTokens = -1 };
        yield return ValidReceipt with { UsedTokens = 3 };
        yield return ValidReceipt with { UsedTokens = long.MaxValue };
        yield return ValidReceipt with { Cost = 0 };
        yield return ValidReceipt with { Cost = -1 };
        yield return ValidReceipt with { Cost = 2 };
        yield return ValidReceipt with { Cost = long.MaxValue };
        yield return ValidReceipt with { RequestMatches = false };
        yield return ValidReceipt with { ModelMatches = false };
        yield return ValidReceipt with { TermsMatch = false };
        yield return ValidReceipt with { InputHashMatches = false };
        yield return ValidReceipt with { HasResponseHash = false };
        yield return ValidReceipt with { SignatureValid = false };
    }
}

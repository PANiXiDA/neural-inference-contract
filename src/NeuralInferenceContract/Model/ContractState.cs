namespace NeuralInferenceContract.Model;

public enum ContractStage
{
    Idle,
    Reserved,
    Processing,
    AwaitingConfirmation,
    Disputed,
    Settled,
    Refunded
}

public sealed record ContractState
{
    internal ContractState(
        ContractTerms terms,
        ContractStage stage,
        bool deadlineReached = false,
        Receipt? receipt = null,
        long escrow = 0,
        long providerPaid = 0,
        long userRefunded = 0)
    {
        Terms = terms;
        Stage = stage;
        DeadlineReached = deadlineReached;
        Receipt = receipt;
        Escrow = escrow;
        ProviderPaid = providerPaid;
        UserRefunded = userRefunded;
    }

    public ContractTerms Terms { get; }
    public ContractStage Stage { get; }
    public bool DeadlineReached { get; }
    public Receipt? Receipt { get; }
    public long Escrow { get; }
    public long ProviderPaid { get; }
    public long UserRefunded { get; }
    public bool IsFinal => Stage is ContractStage.Settled or ContractStage.Refunded;
    public int Output => IsFinal ? 1 : 0;

    public static ContractState Create(ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return new ContractState(terms, ContractStage.Idle);
    }
}

namespace NeuralInferenceContract.Model;

public enum ActorRole
{
    None,
    User,
    Provider,
    Arbiter,
    Observer,
    Clock
}

public enum EventKind
{
    Reserve,
    Accept,
    PublishReceipt,
    Confirm,
    OpenDispute,
    ResolvePay,
    ResolveRefund,
    ClaimTimeout,
    DeadlineReached,
    Wait
}

public sealed record ContractEvent(
    EventKind Kind,
    ActorRole Actor,
    bool RequestMatches = true,
    long? Amount = null,
    Receipt? Receipt = null,
    bool ReceiptMatches = true);

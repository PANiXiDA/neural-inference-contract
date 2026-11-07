namespace NeuralInferenceContract.Model;

public sealed record TransitionResult(ContractState State, bool Accepted, string Description)
{
    public int Output => State.Output;
}

namespace NeuralInferenceContract.Model;

public sealed record TransitionResult(ContractState State, bool Accepted, string Description)
{
    // Выход Мили равен выходу Мура состояния после обработки события.
    public int Output => State.Output;
}

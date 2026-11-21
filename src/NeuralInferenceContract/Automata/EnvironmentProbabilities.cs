namespace NeuralInferenceContract.Automata;

public sealed record EnvironmentProbabilities
{
    public EnvironmentProbabilities(
        decimal providerResult = 0.5m,
        decimal userConfirmation = 0.5m,
        decimal arbiterDecision = 0.5m,
        decimal timeoutInitiator = 0.5m)
    {
        Validate(providerResult, nameof(providerResult));
        Validate(userConfirmation, nameof(userConfirmation));
        Validate(arbiterDecision, nameof(arbiterDecision));
        Validate(timeoutInitiator, nameof(timeoutInitiator));

        ProviderResult = providerResult;
        UserConfirmation = userConfirmation;
        ArbiterDecision = arbiterDecision;
        TimeoutInitiator = timeoutInitiator;
    }

    public decimal ProviderResult { get; }
    public decimal UserConfirmation { get; }
    public decimal ArbiterDecision { get; }
    public decimal TimeoutInitiator { get; }

    private static void Validate(decimal value, string name)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, "Вероятность должна принадлежать отрезку [0, 1].");
        }
    }
}

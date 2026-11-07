namespace NeuralInferenceContract.Model;

public sealed record ContractTerms
{
    public ContractTerms(long pricePerToken = 1, long maxTokens = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pricePerToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);

        if (maxTokens > long.MaxValue / pricePerToken)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Стоимость резерва превышает Int64.");
        }

        PricePerToken = pricePerToken;
        MaxTokens = maxTokens;
        Reservation = checked(pricePerToken * maxTokens);
    }

    public long PricePerToken { get; }
    public long MaxTokens { get; }
    public long Reservation { get; }
}

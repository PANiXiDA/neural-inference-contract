namespace NeuralInferenceContract.Model;

// Признаки соответствия заменяют строки, хеши и проверку подписи в конечной модели.
public sealed record Receipt(
    long UsedTokens,
    long Cost,
    bool RequestMatches = true,
    bool ModelMatches = true,
    bool TermsMatch = true,
    bool InputHashMatches = true,
    bool HasResponseHash = true,
    bool SignatureValid = true);

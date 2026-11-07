using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Scenarios;

public static class ScenarioCatalog
{
    public static IReadOnlyList<ContractScenario> Create(ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var tokens = Math.Max(1, terms.MaxTokens / 2);
        var receipt = new Receipt(tokens, checked(tokens * terms.PricePerToken));
        var fullReceipt = new Receipt(terms.MaxTokens, terms.Reservation);
        var reserve = new ContractEvent(EventKind.Reserve, ActorRole.User, Amount: terms.Reservation);
        var accept = new ContractEvent(EventKind.Accept, ActorRole.Provider);
        var publish = new ContractEvent(EventKind.PublishReceipt, ActorRole.Provider, Receipt: receipt);
        var confirm = new ContractEvent(EventKind.Confirm, ActorRole.User);
        var dispute = new ContractEvent(EventKind.OpenDispute, ActorRole.User);
        var pay = new ContractEvent(EventKind.ResolvePay, ActorRole.Arbiter);
        var refund = new ContractEvent(EventKind.ResolveRefund, ActorRole.Arbiter);
        var deadline = new ContractEvent(EventKind.DeadlineReached, ActorRole.Clock);
        var claim = new ContractEvent(EventKind.ClaimTimeout, ActorRole.Observer);
        var wait = new ContractEvent(EventKind.Wait, ActorRole.Observer);

        return Array.AsReadOnly(new[]
        {
            Scenario("success", "Подтверждение ответа и оплата фактического объёма.",
                reserve, accept, publish, confirm),
            Scenario("full-cost", "Использован весь зарезервированный объём.",
                reserve, accept, publish with { Receipt = fullReceipt }, confirm),
            Scenario("provider-no-accept", "Провайдер не принял запрос; возврат по сроку.",
                reserve, deadline, claim),
            Scenario("provider-no-result", "Провайдер принял запрос, но не опубликовал квитанцию.",
                reserve, accept, deadline, claim),
            Scenario("user-silent", "Нет подтверждения; спор по сроку и оплата по вердикту.",
                reserve, accept, publish, deadline, claim, pay),
            Scenario("dispute-pay", "Арбитр разрешает спор в пользу оплаты.",
                reserve, accept, publish, dispute, pay),
            Scenario("dispute-refund", "Арбитр разрешает спор в пользу возврата.",
                reserve, accept, publish, dispute, refund),
            Scenario("arbiter-offline", "Арбитр недоступен; резерв возвращается после срока спора.",
                reserve, accept, publish, dispute, deadline, claim),
            Scenario("no-initiator", "Срок истёк, операция завершения не вызывается.",
                reserve, accept, deadline, wait, wait),
            Scenario("replay-and-invalid", "Неверные роли, квитанции и повторные сообщения.",
                reserve,
                accept with { Actor = ActorRole.User },
                accept,
                publish with { Receipt = receipt with { SignatureValid = false } },
                publish with { Receipt = receipt with { Cost = 0 } },
                publish,
                confirm with { ReceiptMatches = false },
                confirm,
                confirm,
                claim,
                reserve)
        });
    }

    private static ContractScenario Scenario(string name, string description, params ContractEvent[] events) =>
        new(name, description, Array.AsReadOnly(events));
}

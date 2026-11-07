namespace NeuralInferenceContract.Model;

public static class ContractModel
{
    public static TransitionResult Apply(ContractState state, ContractEvent input)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);

        if (!Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.Actor) || input.Actor == ActorRole.None)
        {
            return Reject(state, "Неизвестное событие или роль.");
        }

        if (!input.RequestMatches)
        {
            return Reject(state, "Событие относится к другому запросу.");
        }

        if (input.Kind == EventKind.Wait)
        {
            return Accept(state, "Ожидание.");
        }

        if (state.IsFinal)
        {
            return Reject(state, "Расчёты уже завершены.");
        }

        if (input.Kind == EventKind.DeadlineReached)
        {
            if (input.Actor != ActorRole.Clock || state.Stage == ContractStage.Idle)
            {
                return Reject(state, "Срок активного этапа может изменить только источник времени.");
            }

            return Accept(
                state.DeadlineReached ? state : new ContractState(
                    state.Terms, state.Stage, true, state.Receipt,
                    state.Escrow, state.ProviderPaid, state.UserRefunded),
                "Срок текущего этапа истёк.");
        }

        if (!HasPermission(input))
        {
            return Reject(state, "У отправителя нет права на это действие.");
        }

        if (input.Kind == EventKind.ClaimTimeout)
        {
            return ClaimTimeout(state);
        }

        if (state.DeadlineReached)
        {
            return Reject(state, "Срок истёк; доступна операция ClaimTimeout.");
        }

        return (state.Stage, input.Kind) switch
        {
            (ContractStage.Idle, EventKind.Reserve) => input.Amount == state.Terms.Reservation
                ? Accept(new ContractState(state.Terms, ContractStage.Reserved, escrow: state.Terms.Reservation),
                    "Средства зарезервированы.")
                : Reject(state, "Сумма должна совпадать с максимальной стоимостью запроса."),
            (ContractStage.Reserved, EventKind.Accept) =>
                Accept(MoveTo(state, ContractStage.Processing), "Провайдер принял запрос."),
            (ContractStage.Processing, EventKind.PublishReceipt) => PublishReceipt(state, input.Receipt),
            (ContractStage.AwaitingConfirmation, EventKind.Confirm) => input.ReceiptMatches
                ? Settle(state)
                : Reject(state, "Подтверждение относится к другой квитанции."),
            (ContractStage.AwaitingConfirmation, EventKind.OpenDispute) => input.ReceiptMatches
                ? Accept(MoveTo(state, ContractStage.Disputed), "Открыт спор.")
                : Reject(state, "Спор относится к другой квитанции."),
            (ContractStage.Disputed, EventKind.ResolvePay) => input.ReceiptMatches
                ? Settle(state)
                : Reject(state, "Вердикт относится к другой квитанции."),
            (ContractStage.Disputed, EventKind.ResolveRefund) => input.ReceiptMatches
                ? Refund(state)
                : Reject(state, "Вердикт относится к другой квитанции."),
            _ => Reject(state, "Событие недопустимо на текущем этапе.")
        };
    }

    private static bool HasPermission(ContractEvent input) => input.Kind switch
    {
        EventKind.Reserve or EventKind.Confirm or EventKind.OpenDispute => input.Actor == ActorRole.User,
        EventKind.Accept or EventKind.PublishReceipt => input.Actor == ActorRole.Provider,
        EventKind.ResolvePay or EventKind.ResolveRefund => input.Actor == ActorRole.Arbiter,
        EventKind.ClaimTimeout => input.Actor is ActorRole.User or ActorRole.Provider or ActorRole.Arbiter or ActorRole.Observer,
        _ => false
    };

    private static TransitionResult PublishReceipt(ContractState state, Receipt? receipt)
    {
        if (receipt is null || !receipt.RequestMatches || !receipt.ModelMatches || !receipt.TermsMatch
            || !receipt.InputHashMatches || !receipt.HasResponseHash || !receipt.SignatureValid)
        {
            return Reject(state, "Квитанция не прошла проверку соответствия или подписи.");
        }

        if (receipt.UsedTokens < 1 || receipt.UsedTokens > state.Terms.MaxTokens
            || receipt.Cost != checked(receipt.UsedTokens * state.Terms.PricePerToken))
        {
            return Reject(state, "Объём или стоимость квитанции не соответствуют условиям.");
        }

        return Accept(MoveTo(state, ContractStage.AwaitingConfirmation, receipt), "Квитанция опубликована.");
    }

    private static TransitionResult ClaimTimeout(ContractState state)
    {
        if (!state.DeadlineReached)
        {
            return Reject(state, "Срок текущего этапа ещё не истёк.");
        }

        return state.Stage switch
        {
            ContractStage.AwaitingConfirmation =>
                Accept(MoveTo(state, ContractStage.Disputed), "По тайм-ауту открыт спор с новым сроком."),
            ContractStage.Reserved or ContractStage.Processing or ContractStage.Disputed => Refund(state),
            _ => Reject(state, "На текущем этапе нет операции завершения по сроку.")
        };
    }

    private static TransitionResult Settle(ContractState state)
    {
        if (state.Receipt is null)
        {
            return Reject(state, "Для оплаты требуется опубликованная квитанция.");
        }

        return Accept(new ContractState(
            state.Terms, ContractStage.Settled, receipt: state.Receipt,
            providerPaid: state.Receipt.Cost,
            userRefunded: checked(state.Terms.Reservation - state.Receipt.Cost)),
            "Провайдеру начислена стоимость, пользователю возвращён остаток.");
    }

    private static TransitionResult Refund(ContractState state) => Accept(new ContractState(
        state.Terms, ContractStage.Refunded, receipt: state.Receipt,
        userRefunded: state.Terms.Reservation), "Резерв полностью возвращён пользователю.");

    private static ContractState MoveTo(ContractState state, ContractStage stage, Receipt? receipt = null) =>
        new(state.Terms, stage, receipt: receipt ?? state.Receipt,
            escrow: state.Escrow, providerPaid: state.ProviderPaid, userRefunded: state.UserRefunded);

    private static TransitionResult Accept(ContractState state, string description) => new(state, true, description);
    private static TransitionResult Reject(ContractState state, string description) => new(state, false, description);
}

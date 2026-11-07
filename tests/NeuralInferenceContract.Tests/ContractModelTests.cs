using NeuralInferenceContract.Model;

namespace NeuralInferenceContract.Tests;

public sealed class ContractModelTests
{
    [Theory]
    [InlineData(1, 2, 1, 1, 1)]
    [InlineData(3, 5, 2, 6, 9)]
    [InlineData(3, 5, 5, 15, 0)]
    [InlineData(1, long.MaxValue, long.MaxValue, long.MaxValue, 0)]
    public void Confirmation_pays_usage_and_returns_unused_reservation(
        long price, long limit, long used, long expectedPayment, long expectedRefund)
    {
        var terms = new ContractTerms(price, limit);
        var state = ModelTestData.ApplyAccepted(ContractState.Create(terms),
            new(EventKind.Reserve, ActorRole.User, Amount: terms.Reservation),
            new(EventKind.Accept, ActorRole.Provider),
            new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: new(used, expectedPayment)),
            new(EventKind.Confirm, ActorRole.User));

        Assert.Equal(ContractStage.Settled, state.Stage);
        Assert.Equal(0, state.Escrow);
        Assert.Equal(expectedPayment, state.ProviderPaid);
        Assert.Equal(expectedRefund, state.UserRefunded);
        Assert.Equal(1, state.Output);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(long.MaxValue, 2)]
    [InlineData(2, long.MaxValue)]
    public void Invalid_terms_are_rejected_before_a_state_is_created(long price, long limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContractTerms(price, limit));
    }

    [Fact]
    public void Invalid_receipts_leave_funds_and_state_unchanged()
    {
        var state = ModelTestData.AtStage(ContractStage.Processing);
        foreach (var receipt in ModelTestData.InvalidReceipts())
        {
            var result = ContractModel.Apply(state,
                new(EventKind.PublishReceipt, ActorRole.Provider, Receipt: receipt));
            Assert.False(result.Accepted);
            Assert.Same(state, result.State);
        }
    }

    [Theory]
    [InlineData(ContractStage.Idle, EventKind.Reserve, ActorRole.User)]
    [InlineData(ContractStage.Reserved, EventKind.Accept, ActorRole.Provider)]
    [InlineData(ContractStage.Processing, EventKind.PublishReceipt, ActorRole.Provider)]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.Confirm, ActorRole.User)]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.OpenDispute, ActorRole.User)]
    [InlineData(ContractStage.Disputed, EventKind.ResolvePay, ActorRole.Arbiter)]
    [InlineData(ContractStage.Disputed, EventKind.ResolveRefund, ActorRole.Arbiter)]
    [InlineData(ContractStage.Processing, EventKind.DeadlineReached, ActorRole.Clock)]
    public void Other_roles_cannot_perform_authorized_action(
        ContractStage stage, EventKind kind, ActorRole authorized)
    {
        var state = ModelTestData.AtStage(stage);
        foreach (var actor in Enum.GetValues<ActorRole>().Where(actor => actor != authorized))
        {
            var result = ContractModel.Apply(state,
                new(kind, actor, Amount: 2, Receipt: ModelTestData.ValidReceipt));
            Assert.False(result.Accepted);
            Assert.Same(state, result.State);
        }
    }

    [Theory]
    [InlineData(ContractStage.Reserved, ContractStage.Refunded)]
    [InlineData(ContractStage.Processing, ContractStage.Refunded)]
    [InlineData(ContractStage.AwaitingConfirmation, ContractStage.Disputed)]
    [InlineData(ContractStage.Disputed, ContractStage.Refunded)]
    public void Timeout_requires_elapsed_deadline_and_separate_claim(
        ContractStage stage, ContractStage expected)
    {
        var state = ModelTestData.AtStage(stage);
        var claim = new ContractEvent(EventKind.ClaimTimeout, ActorRole.Observer);
        var early = ContractModel.Apply(state, claim);
        Assert.False(early.Accepted);
        Assert.Same(state, early.State);

        var expired = ModelTestData.ApplyAccepted(state, new ContractEvent(EventKind.DeadlineReached, ActorRole.Clock));
        Assert.Equal(stage, expired.Stage);
        Assert.Equal(2, expired.Escrow);
        Assert.Equal(0, expired.ProviderPaid);
        Assert.Equal(0, expired.UserRefunded);
        Assert.True(expired.DeadlineReached);
        Assert.Equal(expired, ContractModel.Apply(expired,
            new(EventKind.DeadlineReached, ActorRole.Clock)).State);

        var completed = ModelTestData.ApplyAccepted(expired, claim);
        Assert.Equal(expected, completed.Stage);
        Assert.False(completed.DeadlineReached);
        Assert.Equal(expected == ContractStage.Refunded ? 2 : 0, completed.UserRefunded);
    }

    [Theory]
    [InlineData(ContractStage.Reserved, EventKind.Accept, ActorRole.Provider)]
    [InlineData(ContractStage.Processing, EventKind.PublishReceipt, ActorRole.Provider)]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.Confirm, ActorRole.User)]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.OpenDispute, ActorRole.User)]
    [InlineData(ContractStage.Disputed, EventKind.ResolvePay, ActorRole.Arbiter)]
    [InlineData(ContractStage.Disputed, EventKind.ResolveRefund, ActorRole.Arbiter)]
    public void Business_action_at_reached_deadline_is_rejected(
        ContractStage stage, EventKind kind, ActorRole actor)
    {
        var state = ModelTestData.ApplyAccepted(ModelTestData.AtStage(stage),
            new ContractEvent(EventKind.DeadlineReached, ActorRole.Clock));
        var result = ContractModel.Apply(state, new(kind, actor, Receipt: ModelTestData.ValidReceipt));
        Assert.False(result.Accepted);
        Assert.Same(state, result.State);
    }

    [Fact]
    public void Dispute_opened_by_timeout_has_its_own_deadline()
    {
        var dispute = ModelTestData.ApplyAccepted(ModelTestData.AtStage(ContractStage.AwaitingConfirmation),
            new(EventKind.DeadlineReached, ActorRole.Clock),
            new(EventKind.ClaimTimeout, ActorRole.Provider));
        Assert.Equal(ContractStage.Disputed, dispute.Stage);
        Assert.False(dispute.DeadlineReached);
        Assert.False(ContractModel.Apply(dispute, new(EventKind.ClaimTimeout, ActorRole.User)).Accepted);

        var refunded = ModelTestData.ApplyAccepted(dispute,
            new(EventKind.DeadlineReached, ActorRole.Clock),
            new(EventKind.ClaimTimeout, ActorRole.User));
        Assert.Equal(2, refunded.UserRefunded);
        Assert.Equal(0, refunded.Escrow);
    }

    [Theory]
    [InlineData(ActorRole.User)]
    [InlineData(ActorRole.Provider)]
    [InlineData(ActorRole.Arbiter)]
    [InlineData(ActorRole.Observer)]
    public void Any_participant_can_initiate_refund_after_deadline(ActorRole actor)
    {
        var state = ModelTestData.ApplyAccepted(ModelTestData.AtStage(ContractStage.Processing),
            new(EventKind.DeadlineReached, ActorRole.Clock),
            new(EventKind.ClaimTimeout, actor));
        Assert.Equal(ContractStage.Refunded, state.Stage);
        Assert.Equal(2, state.UserRefunded);
    }

    [Theory]
    [InlineData(ActorRole.None)]
    [InlineData(ActorRole.Clock)]
    public void Nonparticipant_cannot_claim_timeout(ActorRole actor)
    {
        var state = ModelTestData.ApplyAccepted(ModelTestData.AtStage(ContractStage.Processing),
            new ContractEvent(EventKind.DeadlineReached, ActorRole.Clock));
        Assert.False(ContractModel.Apply(state, new(EventKind.ClaimTimeout, actor)).Accepted);
    }

    [Theory]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.Confirm, ActorRole.User)]
    [InlineData(ContractStage.AwaitingConfirmation, EventKind.OpenDispute, ActorRole.User)]
    [InlineData(ContractStage.Disputed, EventKind.ResolvePay, ActorRole.Arbiter)]
    [InlineData(ContractStage.Disputed, EventKind.ResolveRefund, ActorRole.Arbiter)]
    public void Decision_must_reference_current_receipt(ContractStage stage, EventKind kind, ActorRole actor)
    {
        var state = ModelTestData.AtStage(stage);
        var result = ContractModel.Apply(state, new(kind, actor, ReceiptMatches: false));
        Assert.False(result.Accepted);
        Assert.Same(state, result.State);
    }

    [Fact]
    public void Finite_configuration_preserves_invariants_for_all_reachable_transitions()
    {
        var alphabet = ModelTestData.FiniteAlphabet();
        var initial = ContractState.Create(ModelTestData.Terms);
        var visited = new HashSet<ContractState> { initial };
        var pending = new Queue<ContractState>();
        pending.Enqueue(initial);

        while (pending.TryDequeue(out var state))
        {
            foreach (var input in alphabet)
            {
                var result = ContractModel.Apply(state, input);
                var next = result.State;
                Assert.Equal(result, ContractModel.Apply(state, input));
                Assert.Equal(next.Output, result.Output);
                Assert.InRange(next.Escrow, 0, 2);
                Assert.InRange(next.ProviderPaid, 0, 2);
                Assert.InRange(next.UserRefunded, 0, 2);
                Assert.Equal(next.Stage == ContractStage.Idle ? 0 : 2,
                    next.Escrow + next.ProviderPaid + next.UserRefunded);

                if (!result.Accepted || state.IsFinal)
                {
                    Assert.Same(state, next);
                }

                if (result.Accepted && next != state)
                {
                    var otherRequest = ContractModel.Apply(state, input with { RequestMatches = false });
                    Assert.False(otherRequest.Accepted);
                    Assert.Same(state, otherRequest.State);
                }

                if (next.IsFinal)
                {
                    Assert.Equal(0, next.Escrow);
                    Assert.False(next.DeadlineReached);
                }

                if (visited.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        Assert.Equal(Enum.GetValues<ContractStage>().Order(), visited.Select(state => state.Stage).Distinct().Order());
        Assert.Contains(visited, state => state.Stage == ContractStage.Settled && state.ProviderPaid == 1);
        Assert.Contains(visited, state => state.Stage == ContractStage.Settled && state.ProviderPaid == 2);
    }
}

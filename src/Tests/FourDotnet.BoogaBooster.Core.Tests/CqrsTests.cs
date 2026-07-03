using FourDotnet.BoogaBooster.Core.Cqrs;
using Xunit;

namespace FourDotnet.BoogaBooster.Core.Tests;

/// <summary>
/// Exercises the hand-written CQRS base types (ADR-0005) through minimal concrete
/// command/query handlers, confirming the base plumbing dispatches as intended.
/// </summary>
public class CqrsTests
{
    private sealed record AddNumbersCommand(int A, int B) : Command;

    private sealed class AddNumbersCommandHandler : CommandHandler<AddNumbersCommand>
    {
        public int? Result { get; private set; }

        public override Task HandleAsync(AddNumbersCommand command, CancellationToken cancellationToken)
        {
            Result = command.A + command.B;
            return Task.CompletedTask;
        }
    }

    private sealed record SquareQuery(int Value) : Query<int>;

    private sealed class SquareQueryHandler : QueryHandler<SquareQuery, int>
    {
        public override Task<int> HandleAsync(SquareQuery query, CancellationToken cancellationToken)
            => Task.FromResult(query.Value * query.Value);
    }

    [Fact]
    public async Task CommandHandler_HandlesCommand_ThroughInterface()
    {
        ICommandHandler<AddNumbersCommand> handler = new AddNumbersCommandHandler();

        await handler.HandleAsync(new AddNumbersCommand(2, 3), CancellationToken.None);

        Assert.Equal(5, ((AddNumbersCommandHandler)handler).Result);
    }

    [Fact]
    public async Task QueryHandler_ReturnsResponse_ThroughInterface()
    {
        IQueryHandler<SquareQuery, int> handler = new SquareQueryHandler();

        var result = await handler.HandleAsync(new SquareQuery(4), CancellationToken.None);

        Assert.Equal(16, result);
    }

    [Fact]
    public void Commands_AreValueEqualRecords()
    {
        Assert.Equal(new AddNumbersCommand(1, 2), new AddNumbersCommand(1, 2));
        Assert.NotEqual(new AddNumbersCommand(1, 2), new AddNumbersCommand(2, 1));
    }

    [Fact]
    public void Queries_AreValueEqualRecords()
    {
        Assert.Equal(new SquareQuery(3), new SquareQuery(3));
        Assert.NotEqual(new SquareQuery(3), new SquareQuery(4));
    }
}

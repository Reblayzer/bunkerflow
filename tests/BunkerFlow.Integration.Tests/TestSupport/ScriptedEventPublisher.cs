using BunkerFlow.Contracts;
using BunkerFlow.Integration.Publishing;

namespace BunkerFlow.Integration.Tests.TestSupport;

/// <summary>
/// A publisher whose behaviour each test scripts: succeed, fail a few times
/// then succeed, or always fail.
/// </summary>
public sealed class ScriptedEventPublisher : IEventPublisher
{
    private readonly Func<int, Exception?> _failureFor;
    private readonly List<IntegrationEvent> _published = [];

    private ScriptedEventPublisher(Func<int, Exception?> failureFor) => _failureFor = failureFor;

    public IReadOnlyList<IntegrationEvent> Published => _published;

    public int Attempts { get; private set; }

    public static ScriptedEventPublisher AlwaysSucceeds() => new(_ => null);

    public static ScriptedEventPublisher AlwaysFailsWith(Exception exception) => new(_ => exception);

    public static ScriptedEventPublisher FailsTimes(int times, Exception exception) =>
        new(attempt => attempt <= times ? exception : null);

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Attempts++;

        var failure = _failureFor(Attempts);
        if (failure is not null)
        {
            return Task.FromException(failure);
        }

        _published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

namespace Velucid.Silo.Tests.Infrastructure;

/// <summary>
/// A controllable <see cref="TimeProvider"/> for testing time-dependent logic.
/// Starts at a fixed UTC timestamp and advances only when explicitly requested.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}

using FluentAssertions;
using OsrsTracker.Api.Services;
using OsrsTracker.Domain.Models;
// IsDue is internal (visible via InternalsVisibleTo) and tests the boolean logic only.
// The PollingService uses an inlined LINQ expression rather than calling IsDue directly,
// because EF Core can't translate arbitrary C# method calls to SQL.

namespace OsrsTracker.Tests;

public class PollingServiceTests
{
    private static readonly DateTime Cutoff = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsDue_WhenNeverPolled_ReturnsTrue()
    {
        var account = new TrackedAccount
        {
            OsrsUsername = "Zezima",
            DisplayName = "Zezima",
            LastPolledAt = null
        };

        PollingService.IsDue(account, Cutoff).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenPolledBeforeCutoff_ReturnsTrue()
    {
        var account = new TrackedAccount
        {
            OsrsUsername = "Zezima",
            DisplayName = "Zezima",
            LastPolledAt = Cutoff.AddMinutes(-1)
        };

        PollingService.IsDue(account, Cutoff).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenPolledAfterCutoff_ReturnsFalse()
    {
        var account = new TrackedAccount
        {
            OsrsUsername = "Zezima",
            DisplayName = "Zezima",
            LastPolledAt = Cutoff.AddMinutes(1)
        };

        PollingService.IsDue(account, Cutoff).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WhenPolledExactlyAtCutoff_ReturnsFalse()
    {
        var account = new TrackedAccount
        {
            OsrsUsername = "Zezima",
            DisplayName = "Zezima",
            LastPolledAt = Cutoff
        };

        // Equal to cutoff means it was polled AT the cutoff — not yet due again
        PollingService.IsDue(account, Cutoff).Should().BeFalse();
    }
}

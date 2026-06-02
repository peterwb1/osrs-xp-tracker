using FluentAssertions;
using OsrsTracker.Domain.Stats;
using Xunit;

namespace OsrsTracker.Tests;

public class CombatLevelTests
{
    [Fact]
    public void MaxedMeleeAccount_Is126()
    {
        var combat = CombatLevel.Calculate(
            attack: 99, strength: 99, defence: 99, hitpoints: 99,
            ranged: 99, prayer: 99, magic: 99);

        combat.Should().Be(126);
    }

    [Fact]
    public void FreshAccount_Is3()
    {
        // A new account starts with all stats at 1 except Hitpoints at 10.
        var combat = CombatLevel.Calculate(
            attack: 1, strength: 1, defence: 1, hitpoints: 10,
            ranged: 1, prayer: 1, magic: 1);

        combat.Should().Be(3);
    }

    [Fact]
    public void RangedAccount_UsesRangedBranchWhenHigher()
    {
        // High ranged, low melee — the ranged branch should dominate.
        var combat = CombatLevel.Calculate(
            attack: 1, strength: 1, defence: 1, hitpoints: 50,
            ranged: 80, prayer: 1, magic: 1);

        // base = 0.25*(1+50+0) = 12.75; ranged = 0.325*floor(120) = 39
        // combat = floor(12.75 + 39) = 51
        combat.Should().Be(51);
    }
}

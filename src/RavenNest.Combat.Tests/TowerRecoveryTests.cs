using Xunit;

namespace RavenNest.Combat.Tests
{
    public class TowerRecoveryTests
    {
        [Fact]
        public void AKnockedOutPlayerComesBackAfterOneFixedTimeAtAShareOfTheirHealth()
        {
            // one time for every floor and every knockout, shorter than the shortest boss time limit by far
            Assert.True(TowerRecovery.Seconds >= 10 && TowerRecovery.Seconds <= 120);
            Assert.True(TowerRecovery.Seconds < Tower.BossTimeLimitSeconds / 3.0);
            Assert.True(TowerRecovery.ReturnHealth > 0 && TowerRecovery.ReturnHealth <= 1);
            Assert.True(TowerRecovery.WalkBackSeconds >= 0);
        }
    }
}

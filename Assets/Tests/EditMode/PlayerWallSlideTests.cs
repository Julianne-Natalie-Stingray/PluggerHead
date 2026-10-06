using NUnit.Framework;

namespace PluggerHead.Tests
{
    public sealed class PlayerWallSlideTests
    {
        [TestCase(-1, false)]
        [TestCase(1, false)]
        [TestCase(-1, true)]
        [TestCase(1, true)]
        public void HoldingIntoWall_PreservesGravityAndJumpArc(int direction, bool ascending)
        {
            IntegrationCheckBridge.Invoke("PlayerWallSlideIntegrationChecks", "CheckWall", direction, ascending);
        }

        [Test]
        public void GroundMovementStoppingAndJump_RemainResponsive()
        {
            IntegrationCheckBridge.Invoke("PlayerWallSlideIntegrationChecks", "CheckGroundAndJump");
        }
    }
}

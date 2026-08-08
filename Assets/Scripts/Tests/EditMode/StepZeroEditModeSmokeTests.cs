using NUnit.Framework;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepZeroEditModeSmokeTests
    {
        [Test]
        public void RuntimeAssemblyCanBeReferenced()
        {
            Assert.That(
                typeof(RuntimeAssemblyMarker).Assembly.GetName().Name,
                Is.EqualTo("MonstersVsZombies.Runtime"));
        }
    }
}

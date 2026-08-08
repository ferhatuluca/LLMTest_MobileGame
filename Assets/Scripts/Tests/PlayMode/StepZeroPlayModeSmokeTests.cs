using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MonstersVsZombies.Tests.PlayMode
{
    public sealed class StepZeroPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator RuntimeAssemblyCanBeReferencedInPlayMode()
        {
            yield return null;

            Assert.That(
                typeof(RuntimeAssemblyMarker).Assembly.GetName().Name,
                Is.EqualTo("MonstersVsZombies.Runtime"));
        }
    }
}

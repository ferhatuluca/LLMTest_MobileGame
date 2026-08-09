using MonstersVsZombies.Units.Movement;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSevenMotorProbe : MonoBehaviour, IUnitMotor
    {
        public bool IsStopped { get; private set; }
        public int MoveRequestCount { get; private set; }
        public int FaceRequestCount { get; private set; }
        public int StopCount { get; private set; }
        public int ResumeCount { get; private set; }

        public void MoveTo(Vector3 worldPosition)
        {
            MoveRequestCount++;
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            FaceRequestCount++;
        }

        public void Stop()
        {
            IsStopped = true;
            StopCount++;
        }

        public void Resume()
        {
            IsStopped = false;
            ResumeCount++;
        }
    }
}

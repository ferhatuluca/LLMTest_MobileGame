using UnityEngine;

namespace MonstersVsZombies.Units.Movement
{
    public interface IUnitMotor
    {
        bool IsStopped { get; }

        void MoveTo(Vector3 worldPosition);
        void FaceTowards(Vector3 worldPosition);
        void Stop();
        void Resume();
    }
}

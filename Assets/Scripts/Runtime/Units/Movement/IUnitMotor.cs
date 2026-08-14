using UnityEngine;

namespace MonstersVsZombies.Units.Movement
{
    /// <summary>
    /// Abstracts movement commands shared by Player and NavMesh-controlled units.
    /// </summary>
    public interface IUnitMotor
    {
        bool IsStopped { get; }

        void MoveTo(Vector3 worldPosition);
        void FaceTowards(Vector3 worldPosition);
        void Stop();
        void Resume();
    }

}

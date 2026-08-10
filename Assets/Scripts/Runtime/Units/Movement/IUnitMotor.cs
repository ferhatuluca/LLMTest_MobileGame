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

    /// <summary>
    /// Allows AI movement to decide when a NavMesh destination should be refreshed.
    /// </summary>
    public interface IDestinationRefreshPolicy
    {
        float DestinationRefreshDistance { get; }
    }
}

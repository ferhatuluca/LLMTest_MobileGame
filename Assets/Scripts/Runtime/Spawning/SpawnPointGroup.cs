using System;
using UnityEngine;

namespace MonstersVsZombies.Spawning
{
    /// <summary>
    /// Selects deterministic positions from an authored group of faction spawn
    /// points without embedding formation values in spawning services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPointGroup : MonoBehaviour
    {
        [SerializeField] private Transform[] _spawnPoints = Array.Empty<Transform>();
        private int _nextRoundRobinIndex;

        public int Count => _spawnPoints?.Length ?? 0;

        public bool TryGetPoint(int index, out Pose spawnPose)
        {
            if (_spawnPoints == null || index < 0 || index >= _spawnPoints.Length ||
                _spawnPoints[index] == null)
            {
                spawnPose = default;
                return false;
            }

            Transform spawnPoint = _spawnPoints[index];
            spawnPose = new Pose(spawnPoint.position, spawnPoint.rotation);
            return true;
        }

        public bool TryGetNext(out Pose spawnPose)
        {
            if (Count == 0)
            {
                spawnPose = default;
                return false;
            }

            int selectedIndex = _nextRoundRobinIndex;
            if (!TryGetPoint(selectedIndex, out spawnPose))
            {
                return false;
            }

            _nextRoundRobinIndex = (_nextRoundRobinIndex + 1) % Count;
            return true;
        }

        public void ResetRoundRobin()
        {
            _nextRoundRobinIndex = 0;
        }

        internal void Configure(Transform[] spawnPoints)
        {
            _spawnPoints = spawnPoints == null
                ? Array.Empty<Transform>()
                : (Transform[])spawnPoints.Clone();
            ResetRoundRobin();
        }
    }
}

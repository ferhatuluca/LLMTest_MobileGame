using System;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Units;
using UnityEngine;

namespace MonstersVsZombies.Combat.Projectiles
{
    /// <summary>
    /// Presents a short-lived pooled hitscan beam between two captured points.
    /// The beam inherits its attacker's faction color and returns itself when
    /// its presentation lifetime expires.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PooledEntity))]
    public sealed class LaserBeamPresentationController : MonoBehaviour,
        IPoolable
    {
        private PooledEntity _pooledEntity;
        private PoolManager _poolManager;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private float _elapsedTime;
        private bool _isConfigured;
        private UnitFaction _sourceFaction;
        private Renderer[] _factionRenderers;
        private MaterialPropertyBlock _factionPropertyBlock;

        [field: SerializeField] public Transform VisualTransform { get; private set; }
        [field: SerializeField] public float Lifetime { get; private set; }

        public bool IsRunning { get; private set; }

        private void Awake()
        {
            CacheComponents();
            CacheFactionVisuals();
        }

        private void OnValidate()
        {
            CacheComponents();
            CacheFactionVisuals();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public bool ConfigurePresentation(
            Vector3 startPosition,
            Vector3 endPosition,
            PoolManager poolManager,
            UnitFaction sourceFaction)
        {
            if (gameObject.activeInHierarchy || poolManager == null ||
                !IsFinite(startPosition) || !IsFinite(endPosition) ||
                (endPosition - startPosition).sqrMagnitude <= Mathf.Epsilon ||
                !FactionVisuals.TryGetColor(sourceFaction, out _))
            {
                return false;
            }

            _startPosition = startPosition;
            _endPosition = endPosition;
            _poolManager = poolManager;
            _sourceFaction = sourceFaction;
            _isConfigured = true;
            return true;
        }

        public bool PrepareForSpawn()
        {
            CacheComponents();
            _elapsedTime = 0f;
            IsRunning = false;
            if (!_isConfigured || !ValidateConfiguration(out _))
            {
                return false;
            }

            Vector3 beamDirection = _endPosition - _startPosition;
            float beamLength = beamDirection.magnitude;
            transform.SetPositionAndRotation(
                (_startPosition + _endPosition) * 0.5f,
                Quaternion.LookRotation(beamDirection.normalized, Vector3.up));
            VisualTransform.localPosition = Vector3.zero;
            Vector3 visualScale = VisualTransform.localScale;
            visualScale.z = beamLength;
            VisualTransform.localScale = visualScale;
            ApplyFactionVisuals();
            return true;
        }

        public bool CompleteSpawn()
        {
            IsRunning = gameObject.activeInHierarchy;
            return IsRunning;
        }

        public void PrepareForReturn()
        {
            IsRunning = false;
            _elapsedTime = 0f;
            _poolManager = null;
            _startPosition = default;
            _endPosition = default;
            _sourceFaction = default;
            _isConfigured = false;
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            CacheComponents();
            if (_pooledEntity == null || VisualTransform == null ||
                Lifetime <= 0f || float.IsNaN(Lifetime) ||
                float.IsInfinity(Lifetime))
            {
                failureMessage =
                    "LaserBeamPresentationController requires a pooled root, visual transform, and positive finite lifetime.";
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        internal void AdvanceTime(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Beam presentation time must be non-negative and finite.");
            }

            if (!IsRunning)
            {
                return;
            }

            _elapsedTime += deltaTime;
            if (_elapsedTime >= Lifetime)
            {
                IsRunning = false;
                _poolManager.Return(_pooledEntity);
            }
        }

        internal bool ConfigureAsset(Transform visualTransform, float lifetime)
        {
            if (visualTransform == null || lifetime <= 0f ||
                float.IsNaN(lifetime) || float.IsInfinity(lifetime))
            {
                return false;
            }

            VisualTransform = visualTransform;
            Lifetime = lifetime;
            return true;
        }

        private void CacheComponents()
        {
            _pooledEntity = GetComponent<PooledEntity>();
            if (VisualTransform == null && transform.childCount == 1)
            {
                VisualTransform = transform.GetChild(0);
            }
        }

        private void CacheFactionVisuals()
        {
            _factionRenderers = GetComponentsInChildren<Renderer>(true);
            _factionPropertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyFactionVisuals()
        {
            if (_factionRenderers == null || _factionPropertyBlock == null)
            {
                CacheFactionVisuals();
            }

            FactionVisuals.Apply(
                _factionRenderers,
                _sourceFaction,
                _factionPropertyBlock);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}

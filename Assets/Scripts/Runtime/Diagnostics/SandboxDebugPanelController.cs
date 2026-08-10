using System;
using System.Collections.Generic;
using MonstersVsZombies.Combat.Interaction;
using MonstersVsZombies.Core.Bootstrap;
using MonstersVsZombies.Core.Pooling;
using MonstersVsZombies.Data;
using MonstersVsZombies.Spawning;
using MonstersVsZombies.Units;
using MonstersVsZombies.Units.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MonstersVsZombies.Diagnostics
{
    [Serializable]
    public sealed class SandboxSpawnButtonBinding
    {
        [field: SerializeField] public UnitDefinition Definition { get; private set; }
        [field: SerializeField] public Button SpawnOneButton { get; private set; }
        [field: SerializeField] public Button SpawnTenButton { get; private set; }

        public bool IsValid => Definition != null &&
            SpawnOneButton != null && SpawnTenButton != null;

        public SandboxSpawnButtonBinding(
            UnitDefinition definition,
            Button spawnOneButton,
            Button spawnTenButton)
        {
            Definition = definition;
            SpawnOneButton = spawnOneButton;
            SpawnTenButton = spawnTenButton;
        }
    }

    /// <summary>
    /// Owns the Game-view developer panel, its spawn/stress buttons, live HUD-like
    /// diagnostics, pool table, pause controls, and gizmo toggles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxDebugPanelController : MonoBehaviour
    {
        private readonly List<PoolDiagnostics> _poolDiagnostics =
            new List<PoolDiagnostics>();
        private readonly List<Action> _buttonUnsubscriptions =
            new List<Action>();
        private float _refreshTimeRemaining;

        [field: SerializeField] public DebugUnitSpawner DebugUnitSpawner { get; private set; }
        [field: SerializeField] public PoolManager PoolManager { get; private set; }
        [field: SerializeField] public PoolCatalog PoolCatalog { get; private set; }
        [field: SerializeField] public UnitRegistry UnitRegistry { get; private set; }
        [field: SerializeField] public InteractionSystem InteractionSystem { get; private set; }
        [field: SerializeField] public CombatSandboxBootstrap Bootstrap { get; private set; }
        [field: SerializeField] public SandboxGizmoController GizmoController { get; private set; }
        [field: SerializeField] public Text PlayerText { get; private set; }
        [field: SerializeField] public Text FactionCountsText { get; private set; }
        [field: SerializeField] public Text PoolCountsText { get; private set; }
        [field: SerializeField] public Text LastInteractionText { get; private set; }
        [field: SerializeField] public Button ClearButton { get; private set; }
        [field: SerializeField] public Button ResetPlayerButton { get; private set; }
        [field: SerializeField] public Button PauseAIButton { get; private set; }
        [field: SerializeField] public Text PauseAIButtonText { get; private set; }
        [field: SerializeField] public Toggle ChaseRangeToggle { get; private set; }
        [field: SerializeField] public Toggle AttackRangeToggle { get; private set; }
        [field: SerializeField] public Toggle TargetLineToggle { get; private set; }
        [field: SerializeField] public Toggle SpawnPointToggle { get; private set; }
        [field: SerializeField] public SandboxStressPresetController StressPresetController { get; private set; }
        [field: SerializeField] public Button StressTenButton { get; private set; }
        [field: SerializeField] public Button StressFiftyButton { get; private set; }
        [field: SerializeField] public Button StressHundredButton { get; private set; }
        [field: SerializeField] public Text StressStatusText { get; private set; }
        [field: SerializeField] public SandboxSpawnButtonBinding[] SpawnButtons { get; private set; } =
            Array.Empty<SandboxSpawnButtonBinding>();

        public string LastInteractionSummary { get; private set; } =
            "Last interaction: none";
        public bool HasStressPresetControls =>
            StressPresetController != null && StressTenButton != null &&
            StressFiftyButton != null && StressHundredButton != null &&
            StressStatusText != null;

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            if (!SandboxDebugRuntime.IsAvailable)
            {
                gameObject.SetActive(false);
                return;
            }

            SandboxDebugRuntime.SetDiagnosticsEnabled(true);
            if (InteractionSystem != null)
            {
                InteractionSystem.InteractionResolved += HandleInteractionResolved;
            }

            if (DebugUnitSpawner != null)
            {
                DebugUnitSpawner.DiagnosticReported += HandleDiagnosticReported;
            }

            _refreshTimeRemaining = 0f;
            RefreshPanel();
        }

        private void OnDisable()
        {
            if (InteractionSystem != null)
            {
                InteractionSystem.InteractionResolved -= HandleInteractionResolved;
            }

            if (DebugUnitSpawner != null)
            {
                DebugUnitSpawner.DiagnosticReported -= HandleDiagnosticReported;
            }
        }

        private void OnDestroy()
        {
            ReleaseButtons();
            SandboxDebugRuntime.SetAIDecisionsPaused(false);
            SandboxDebugRuntime.SetDiagnosticsEnabled(false);
        }

        private void Update()
        {
            _refreshTimeRemaining -= Time.unscaledDeltaTime;
            if (_refreshTimeRemaining <= 0f)
            {
                RefreshPanel();
                _refreshTimeRemaining = 0.25f;
            }
        }

        public bool ValidateConfiguration(out string failureMessage)
        {
            if (DebugUnitSpawner == null || PoolManager == null ||
                PoolCatalog == null || UnitRegistry == null ||
                InteractionSystem == null || Bootstrap == null ||
                GizmoController == null || PlayerText == null ||
                FactionCountsText == null || PoolCountsText == null ||
                LastInteractionText == null || ClearButton == null ||
                ResetPlayerButton == null || PauseAIButton == null ||
                PauseAIButtonText == null || ChaseRangeToggle == null ||
                AttackRangeToggle == null || TargetLineToggle == null ||
                SpawnPointToggle == null || SpawnButtons == null ||
                SpawnButtons.Length != 10)
            {
                failureMessage =
                    "SandboxDebugPanelController has missing services, controls, text, or concrete-unit button bindings.";
                return false;
            }

            HashSet<UnitDefinition> definitions = new HashSet<UnitDefinition>();
            foreach (SandboxSpawnButtonBinding binding in SpawnButtons)
            {
                if (binding == null || !binding.IsValid ||
                    !definitions.Add(binding.Definition))
                {
                    failureMessage =
                        "Every sandbox spawn binding must be complete and use a distinct concrete unit definition.";
                    return false;
                }
            }

            failureMessage = string.Empty;
            return true;
        }

        public void RefreshPanel()
        {
            RefreshPlayer();
            FactionCountsText.text =
                $"Active units  Player {UnitRegistry.GetFactionCount(UnitFaction.Player)}  " +
                $"Ally {UnitRegistry.GetFactionCount(UnitFaction.Ally)}  " +
                $"Enemy {UnitRegistry.GetFactionCount(UnitFaction.Enemy)}";
            RefreshPools();
            LastInteractionText.text = LastInteractionSummary;
            PauseAIButtonText.text = SandboxDebugRuntime.AreAIDecisionsPaused
                ? "Resume AI decisions"
                : "Pause AI decisions";
            RefreshStressStatus();
        }

        private void RefreshPlayer()
        {
            UnitController player = Bootstrap.InitialPlayer;
            if (player == null || player.HealthController == null)
            {
                PlayerText.text = "Player: not active";
                return;
            }

            PlayerWeaponController weapons =
                player.GetComponent<PlayerWeaponController>();
            string weaponName = weapons?.CurrentWeapon == null
                ? "none"
                : weapons.CurrentWeapon.DisplayName;
            PlayerText.text =
                $"Player health {player.HealthController.CurrentHealth:0}/" +
                $"{player.HealthController.MaximumHealth:0}  Weapon {weaponName}";
        }

        private void RefreshPools()
        {
            PoolManager.CopyDiagnostics(_poolDiagnostics);
            System.Text.StringBuilder builder = new System.Text.StringBuilder(512);
            builder.AppendLine(
                "Pool | Active | Inactive | Created | Peak | Failed | Capacity | Overflow");
            foreach (PoolDiagnostics diagnostics in _poolDiagnostics)
            {
                builder.Append(diagnostics.PoolId).Append(" | ")
                    .Append(diagnostics.ActiveCount).Append(" | ")
                    .Append(diagnostics.InactiveCount).Append(" | ")
                    .Append(diagnostics.CreatedCount).Append(" | ")
                    .Append(diagnostics.PeakActiveCount).Append(" | ")
                    .Append(diagnostics.FailedRentCount).Append(" | ")
                    .Append(diagnostics.CapacityReachedCount).Append(" | ")
                    .Append(diagnostics.OverflowDestroyCount).AppendLine();
            }

            PoolCountsText.text = builder.ToString();
        }

        private void BindButtons()
        {
            ReleaseButtons();
            if (!ValidateConfiguration(out _))
            {
                return;
            }

            foreach (SandboxSpawnButtonBinding binding in SpawnButtons)
            {
                UnitDefinition definition = binding.Definition;
                UnityEngine.Events.UnityAction spawnOne =
                    () => DebugUnitSpawner.Spawn(definition.UnitId);
                UnityEngine.Events.UnityAction spawnTen =
                    () => DebugUnitSpawner.SpawnMany(definition.UnitId, 10);
                binding.SpawnOneButton.onClick.AddListener(spawnOne);
                binding.SpawnTenButton.onClick.AddListener(spawnTen);
                _buttonUnsubscriptions.Add(
                    () => binding.SpawnOneButton.onClick.RemoveListener(spawnOne));
                _buttonUnsubscriptions.Add(
                    () => binding.SpawnTenButton.onClick.RemoveListener(spawnTen));
            }

            ClearButton.onClick.AddListener(HandleClear);
            ResetPlayerButton.onClick.AddListener(HandleResetPlayer);
            PauseAIButton.onClick.AddListener(HandleToggleAIPause);
            ChaseRangeToggle.onValueChanged.AddListener(HandleChaseRangeToggle);
            AttackRangeToggle.onValueChanged.AddListener(HandleAttackRangeToggle);
            TargetLineToggle.onValueChanged.AddListener(HandleTargetLineToggle);
            SpawnPointToggle.onValueChanged.AddListener(HandleSpawnPointToggle);
            _buttonUnsubscriptions.Add(
                () => ClearButton.onClick.RemoveListener(HandleClear));
            _buttonUnsubscriptions.Add(
                () => ResetPlayerButton.onClick.RemoveListener(HandleResetPlayer));
            _buttonUnsubscriptions.Add(
                () => PauseAIButton.onClick.RemoveListener(HandleToggleAIPause));
            _buttonUnsubscriptions.Add(
                () => ChaseRangeToggle.onValueChanged.RemoveListener(HandleChaseRangeToggle));
            _buttonUnsubscriptions.Add(
                () => AttackRangeToggle.onValueChanged.RemoveListener(HandleAttackRangeToggle));
            _buttonUnsubscriptions.Add(
                () => TargetLineToggle.onValueChanged.RemoveListener(HandleTargetLineToggle));
            _buttonUnsubscriptions.Add(
                () => SpawnPointToggle.onValueChanged.RemoveListener(HandleSpawnPointToggle));

            if (HasStressPresetControls)
            {
                StressTenButton.onClick.AddListener(HandleStressTen);
                StressFiftyButton.onClick.AddListener(HandleStressFifty);
                StressHundredButton.onClick.AddListener(HandleStressHundred);
                _buttonUnsubscriptions.Add(
                    () => StressTenButton.onClick.RemoveListener(HandleStressTen));
                _buttonUnsubscriptions.Add(
                    () => StressFiftyButton.onClick.RemoveListener(HandleStressFifty));
                _buttonUnsubscriptions.Add(
                    () => StressHundredButton.onClick.RemoveListener(HandleStressHundred));
            }
        }

        private void ReleaseButtons()
        {
            foreach (Action unsubscribe in _buttonUnsubscriptions)
            {
                unsubscribe();
            }

            _buttonUnsubscriptions.Clear();
        }

        private void HandleClear()
        {
            StressPresetController?.StopPreset();
            DebugUnitSpawner.ClearNonPlayerUnitsAndProjectiles();
            RefreshPanel();
        }

        private void HandleResetPlayer()
        {
            DebugUnitSpawner.ResetPlayer();
            RefreshPanel();
        }

        private void HandleToggleAIPause()
        {
            SandboxDebugRuntime.SetAIDecisionsPaused(
                !SandboxDebugRuntime.AreAIDecisionsPaused);
            RefreshPanel();
        }

        private void HandleChaseRangeToggle(bool value)
        {
            GizmoController.DrawChaseRanges = value;
        }

        private void HandleAttackRangeToggle(bool value)
        {
            GizmoController.DrawAttackRanges = value;
        }

        private void HandleTargetLineToggle(bool value)
        {
            GizmoController.DrawTargetLines = value;
        }

        private void HandleSpawnPointToggle(bool value)
        {
            GizmoController.DrawSpawnPoints = value;
        }

        private void HandleStressTen()
        {
            RunStressPreset(10);
        }

        private void HandleStressFifty()
        {
            RunStressPreset(50);
        }

        private void HandleStressHundred()
        {
            RunStressPreset(100);
        }

        private void RunStressPreset(int perFactionCount)
        {
            SandboxStressPresetResult result =
                StressPresetController.RunPreset(perFactionCount);
            RefreshPanel();
            StressStatusText.text = result.IsSuccess
                ? $"Maintaining {perFactionCount} Allies versus {perFactionCount} Enemies"
                : $"Stress preset failed: {result.PoolFailureReason}, " +
                  $"Allies {result.SpawnedAllies}/{perFactionCount}, " +
                  $"Enemies {result.SpawnedEnemies}/{perFactionCount}";
        }

        private void RefreshStressStatus()
        {
            if (!HasStressPresetControls)
            {
                return;
            }

            if (!StressPresetController.IsMaintainingPreset)
            {
                StressStatusText.text = "Stress preset: inactive";
                return;
            }

            int count = StressPresetController.RequestedPerFaction;
            StressStatusText.text =
                $"Maintaining {count} Allies versus {count} Enemies";
        }

        private void HandleInteractionResolved(
            InteractionResolvedEvent interactionEvent)
        {
            InteractionResult result = interactionEvent.Result;
            LastInteractionSummary = result.IsApplied
                ? $"Last interaction: {result.Outcome}, " +
                  $"damage {result.DamageResult.AppliedAmount:0.##}, " +
                  $"target {result.TargetSpawnId}"
                : $"Last interaction: {result.Outcome}, target {result.TargetSpawnId}";
            LastInteractionText.text = LastInteractionSummary;
        }

        private void HandleDiagnosticReported(
            SandboxDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent.Code != SandboxDiagnosticCode.SpawnSucceeded)
            {
                LastInteractionSummary =
                    $"Last diagnostic: {diagnosticEvent.Code} - {diagnosticEvent.Message}";
                LastInteractionText.text = LastInteractionSummary;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public enum HireFailureReason
{
    None,
    NotAwaitingWave,
    NotEnoughCoins,
    NoFreeSlot,
    WorkerCapReached,
    InvalidBuilding,
    BuildingHasNoWorkerSlot,
    BuildingLocked,
    ConfigurationMissing
}

public enum WorkerMoveFailureReason
{
    None,
    InvalidBuilding,
    BuildingHasNoWorkerSlot,
    BuildingLocked,
    NoWorkerOnSource,
    NoFreeSlot,
    SameBuilding,
    WavePaused,
    InvalidGameState
}

[Serializable]
public struct HireResult
{
    public bool Success;
    public HireFailureReason Reason;
    public int Cost;
    public string BuildingId;
    public string Message;

    public HireResult(bool success, HireFailureReason reason, int cost, string buildingId, string message)
    {
        Success = success;
        Reason = reason;
        Cost = cost;
        BuildingId = buildingId;
        Message = message;
    }
}

[Serializable]
public struct WorkerMoveResult
{
    public bool Success;
    public WorkerMoveFailureReason Reason;
    public string SourceBuildingId;
    public string TargetBuildingId;
    public string Message;

    public WorkerMoveResult(bool success, WorkerMoveFailureReason reason, string sourceId, string targetId, string message)
    {
        Success = success;
        Reason = reason;
        SourceBuildingId = sourceId;
        TargetBuildingId = targetId;
        Message = message;
    }
}

[Serializable]
public sealed class WorkerBuildingAssignmentData
{
    public string buildingId;
    public int workerCount;
}

[Serializable]
public sealed class WorkerRosterSaveData
{
    public int schemaVersion = 1;
    public int successfulPaidHires;
    public WorkerBuildingAssignmentData[] assignments = new WorkerBuildingAssignmentData[0];
}

[Serializable]
public sealed class WorkerBuildingSnapshot
{
    public string buildingId;
    public int workerCount;
    public int workerSlots;
    public float utilization;
}

[Serializable]
public sealed class WorkerRosterSnapshot
{
    public int workerCount;
    public int totalWorkerSlots;
    public int workerCap;
    public int nextHirePrice;
    public WorkerBuildingSnapshot[] buildings;
}

[AddComponentMenu("Workers/Worker Roster")]
public sealed class WorkerRosterComponent : MonoBehaviour
{
    [SerializeField] private MvpBuildingSystem buildings;
    [SerializeField] private GameStateManager gameState;
    [SerializeField] private WorkerTuningConfig tuning;

    private int successfulPaidHires;
    private bool initialized;

    public int WorkerCount { get; private set; }
    public int TotalWorkerSlots { get; private set; }
    public int WorkerCap { get; private set; }
    public int NextHirePrice => tuning == null ? -1 : tuning.GetHirePrice(successfulPaidHires);
    public WorkerRosterSnapshot CurrentSnapshot => BuildSnapshot();

    public event Action<HireResult> WorkerHired;
    public event Action<HireResult> HireRejected;
    public event Action<WorkerMoveResult> WorkerMoved;
    public event Action<WorkerRosterSnapshot> RosterChanged;
    public event Action<int> WorkerCountChanged;
    public event Action<string, int, float> BuildingUtilizationChanged;

    private void Start()
    {
        if (buildings == null)
            buildings = GetComponent<MvpBuildingSystem>();
        if (gameState == null)
            gameState = GetComponent<GameStateManager>();

        if (buildings == null || tuning == null)
        {
            Debug.LogError("Worker Roster requires MvpBuildingSystem and WorkerTuningConfig references.", this);
            enabled = false;
            return;
        }

        buildings.OnChanged += OnBuildingsChanged;
        if (gameState != null)
            gameState.StateChanged += OnGameStateChanged;

        initialized = true;
        ReconcileRoster();
        ApplyAllUtilization();
        PublishRoster();
    }

    private void OnDestroy()
    {
        if (buildings != null)
            buildings.OnChanged -= OnBuildingsChanged;
        if (gameState != null)
            gameState.StateChanged -= OnGameStateChanged;
    }

    public HireResult GetHireStatus(string targetBuildingId)
    {
        return EvaluateHire(targetBuildingId);
    }

    public HireResult GetHireStatus(MvpBuildingSystem.Building targetBuilding)
    {
        return EvaluateHire(targetBuilding == null ? null : targetBuilding.id);
    }

    public bool TryHireWorker(string targetBuildingId, out HireResult result)
    {
        result = EvaluateHire(targetBuildingId);
        if (!result.Success)
        {
            HireRejected?.Invoke(result);
            return false;
        }

        MvpBuildingSystem.Building target = buildings.Find(targetBuildingId);
        buildings.coins -= result.Cost;
        target.workers++;
        successfulPaidHires++;

        buildings.NotifyExternalChange();

        result = new HireResult(true, HireFailureReason.None, result.Cost, targetBuildingId, "Worker hired.");
        WorkerHired?.Invoke(result);
        return true;
    }

    public bool TryHireWorker(MvpBuildingSystem.Building targetBuilding, out HireResult result)
    {
        return TryHireWorker(targetBuilding == null ? null : targetBuilding.id, out result);
    }

    public bool TryMoveWorker(string sourceBuildingId, string targetBuildingId, out WorkerMoveResult result)
    {
        result = EvaluateMove(sourceBuildingId, targetBuildingId);
        if (!result.Success)
            return false;

        MvpBuildingSystem.Building source = buildings.Find(sourceBuildingId);
        MvpBuildingSystem.Building target = buildings.Find(targetBuildingId);
        source.workers--;
        target.workers++;

        buildings.NotifyExternalChange();

        result = new WorkerMoveResult(true, WorkerMoveFailureReason.None, sourceBuildingId, targetBuildingId, "Worker moved.");
        WorkerMoved?.Invoke(result);
        return true;
    }

    public float GetUtilization(string buildingId)
    {
        if (buildings == null || tuning == null)
            return 0f;

        MvpBuildingSystem.Building building = buildings.Find(buildingId);
        if (building == null || building.IsSpa || !building.unlocked)
            return tuning.utilizationEmpty;

        return tuning.GetUtilization(Mathf.Max(0, building.workers));
    }

    public int GetWorkerCount(string buildingId)
    {
        MvpBuildingSystem.Building building = buildings == null ? null : buildings.Find(buildingId);
        return building == null || building.IsSpa ? 0 : Mathf.Max(0, building.workers);
    }

    public WorkerRosterSaveData CaptureState()
    {
        List<WorkerBuildingAssignmentData> assignments = new List<WorkerBuildingAssignmentData>();
        if (buildings != null && buildings.buildings != null)
        {
            foreach (MvpBuildingSystem.Building building in buildings.buildings)
            {
                if (building == null)
                    continue;

                assignments.Add(new WorkerBuildingAssignmentData
                {
                    buildingId = building.id,
                    workerCount = building.IsSpa ? 0 : Mathf.Max(0, building.workers)
                });
            }
        }

        return new WorkerRosterSaveData
        {
            schemaVersion = 1,
            successfulPaidHires = successfulPaidHires,
            assignments = assignments.ToArray()
        };
    }

    public bool RestoreState(WorkerRosterSaveData data)
    {
        if (!initialized || data == null || buildings == null)
            return false;

        successfulPaidHires = Mathf.Max(0, data.successfulPaidHires);
        if (data.assignments != null)
        {
            foreach (WorkerBuildingAssignmentData assignment in data.assignments)
            {
                if (assignment == null || string.IsNullOrEmpty(assignment.buildingId))
                    continue;

                MvpBuildingSystem.Building building = buildings.Find(assignment.buildingId);
                if (building == null)
                    continue;

                building.workers = building.IsSpa ? 0 : Mathf.Max(0, assignment.workerCount);
            }
        }

        buildings.NotifyExternalChange();
        return true;
    }

    private HireResult EvaluateHire(string targetBuildingId)
    {
        int price = NextHirePrice;
        if (!initialized || buildings == null || tuning == null)
            return HireFailure(HireFailureReason.ConfigurationMissing, price, targetBuildingId, "Worker hiring is not configured.");

        if (gameState == null || gameState.State != GameState.AWAITING_WAVE)
            return HireFailure(HireFailureReason.NotAwaitingWave, price, targetBuildingId, "Workers can only be hired between waves.");

        MvpBuildingSystem.Building target = buildings.Find(targetBuildingId);
        if (target == null)
            return HireFailure(HireFailureReason.InvalidBuilding, price, targetBuildingId, "Select a valid building.");

        if (target.IsSpa || buildings.WorkerSlots(target) <= 0)
            return HireFailure(HireFailureReason.BuildingHasNoWorkerSlot, price, targetBuildingId, "This building has no Worker slots.");

        if (!target.unlocked)
            return HireFailure(HireFailureReason.BuildingLocked, price, targetBuildingId, "Unlock this building first.");

        if (target.workers >= buildings.WorkerSlots(target))
            return HireFailure(HireFailureReason.NoFreeSlot, price, targetBuildingId, "This building has no free Worker slot.");

        if (WorkerCount + 1 >= WorkerCap)
            return HireFailure(HireFailureReason.WorkerCapReached, price, targetBuildingId, "Upgrade buildings to open more shifts.");

        if (price < 0 || buildings.coins < price)
            return HireFailure(HireFailureReason.NotEnoughCoins, price, targetBuildingId, "Not enough Coins.");

        return new HireResult(true, HireFailureReason.None, price, targetBuildingId, "Ready to hire.");
    }

    private WorkerMoveResult EvaluateMove(string sourceBuildingId, string targetBuildingId)
    {
        if (!initialized || buildings == null || tuning == null)
            return MoveFailure(WorkerMoveFailureReason.InvalidBuilding, sourceBuildingId, targetBuildingId, "Worker roster is not configured.");

        if (gameState == null || gameState.State == GameState.WAVE_PAUSED)
            return MoveFailure(WorkerMoveFailureReason.WavePaused, sourceBuildingId, targetBuildingId, "Workers cannot be moved while the wave is paused.");

        GameState state = gameState.State;
        if (state != GameState.AWAITING_WAVE && state != GameState.WAVE_INCOMING &&
            state != GameState.WAVE_ACTIVE && state != GameState.WAVE_RESOLVING)
            return MoveFailure(WorkerMoveFailureReason.InvalidGameState, sourceBuildingId, targetBuildingId, "Workers cannot be moved in the current game state.");

        if (sourceBuildingId == targetBuildingId)
            return MoveFailure(WorkerMoveFailureReason.SameBuilding, sourceBuildingId, targetBuildingId, "Choose a different destination.");

        MvpBuildingSystem.Building source = buildings.Find(sourceBuildingId);
        MvpBuildingSystem.Building target = buildings.Find(targetBuildingId);
        if (source == null || target == null)
            return MoveFailure(WorkerMoveFailureReason.InvalidBuilding, sourceBuildingId, targetBuildingId, "Select valid buildings.");

        if (source.IsSpa || target.IsSpa || buildings.WorkerSlots(source) <= 0 || buildings.WorkerSlots(target) <= 0)
            return MoveFailure(WorkerMoveFailureReason.BuildingHasNoWorkerSlot, sourceBuildingId, targetBuildingId, "Spa and buildings without Worker slots cannot hold Workers.");

        if (!source.unlocked || !target.unlocked)
            return MoveFailure(WorkerMoveFailureReason.BuildingLocked, sourceBuildingId, targetBuildingId, "Both buildings must be unlocked.");

        if (source.workers <= 0)
            return MoveFailure(WorkerMoveFailureReason.NoWorkerOnSource, sourceBuildingId, targetBuildingId, "The source building has no Worker to move.");

        if (target.workers >= buildings.WorkerSlots(target))
            return MoveFailure(WorkerMoveFailureReason.NoFreeSlot, sourceBuildingId, targetBuildingId, "The destination building has no free Worker slot.");

        return new WorkerMoveResult(true, WorkerMoveFailureReason.None, sourceBuildingId, targetBuildingId, "Ready to move.");
    }

    private void OnBuildingsChanged()
    {
        if (!initialized)
            return;

        ReconcileRoster();
        ApplyAllUtilization();
        PublishRoster();
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.WAVE_INCOMING || state == GameState.WAVE_ACTIVE)
            ApplyAllUtilization();
    }

    private void ReconcileRoster()
    {
        if (buildings == null || buildings.buildings == null)
            return;

        int totalSlots = 0;
        int totalWorkers = 0;
        foreach (MvpBuildingSystem.Building building in buildings.buildings)
        {
            if (building == null)
                continue;

            if (building.IsSpa || !building.unlocked)
            {
                building.workers = 0;
                continue;
            }

            int slots = Mathf.Max(0, buildings.WorkerSlots(building));
            totalSlots += slots;
            building.workers = Mathf.Clamp(building.workers, 0, slots);
            totalWorkers += building.workers;
        }

        int cap = Mathf.Max(0, Mathf.FloorToInt(totalSlots * tuning.workerCapRatio));
        for (int i = buildings.buildings.Length - 1; i >= 0 && totalWorkers >= cap; i--)
        {
            MvpBuildingSystem.Building building = buildings.buildings[i];
            if (building == null || building.IsSpa || building.workers <= 0)
                continue;

            while (building.workers > 0 && totalWorkers >= cap)
            {
                building.workers--;
                totalWorkers--;
            }
        }

        WorkerCount = totalWorkers;
        TotalWorkerSlots = totalSlots;
        WorkerCap = cap;
    }

    private void ApplyAllUtilization()
    {
        if (buildings == null || buildings.buildings == null || tuning == null)
            return;

        foreach (MvpBuildingSystem.Building building in buildings.buildings)
            if (building != null && !building.IsSpa && building.unlocked)
                ApplyUtilization(building);
    }

    private void ApplyUtilization(MvpBuildingSystem.Building building)
    {
        if (building == null || building.IsSpa || tuning == null)
            return;

        float utilization = building.unlocked
            ? tuning.GetUtilization(Mathf.Max(0, building.workers))
            : tuning.utilizationEmpty;

        if (gameState != null)
            gameState.SetServiceSpeed(building.needType, utilization);

        BuildingUtilizationChanged?.Invoke(building.id, Mathf.Max(0, building.workers), utilization);
    }

    private WorkerRosterSnapshot BuildSnapshot()
    {
        List<WorkerBuildingSnapshot> records = new List<WorkerBuildingSnapshot>();
        if (buildings != null && buildings.buildings != null)
        {
            foreach (MvpBuildingSystem.Building building in buildings.buildings)
            {
                if (building == null || building.IsSpa)
                    continue;

                records.Add(new WorkerBuildingSnapshot
                {
                    buildingId = building.id,
                    workerCount = Mathf.Max(0, building.workers),
                    workerSlots = buildings.WorkerSlots(building),
                    utilization = tuning == null ? 0f : (building.unlocked
                        ? tuning.GetUtilization(Mathf.Max(0, building.workers))
                        : tuning.utilizationEmpty)
                });
            }
        }

        return new WorkerRosterSnapshot
        {
            workerCount = WorkerCount,
            totalWorkerSlots = TotalWorkerSlots,
            workerCap = WorkerCap,
            nextHirePrice = NextHirePrice,
            buildings = records.ToArray()
        };
    }

    private void PublishRoster()
    {
        RosterChanged?.Invoke(BuildSnapshot());
        WorkerCountChanged?.Invoke(WorkerCount);
    }

    private HireResult HireFailure(HireFailureReason reason, int cost, string buildingId, string message)
    {
        return new HireResult(false, reason, cost, buildingId, message);
    }

    private WorkerMoveResult MoveFailure(WorkerMoveFailureReason reason, string sourceId, string targetId, string message)
    {
        return new WorkerMoveResult(false, reason, sourceId, targetId, message);
    }
}

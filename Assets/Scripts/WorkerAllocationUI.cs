using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S3 Worker Allocation - shows Worker distribution and moves a Worker to another building
// (free and instant, before a wave and during a wave; during a wave closing returns to S2).
//
// The UI is made by you in the scene; this script only uses the references you assign.
public sealed class WorkerAllocationUI : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("Leave empty to find BuildingSystem automatically.")]
    [SerializeField] private BuildingSystem buildingSystem;
    [SerializeField] private GameStateManager gameStateManager;

    [Header("Your S3 Panel")]
    [Tooltip("The Panel you created for S3. It is hidden at start and shown when S3 opens.")]
    [SerializeField] private GameObject s3Panel;
    [SerializeField] private Button openButton;             // HUD button that opens S3 outside a wave
    [SerializeField] private Button closeButton;            // closes S3
    [SerializeField] private TMP_Text titleText;            // optional
    [SerializeField] private TMP_Text summaryText;          // optional: "Workers: 3 | Worker slots: 5"
    [SerializeField] private TMP_Text hintText;             // optional: tells the player what to tap next

    [Header("Rows")]
    [SerializeField] private RectTransform rowContainer;    // parent with a Vertical Layout Group
    [SerializeField] private WorkerAllocationRow rowPrefab; // prefab with WorkerAllocationRow on its root

    [Header("Row colors")]
    [SerializeField] private Color normalColor = new Color(0.10f, 0.22f, 0.28f);
    [SerializeField] private Color sourceColor = new Color(0.85f, 0.34f, 0.08f);
    [SerializeField] private Color targetColor = new Color(0.12f, 0.45f, 0.30f);

    [Header("Texts")]
    [SerializeField] private string pickHint = "Tap a building with Workers, then tap where the Worker should go.";
    [SerializeField] private string moveHint = "Moving a Worker from {0}. Tap a building with a free slot (or the same one to cancel).";

    private readonly List<WorkerAllocationRow> rows = new List<WorkerAllocationRow>();
    private BuildingSystem.Building source;

    private void Awake()
    {
        if (buildingSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            buildingSystem = FindFirstObjectByType<BuildingSystem>();
#else
            buildingSystem = FindObjectOfType<BuildingSystem>();
#endif
        }

        SetCanvasVisible(false);
    }

    private void SetCanvasVisible(bool visible)
    {
        if (s3Panel != null)
            s3Panel.SetActive(visible);
    }

    private bool CanvasVisible => s3Panel != null && s3Panel.activeSelf;

    private void OnEnable()
    {
        if (buildingSystem == null)
        {
            Debug.LogError("WorkerAllocationUI: BuildingSystem not found.", this);
            return;
        }

        buildingSystem.OnWorkerAllocationRequested += Open;
        buildingSystem.OnChanged += OnModelChanged;
        if (gameStateManager != null) gameStateManager.StateChanged += OnWaveStateChanged;

        if (openButton != null) openButton.onClick.AddListener(OpenFromButton);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (buildingSystem != null)
        {
            buildingSystem.OnWorkerAllocationRequested -= Open;
            buildingSystem.OnChanged -= OnModelChanged;
        }

        if (openButton != null) openButton.onClick.RemoveListener(OpenFromButton);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (gameStateManager != null) gameStateManager.StateChanged -= OnWaveStateChanged;
    }

    private void OpenFromButton() => Open(null);

    // Called by the HUD button (null) or by a building tap during a wave (that building is preselected).
    public void Open(BuildingSystem.Building preselected)
    {
        source = preselected != null && buildingSystem.GetWorkerCount(preselected) > 0 ? preselected : null;

        SetCanvasVisible(true);
        Rebuild();
    }

    public void Close()
    {
        source = null;

        SetCanvasVisible(false);
    }

    private void OnModelChanged()
    {
        if (CanvasVisible)
            Rebuild();
    }

    private bool CanShop => gameStateManager != null && !buildingSystem.waveActive &&
        (gameStateManager.State == GameState.AWAITING_WAVE || gameStateManager.State == GameState.UPGRADE);

    private void OnWaveStateChanged(GameState state)
    {
        if (state == GameState.WAVE_INCOMING || state == GameState.WAVE_RESOLVING || state == GameState.WAVE_COMPLETE)
            Close();
        else if (CanvasVisible) Rebuild();
    }

    private void OnRowClicked(BuildingSystem.Building b)
    {
        if (source == null)
        {
            if (buildingSystem.GetWorkerCount(b) > 0)
                source = b;
        }
        else if (source == b)
        {
            source = null; // cancel
        }
        else if (buildingSystem.TryMoveWorker(source, b))
        {
            source = null; // moved, OnChanged already refreshed the wave speed
        }
        else if (buildingSystem.GetWorkerCount(b) > 0)
        {
            source = b; // target was full: treat as picking a new source
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (buildingSystem == null || rowContainer == null || rowPrefab == null)
            return;

        foreach (var r in rows)
            if (r != null) { r.gameObject.SetActive(false); Destroy(r.gameObject); }

        rows.Clear();

        foreach (var b in buildingSystem.buildings)
        {
            if (b == null || !b.unlocked || b.IsSpa)
                continue;

            var row = Instantiate(rowPrefab, rowContainer);
            row.gameObject.SetActive(true);
            rows.Add(row);
            Fill(row, b);
        }

        if (titleText != null)
            titleText.text = CanShop ? "STAFF - HIRE & ASSIGN" : "WORKER ALLOCATION";

        if (summaryText != null)
            summaryText.text = $"Coins: {buildingSystem.coins:N0}   |   Workers: {buildingSystem.TotalWorkers}/{buildingSystem.WorkerCap()}   |   Slots: {buildingSystem.TotalWorkerSlots()}";

        if (hintText != null)
            hintText.text = source == null ? pickHint : string.Format(moveHint, source.displayName);
    }

    private void Fill(WorkerAllocationRow row, BuildingSystem.Building b)
    {
        int workers = buildingSystem.GetWorkerCount(b);
        int slots = buildingSystem.WorkerSlots(b);
        int percent = Mathf.RoundToInt(buildingSystem.ServiceMultiplier(b) * 100f);

        bool isSource = source == b;
        bool canReceive = source != null && !isSource && buildingSystem.CanAddWorker(b);
        bool canPick = source == null && workers > 0;

        if (row.nameText != null) row.nameText.text = $"{b.displayName} (Lv {b.level})";
        if (row.countText != null) row.countText.text = $"Workers {workers}/{slots}";
        if (row.speedText != null) row.speedText.text = $"Service {percent}%";

        if (row.actionText != null)
        {
            row.actionText.text =
                isSource ? "FROM (cancel)" :
                canReceive ? "MOVE HERE" :
                canPick ? "PICK" :
                source != null && !buildingSystem.CanAddWorker(b) ? "FULL" : "";
        }

        // if (row.background != null)
            // row.background.color = isSource ? sourceColor : canReceive ? targetColor : normalColor;

        if (row.button != null)
        {
            row.button.interactable = isSource || canReceive || canPick || (source != null && workers > 0);
            var captured = b;
            row.button.onClick.AddListener(() => OnRowClicked(captured));
        }
        FillHiring(row, b);
    }

    private void FillHiring(WorkerAllocationRow row, BuildingSystem.Building b)
    {
        bool capped = buildingSystem.TotalWorkers >= buildingSystem.WorkerCap();
        bool freeSlot = buildingSystem.HasFreeWorkerSlot(b);
        string workerReason = "Hiring available between waves";
        string managerReason = "Hiring available between waves";
        bool workerAllowed = CanShop && buildingSystem.CanHireWorker(b, out workerReason);
        bool managerAllowed = CanShop && buildingSystem.CanHireManager(b, out managerReason);
        bool workerOffer = CanShop && !capped && freeSlot;
        bool managerOffer = CanShop && !b.hasManager;

        if (row.hireWorkerButton != null)
        {
            row.hireWorkerButton.gameObject.SetActive(workerOffer);
            row.hireWorkerButton.interactable = workerAllowed;
            row.hireWorkerButton.onClick.AddListener(() => Hire(b, false));
        }
        if (row.hireWorkerText != null)
        {
            float nextUtilization = b.workers == 0 ? 1f : buildingSystem.Utilization(b) +
                (b.workers == 1 ? buildingSystem.utilizationSecond : buildingSystem.utilizationThird);
            int after = Mathf.RoundToInt(nextUtilization * buildingSystem.ManagerMultiplier(b) * 100f);
            row.hireWorkerText.text = $"Hire Worker - {buildingSystem.NextWorkerHireCost():N0} Coins\n" +
                (workerAllowed ? $"Service {Mathf.RoundToInt(buildingSystem.ServiceMultiplier(b) * 100f)}% -> {after}%" : workerReason);
        }
        if (row.workerUnavailableText != null)
        {
            row.workerUnavailableText.gameObject.SetActive(!workerOffer);
            row.workerUnavailableText.text = !CanShop ? "Hire Workers between waves" :
                capped ? "Upgrade buildings to open more shifts" : "No free Worker slot";
        }

        string managerTitle = buildingSystem.ManagerTitle(b);
        string multiplier = buildingSystem.managerMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        if (row.hireManagerButton != null)
        {
            row.hireManagerButton.gameObject.SetActive(managerOffer);
            row.hireManagerButton.interactable = managerAllowed;
            row.hireManagerButton.onClick.AddListener(() => Hire(b, true));
        }
        if (row.hireManagerText != null)
            row.hireManagerText.text = $"Hire {managerTitle} - {buildingSystem.managerHireCost:N0} Coins\n" +
                (managerAllowed ? $"Service x{multiplier} on every seat" : managerReason);
        if (row.managerUnavailableText != null)
        {
            row.managerUnavailableText.gameObject.SetActive(!managerOffer);
            row.managerUnavailableText.text = b.hasManager ? $"{managerTitle} hired - Service x{multiplier}" : "Hire Managers between waves";
        }
    }

    private void Hire(BuildingSystem.Building b, bool manager)
    {
        if (!CanShop) { Rebuild(); return; }
        source = null;
        bool hired = manager ? buildingSystem.TryHireManager(b) : buildingSystem.TryHireWorker(b);
        if (!hired) Rebuild(); // Successful purchases refresh through OnChanged.
    }
}

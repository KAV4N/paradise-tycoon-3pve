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
            if (r != null) Destroy(r.gameObject);

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
            titleText.text = "WORKER ALLOCATION";

        if (summaryText != null)
            summaryText.text = $"Workers: {buildingSystem.TotalWorkers}   |   Worker slots: {buildingSystem.TotalWorkerSlots()}";

        if (hintText != null)
            hintText.text = source == null ? pickHint : string.Format(moveHint, source.displayName);
    }

    private void Fill(WorkerAllocationRow row, BuildingSystem.Building b)
    {
        int workers = buildingSystem.GetWorkerCount(b);
        int slots = buildingSystem.WorkerSlots(b);
        int percent = Mathf.RoundToInt(buildingSystem.Utilization(b) * 100f);

        bool isSource = source == b;
        bool canReceive = source != null && !isSource && buildingSystem.CanAddWorker(b);
        bool canPick = source == null && workers > 0;

        if (row.nameText != null) row.nameText.text = $"{b.displayName} (Lv {b.level})";
        if (row.countText != null) row.countText.text = $"{workers}/{slots}";
        if (row.speedText != null) row.speedText.text = $"{percent}%";

        if (row.actionText != null)
        {
            row.actionText.text =
                isSource ? "FROM (cancel)" :
                canReceive ? "MOVE HERE" :
                canPick ? "PICK" :
                source != null && !buildingSystem.CanAddWorker(b) ? "FULL" : "";
        }

        if (row.background != null)
            row.background.color = isSource ? sourceColor : canReceive ? targetColor : normalColor;

        if (row.button != null)
        {
            row.button.interactable = isSource || canReceive || canPick || (source != null && workers > 0);
            var captured = b;
            row.button.onClick.AddListener(() => OnRowClicked(captured));
        }
    }
}
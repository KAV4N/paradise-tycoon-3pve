using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S3 Worker Allocation.
// Shows where every Worker stands and moves a Worker to another building - free and instant,
// in AWAITING_WAVE and during a wave (also while the wave is paused).
//
// During a wave the panel opens when a built building is tapped (BuildingSystem.OnWorkerAllocationRequested).
// Outside a wave it opens with the always-visible "Workers" button.
// Closing the panel returns to the previous screen (S2 wave screen during a wave).
//
// The UI is built at runtime (same approach as BuildingSystem), so no scene objects are needed
// except one GameObject with this component.
public sealed class WorkerAllocationUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to find BuildingSystem in the scene automatically.")]
    [SerializeField] private BuildingSystem buildingSystem;

    [Header("Open button (reference resolution 1280x720)")]
    [SerializeField] private Vector2 openButtonSize = new Vector2(200, 64);
    [Tooltip("Distance from the bottom-left corner of the screen.")]
    [SerializeField] private Vector2 openButtonOffset = new Vector2(24, 24);

    private GameObject canvasRoot, openButton, panel;
    private RectTransform listRoot;
    private TMP_Text titleText, summaryText, hintText;

    // The building a Worker is being moved FROM. Null = nothing picked yet.
    private BuildingSystem.Building source;
    private bool built;

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
    }

    private void OnEnable()
    {
        if (buildingSystem == null)
        {
            Debug.LogError("WorkerAllocationUI: BuildingSystem not found. Assign it in the Inspector.", this);
            enabled = false;
            return;
        }

        BuildUI();
        buildingSystem.OnWorkerAllocationRequested += OnBuildingTapped;
        buildingSystem.OnChanged += OnModelChanged;
    }

    private void OnDisable()
    {
        if (buildingSystem != null)
        {
            buildingSystem.OnWorkerAllocationRequested -= OnBuildingTapped;
            buildingSystem.OnChanged -= OnModelChanged;
        }
    }

    private void OnDestroy()
    {
        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    // ---------------------------------------------------------------- logic

    // Tap on a built building during a wave: open S3 with that building preselected as the source.
    private void OnBuildingTapped(BuildingSystem.Building b)
    {
        Open(b);
    }

    private void OnModelChanged()
    {
        if (panel != null && panel.activeSelf)
            Rebuild();
    }

    public void Open() => Open(null);

    public void Open(BuildingSystem.Building preselected)
    {
        if (!built)
            BuildUI();

        source = preselected != null && buildingSystem.GetWorkerCount(preselected) > 0 ? preselected : null;

        panel.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        source = null;

        if (panel != null)
            panel.SetActive(false);
    }

    private void OnRowClicked(BuildingSystem.Building b)
    {
        if (source == null)
        {
            // Step 1: pick the building the Worker leaves.
            if (buildingSystem.GetWorkerCount(b) > 0)
                source = b;
        }
        else if (source == b)
        {
            // Tapping the source again cancels the selection.
            source = null;
        }
        else if (buildingSystem.TryMoveWorker(source, b))
        {
            // Step 2 done: Worker moved (TryMoveWorker raises OnChanged -> utilization is updated live).
            source = null;
        }
        else if (buildingSystem.GetWorkerCount(b) > 0)
        {
            // Target is full but has Workers: treat the tap as picking a new source.
            source = b;
        }

        Rebuild();
    }

    // ---------------------------------------------------------------- UI content

    private void Rebuild()
    {
        if (panel == null)
            return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        int rows = 0;

        foreach (var b in buildingSystem.buildings)
        {
            if (b == null || !b.unlocked || b.IsSpa)
                continue;

            AddRow(b, rows++);
        }

        int total = buildingSystem.TotalWorkers;
        int slots = buildingSystem.TotalWorkerSlots();

        titleText.text = "WORKER ALLOCATION";
        summaryText.text = $"Workers: {total}   |   Worker slots: {slots}";

        if (source == null)
            hintText.text = "Tap a building with Workers to pick it, then tap where the Worker should go.";
        else
            hintText.text = $"Moving a Worker from {source.displayName}. Tap a building with a free slot (or the same building to cancel).";
    }

    private void AddRow(BuildingSystem.Building b, int index)
    {
        int workers = buildingSystem.GetWorkerCount(b);
        int slots = buildingSystem.WorkerSlots(b);
        int percent = Mathf.RoundToInt(buildingSystem.Utilization(b) * 100f);

        bool isSource = source == b;
        bool canReceive = source != null && source != b && buildingSystem.CanAddWorker(b);
        bool canPick = source == null && workers > 0;
        bool interactable = isSource || canReceive || canPick || (source != null && workers > 0);

        Color color =
            isSource ? new Color(0.85f, 0.34f, 0.08f) :
            canReceive ? new Color(0.12f, 0.45f, 0.30f) :
            new Color(0.10f, 0.22f, 0.28f);

        var row = MakeImage("Row " + b.id, listRoot, color);
        float rowHeight = 0.18f;
        float top = 1f - index * (rowHeight + 0.015f);
        Anchor(row.transform, 0, top - rowHeight, 1, top);

        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        button.interactable = interactable;
        var captured = b;
        button.onClick.AddListener(() => OnRowClicked(captured));

        var name = MakeLabel("Name", row.transform, 28, TextAlignmentOptions.Left);
        Anchor(name.transform, 0.03f, 0.08f, 0.45f, 0.92f);
        name.text = $"{b.displayName}  (Lv {b.level})";

        var count = MakeLabel("Count", row.transform, 28, TextAlignmentOptions.Center);
        Anchor(count.transform, 0.45f, 0.08f, 0.62f, 0.92f);
        count.text = $"{workers}/{slots}";

        var speed = MakeLabel("Speed", row.transform, 22, TextAlignmentOptions.Center);
        Anchor(speed.transform, 0.62f, 0.08f, 0.80f, 0.92f);
        speed.text = $"{percent}%";

        var action = MakeLabel("Action", row.transform, 22, TextAlignmentOptions.Right);
        Anchor(action.transform, 0.80f, 0.08f, 0.97f, 0.92f);
        action.text =
            isSource ? "FROM (cancel)" :
            canReceive ? "MOVE HERE" :
            canPick ? "PICK" :
            (source != null && workers >= slots ? "FULL" : "");
    }

    // ---------------------------------------------------------------- UI construction

    private void BuildUI()
    {
        if (built)
            return;

        built = true;

        canvasRoot = new GameObject("Worker Allocation S3",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // under the building panel (100), over the normal HUD

        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        // Always-visible open button (the only way to reach S3 outside a wave).
        var openImg = MakeImage("Workers Button", canvasRoot.transform, new Color(0.85f, 0.34f, 0.08f));
        openButton = openImg.gameObject;
        var openRect = (RectTransform)openButton.transform;
        openRect.anchorMin = openRect.anchorMax = openRect.pivot = Vector2.zero;
        openRect.sizeDelta = openButtonSize;
        openRect.anchoredPosition = openButtonOffset;

        var open = openButton.AddComponent<Button>();
        open.targetGraphic = openImg;
        open.onClick.AddListener(() => Open());

        var openLabel = MakeLabel("Label", openButton.transform, 28, TextAlignmentOptions.Center);
        Anchor(openLabel.transform, 0, 0, 1, 1);
        openLabel.text = "Workers";

        // Full-screen dimmed panel.
        var shade = MakeImage("S3 Panel", canvasRoot.transform, new Color(0, 0, 0, 0.65f));
        panel = shade.gameObject;
        Anchor(panel.transform, 0, 0, 1, 1);

        var card = MakeImage("Card", panel.transform, new Color(0.055f, 0.14f, 0.18f, 1));
        Anchor(card.transform, 0.18f, 0.08f, 0.82f, 0.92f);

        titleText = MakeLabel("Title", card.transform, 36, TextAlignmentOptions.Center);
        Anchor(titleText.transform, 0.05f, 0.88f, 0.95f, 0.98f);

        summaryText = MakeLabel("Summary", card.transform, 22, TextAlignmentOptions.Center);
        Anchor(summaryText.transform, 0.05f, 0.81f, 0.95f, 0.88f);

        var listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(card.transform, false);
        listRoot = (RectTransform)listGo.transform;
        Anchor(listRoot, 0.04f, 0.27f, 0.96f, 0.80f);

        hintText = MakeLabel("Hint", card.transform, 20, TextAlignmentOptions.Center);
        Anchor(hintText.transform, 0.05f, 0.14f, 0.95f, 0.26f);

        var closeImg = MakeImage("Close", card.transform, new Color(0.85f, 0.34f, 0.08f));
        Anchor(closeImg.transform, 0.30f, 0.03f, 0.70f, 0.12f);
        var close = closeImg.gameObject.AddComponent<Button>();
        close.targetGraphic = closeImg;
        close.onClick.AddListener(Close);

        var closeLabel = MakeLabel("Label", closeImg.transform, 26, TextAlignmentOptions.Center);
        Anchor(closeLabel.transform, 0, 0, 1, 1);
        closeLabel.text = "Close";

        panel.SetActive(false);
    }

    private static Image MakeImage(string label, Transform parent, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text MakeLabel(string label, Transform parent, int size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Anchor(Transform t, float x0, float y0, float x1, float y1)
    {
        var r = (RectTransform)t;
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}

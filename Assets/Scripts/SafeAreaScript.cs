using UnityEngine;
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Header("Edges to respect")]
    [SerializeField] private bool applyLeft = true;
    [SerializeField] private bool applyRight = true;
    [SerializeField] private bool applyTop = true;
    [SerializeField] private bool applyBottom = true;

    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private ScreenOrientation _lastOrientation;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // Cheap check each frame; only re-applies when something changed.
        if (_lastSafeArea != Screen.safeArea ||
            _lastScreenSize.x != Screen.width ||
            _lastScreenSize.y != Screen.height ||
            _lastOrientation != Screen.orientation)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;

        _lastSafeArea = safe;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;

        // Convert pixel rect to normalized anchors (0-1).
        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Optionally ignore individual edges.
        if (!applyLeft)   anchorMin.x = 0f;
        if (!applyBottom) anchorMin.y = 0f;
        if (!applyRight)  anchorMax.x = 1f;
        if (!applyTop)    anchorMax.y = 1f;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
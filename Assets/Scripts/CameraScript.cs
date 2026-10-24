using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

[RequireComponent(typeof(Camera))]
public class MobileCameraController : MonoBehaviour
{
    [Header("Map Bounds")]
    public SpriteRenderer island;
    public Rect manualBounds = new Rect(-10, -10, 20, 20);

    [Header("Zoom")]
    [Range(0.05f, 0.9f)] public float zoomInLimit = 0.35f;
    public float scrollSensitivity = 0.15f;
    public bool startZoomedOut = false;

    [Header("Pan")]
    public float inertiaDamping = 6f;
    [Range(0f, 1f)] public float inertiaStrength = 1f;
    public bool ignoreUI = true;

    private Camera cam;
    private bool dragging;
    private Vector2 lastDragScreenPos;
    private Vector3 velocity;
    private bool pinching;
    private float lastPinchDistance;
    private bool touchBlockedByUI;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        if (startZoomedOut)
            cam.orthographicSize = GetMaxZoom();

        ApplyLimits();
    }

    void Update()
    {
        int touchCount = Touch.activeTouches.Count;

        if (touchCount >= 2)
        {
            dragging = false;
            HandlePinch();
        }
        else if (touchCount == 1)
        {
            pinching = false;
            HandleTouchDrag(Touch.activeTouches[0]);
        }
        else
        {
            pinching = false;
            HandleMouse();
        }
    }

    void LateUpdate()
    {
        if (!dragging && !pinching && velocity.sqrMagnitude > 0.0001f)
        {
            transform.position += velocity * Time.deltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.zero, inertiaDamping * Time.deltaTime);
        }

        ApplyLimits();
    }

    void HandleTouchDrag(Touch t)
    {
        switch (t.phase)
        {
            case TouchPhase.Began:
                touchBlockedByUI = ignoreUI && IsOverUI(t.touchId);
                if (touchBlockedByUI) return;
                BeginDrag(t.screenPosition);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (touchBlockedByUI) return;
                if (!dragging) BeginDrag(t.screenPosition);
                Drag(t.screenPosition);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                dragging = false;
                touchBlockedByUI = false;
                break;
        }
    }

    void HandlePinch()
    {
        Touch t0 = Touch.activeTouches[0];
        Touch t1 = Touch.activeTouches[1];

        float distance = Vector2.Distance(t0.screenPosition, t1.screenPosition);
        Vector2 center = (t0.screenPosition + t1.screenPosition) * 0.5f;

        if (!pinching)
        {
            if (ignoreUI && (IsOverUI(t0.touchId) || IsOverUI(t1.touchId))) return;

            pinching = true;
            lastPinchDistance = distance;
            velocity = Vector3.zero;
            return;
        }

        if (distance < 1f || lastPinchDistance < 1f) return;

        float ratio = lastPinchDistance / distance;
        ZoomAt(center, cam.orthographicSize * ratio);
        lastPinchDistance = distance;
    }

    void HandleMouse()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            dragging = false;
            return;
        }

        Vector2 mousePos = mouse.position.ReadValue();

        float rawScroll = mouse.scroll.ReadValue().y;
        float scroll = Mathf.Abs(rawScroll) > 0.01f ? Mathf.Sign(rawScroll) : 0f;
        if (Mathf.Abs(scroll) > 0.01f && !(ignoreUI && IsOverUI(-1)))
        {
            float newSize = cam.orthographicSize * (1f - scroll * scrollSensitivity);
            ZoomAt(mousePos, newSize);
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            touchBlockedByUI = ignoreUI && IsOverUI(-1);
            if (!touchBlockedByUI) BeginDrag(mousePos);
        }
        else if (mouse.leftButton.isPressed && dragging)
        {
            Drag(mousePos);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
            touchBlockedByUI = false;
        }
#else
        dragging = false;
#endif
    }

    void BeginDrag(Vector2 screenPos)
    {
        dragging = true;
        lastDragScreenPos = screenPos;
        velocity = Vector3.zero;
    }

    void Drag(Vector2 screenPos)
    {
        Vector3 before = cam.ScreenToWorldPoint(lastDragScreenPos);
        Vector3 after = cam.ScreenToWorldPoint(screenPos);
        Vector3 delta = before - after;
        delta.z = 0f;

        transform.position += delta;
        lastDragScreenPos = screenPos;

        if (Time.deltaTime > 0f)
        {
            Vector3 instantVelocity = delta / Time.deltaTime;
            velocity = Vector3.Lerp(velocity, instantVelocity * inertiaStrength, 0.5f);
        }
    }

    void ZoomAt(Vector2 screenPoint, float targetSize)
    {
        Vector3 before = cam.ScreenToWorldPoint(screenPoint);

        cam.orthographicSize = Mathf.Clamp(targetSize, GetMinZoom(), GetMaxZoom());

        Vector3 after = cam.ScreenToWorldPoint(screenPoint);
        Vector3 shift = before - after;
        shift.z = 0f;
        transform.position += shift;

        ApplyLimits();
    }

    Bounds GetMapBounds()
    {
        if (island != null)
            return island.bounds;

        return new Bounds(manualBounds.center, manualBounds.size);
    }

    float GetMaxZoom()
    {
        Bounds b = GetMapBounds();
        float maxByHeight = b.extents.y;
        float maxByWidth = b.extents.x / cam.aspect;
        return Mathf.Min(maxByHeight, maxByWidth);
    }

    float GetMinZoom()
    {
        return GetMaxZoom() * zoomInLimit;
    }

    void ApplyLimits()
    {
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, GetMinZoom(), GetMaxZoom());

        Bounds b = GetMapBounds();
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        Vector3 p = transform.position;
        p.x = (minX > maxX) ? b.center.x : Mathf.Clamp(p.x, minX, maxX);
        p.y = (minY > maxY) ? b.center.y : Mathf.Clamp(p.y, minY, maxY);
        transform.position = p;
    }

    static bool IsOverUI(int pointerId)
    {
        if (EventSystem.current == null) return false;
        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Bounds b = island != null ? island.bounds : new Bounds(manualBounds.center, manualBounds.size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}
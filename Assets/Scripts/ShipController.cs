using System;
using UnityEngine;

public class ShipController : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite fullSprite;     // ship with customers (arriving and leaving)
    [SerializeField] private Sprite emptySprite;    // ship after all customers got out (waiting at dock)
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 5;

    [Header("Coordinates (world space)")]
    [SerializeField] private Vector2 spawnPosition = new Vector2(-10f, 0f); // where the ship appears
    [SerializeField] private Vector2 dockPosition  = new Vector2(0f, 0f);   // where customers get out
    [SerializeField] private Vector2 exitPosition  = new Vector2(10f, 0f);  // where the ship leaves to

    [Header("Rotation (degrees, updates live in Play mode)")]
    [SerializeField, Range(-180f, 180f)] private float arriveRotation = 90f; // sailing spawn -> dock
    [SerializeField, Range(-180f, 180f)] private float exitRotation = 90f;   // sailing dock -> exit

    [Header("Flip (updates live in Play mode)")]
    [SerializeField] private bool arriveFlipX = false;  // spawn -> dock (and waiting at dock)
    [SerializeField] private bool arriveFlipY = false;
    [SerializeField] private bool exitFlipX = false;    // dock -> exit
    [SerializeField] private bool exitFlipY = false;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float speed = 3f;
    [SerializeField] private bool hideAtExit = true;   // hide the ship when it reaches the exit

    private Transform ship;
    private SpriteRenderer shipRenderer;
    private Vector3 target;
    private bool moving;
    private bool paused;
    private bool leaving;

    public bool HasArrived { get; private set; }
    public event Action Arrived;   // reached the dock
    public event Action Exited;    // reached the exit position

    public void SetPaused(bool value) => paused = value;

    private Vector3 SpawnPos => new Vector3(spawnPosition.x, spawnPosition.y, 0f);
    private Vector3 DockPos  => new Vector3(dockPosition.x, dockPosition.y, 0f);
    private Vector3 ExitPos  => new Vector3(exitPosition.x, exitPosition.y, 0f);
    private Quaternion CurrentRotation => Quaternion.Euler(0f, 0f, leaving ? exitRotation : arriveRotation);

    private void CreateShip()
    {
        if (fullSprite == null)
        {
            Debug.LogError("ShipController: assign the Full Sprite.", this);
            return;
        }
        var go = new GameObject("Ship");
        go.transform.position = SpawnPos;
        shipRenderer = go.AddComponent<SpriteRenderer>();
        shipRenderer.sprite = fullSprite;
        shipRenderer.sortingLayerName = sortingLayer;
        shipRenderer.sortingOrder = sortingOrder;
        ship = go.transform;
    }

    private void ShowSprite(Sprite sprite)
    {
        if (shipRenderer != null && sprite != null) shipRenderer.sprite = sprite;
    }

    // Wave start: appear at spawn (full) and sail to the dock.
    public void Arrive()
    {
        if (ship == null) CreateShip();
        if (ship == null) return;

        ship.position = SpawnPos;
        ship.gameObject.SetActive(true);
        ShowSprite(fullSprite);
        HasArrived = false;
        leaving = false;
        target = DockPos;
        moving = true;
    }

    // All customers are out of the ship: switch to the empty sprite.
    public void SetEmpty()
    {
        ShowSprite(emptySprite);
    }

    // Wave end: switch back to the full sprite and sail to the exit.
    public void Depart()
    {
        if (ship == null) return;

        ShowSprite(fullSprite);
        HasArrived = false;
        leaving = true;
        target = ExitPos;
        moving = true;
    }

    private void Update()
    {
        if (ship == null) return;

        // Applied every frame so changes in the Inspector show up immediately.
        ship.rotation = CurrentRotation;
        if (shipRenderer != null)
        {
            shipRenderer.flipX = leaving ? exitFlipX : arriveFlipX;
            shipRenderer.flipY = leaving ? exitFlipY : arriveFlipY;
        }

        if (!moving || paused) return;

        ship.position = Vector3.MoveTowards(ship.position, target, speed * Time.deltaTime);

        if ((ship.position - target).sqrMagnitude < 0.0001f)
        {
            moving = false;

            if (leaving)
            {
                Exited?.Invoke();
                if (hideAtExit) ship.gameObject.SetActive(false);
            }
            else
            {
                HasArrived = true;
                Arrived?.Invoke();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;  Gizmos.DrawWireSphere(SpawnPos, 0.5f);
        Gizmos.color = Color.red;    Gizmos.DrawWireSphere(DockPos, 0.5f);
        Gizmos.color = Color.blue;   Gizmos.DrawWireSphere(ExitPos, 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(SpawnPos, DockPos);
        Gizmos.DrawLine(DockPos, ExitPos);
    }
}
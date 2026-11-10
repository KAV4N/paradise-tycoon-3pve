using System.Collections.Generic;
using UnityEngine;

public sealed class GuestVisualSpawner : MonoBehaviour
{
    [SerializeField] private GuestVisualMovement guestPrefab;
    [SerializeField] private Material guestMaterial;
    [SerializeField] private Transform[] routePoints;
    [SerializeField] private GuestPathNetwork.Link[] routeLinks;
    [SerializeField] private WaveUIController waveController;
    [SerializeField, Min(0)] private int guestCount = 5;
    [SerializeField, Min(0.1f)] private float spawnInterval = 1.8f;
    private readonly List<GuestVisualMovement> guests = new List<GuestVisualMovement>();
    private GuestPathNetwork network;
    private Texture2D texture;
    private Sprite circle;
    private bool active;
    private float spawnTimer;
    private static readonly Color[] Shirts = {
        new Color(0.95f, 0.25f, 0.23f), new Color(0.16f, 0.45f, 0.95f),
        new Color(1f, 0.72f, 0.12f), new Color(0.67f, 0.26f, 0.85f), new Color(0.12f, 0.72f, 0.48f)
    };

    private void OnEnable()
    {
        if (guestPrefab == null || guestMaterial == null || waveController == null || routePoints == null) return;
        foreach (var point in routePoints) if (point == null) return;
        try { network = new GuestPathNetwork(routePoints.Length, routeLinks); }
        catch (System.ArgumentException exception) { Debug.LogWarning(exception.Message, this); return; }
        if (circle == null) CreateCircle();
        waveController.WaveStateChanged += SetWaveActive;
        SetWaveActive(waveController.IsWaveInProgress);
    }

    private void SetWaveActive(bool value)
    {
        if (active == value) return;
        active = value;
        spawnTimer = 0;
        if (!active) ClearGuests();
    }

    private void Update()
    {
        bool paused = waveController != null && waveController.IsWavePaused;
        foreach (var existingGuest in guests) if (existingGuest != null) existingGuest.enabled = !paused;
        if (paused) return;
        if (!active || guests.Count >= guestCount) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0) return;
        var guest = Instantiate(guestPrefab, transform);
        guest.name = "Guest " + (guests.Count + 1);
        guest.Initialize(routePoints, network, Random.Range(0, int.MaxValue), circle, guestMaterial, Shirts[guests.Count % Shirts.Length]);
        guests.Add(guest);
        spawnTimer = Mathf.Max(0.1f, spawnInterval);
    }

    private void CreateCircle()
    {
        const int size = 64;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Guest placeholder circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * 0.5f - 1 - distance));
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        circle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
    }

    private void ClearGuests()
    {
        foreach (var guest in guests)
            if (guest != null) { guest.gameObject.SetActive(false); Destroy(guest.gameObject); }
        guests.Clear();
    }

    private void OnDisable()
    {
        if (waveController != null) waveController.WaveStateChanged -= SetWaveActive;
        active = false;
        ClearGuests();
    }

    private void OnDestroy()
    {
        if (circle != null) Destroy(circle);
        if (texture != null) Destroy(texture);
    }

    private void OnDrawGizmosSelected()
    {
        if (routePoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (var point in routePoints) if (point != null) Gizmos.DrawSphere(point.position, 0.12f);
        if (routeLinks == null) return;
        foreach (var link in routeLinks)
            if (link.from >= 0 && link.to >= 0 && link.from < routePoints.Length && link.to < routePoints.Length &&
                routePoints[link.from] != null && routePoints[link.to] != null)
                Gizmos.DrawLine(routePoints[link.from].position, routePoints[link.to].position);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Decorative movement only. Guests have no needs, service state or rewards.
public sealed class GuestVisualMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 1.4f;
    [SerializeField, Min(0f)] private float endpointPause = 1.2f;
    private Transform[] points;
    private GuestPathNetwork network;
    private System.Random random;
    private readonly Queue<int> destinations = new Queue<int>();
    private readonly Queue<int> steps = new Queue<int>();
    private Transform appearance;
    private SortingGroup sorting;
    private int current;
    private int target;
    private float pause;
    private float phase;

    public void Initialize(Transform[] route, GuestPathNetwork paths, int seed, Sprite circle, Material material, Color shirt)
    {
        points = route; network = paths; random = new System.Random(seed);
        transform.position = points[0].position;
        sorting = gameObject.AddComponent<SortingGroup>();
        // Dedicated top world layer: never change terrain or building sorting orders.
        sorting.sortingLayerName = "Guests";
        AddPart("Shadow", transform, circle, material, new Color(0, 0, 0, 0.2f),
            new Vector2(0, 0.02f), new Vector2(0.43f, 0.13f), 0);
        appearance = new GameObject("Appearance").transform;
        appearance.SetParent(transform, false);
        AddPart("Body", appearance, circle, material, shirt, new Vector2(0, 0.25f), new Vector2(0.32f, 0.44f), 1);
        AddPart("Head", appearance, circle, material, new Color(0.94f, 0.72f, 0.49f),
            new Vector2(0, 0.56f), new Vector2(0.27f, 0.27f), 2);
        AddPart("Facing", appearance, circle, material, new Color(0.22f, 0.15f, 0.13f),
            new Vector2(0.075f, 0.58f), new Vector2(0.035f, 0.045f), 3);
        ChooseDestination();
        RefreshDepth();
    }

    private static void AddPart(string name, Transform parent, Sprite sprite, Material material,
        Color color, Vector2 position, Vector2 size, int order)
    {
        var part = new GameObject(name, typeof(SpriteRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = part.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.color = color;
        renderer.sortingOrder = order;
    }

    private void Update()
    {
        if (points == null || points[target] == null) return;
        if (pause > 0) { pause -= Time.deltaTime; appearance.localPosition = Vector3.zero; return; }
        Vector3 destination = points[target].position;
        float horizontal = destination.x - transform.position.x;
        if (Mathf.Abs(horizontal) > 0.01f) appearance.localScale = new Vector3(Mathf.Sign(horizontal), 1, 1);
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        phase += Time.deltaTime * 9;
        appearance.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.025f, 0);
        RefreshDepth();
        if ((transform.position - destination).sqrMagnitude > 0.0001f) return;
        current = target;
        if (steps.Count > 0) target = steps.Dequeue();
        else { pause = endpointPause * (0.5f + (float)random.NextDouble()); ChooseDestination(); }
    }

    private void ChooseDestination()
    {
        if (destinations.Count == 0)
            foreach (int point in network.CreateTour(current, random)) destinations.Enqueue(point);
        foreach (int point in network.FindPath(current, destinations.Dequeue())) steps.Enqueue(point);
        target = steps.Dequeue();
    }

    // Y sorting only orders guests against each other, within the Guests layer.
    private void RefreshDepth() => sorting.sortingOrder = Mathf.Clamp(-Mathf.RoundToInt(transform.position.y * 100), -30000, 30000);
}

using UnityEngine;

// Keeps the ShoreWaves quad exactly on top of your island sprite
// (slightly bigger, so the waves have room around the island)
// and fixes the draw order: Water -> Waves -> Island -> (optional) foam on the sand.
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class ShoreWavesFit : MonoBehaviour
{
    [Tooltip("Drag your Island object (the one with the Sprite Renderer) here")]
    public SpriteRenderer island;

    [Tooltip("Drag IslandShoreSDF.png here")]
    public Texture2D distanceMap;

    [Tooltip("Drag your Water quad here, so it is always drawn behind the waves")]
    public MeshRenderer water;

    [Tooltip("Empty border baked into IslandShoreSDF.png. Leave at 50.")]
    public int padPixels = 50;

    [Tooltip("How far behind the island the waves sit (Z)")]
    public float zOffset = 0.1f;

    [Tooltip("Tick this on the SECOND quad: it draws the foam washing up on the sand, above the island")]
    public bool drawOnTop = false;

    MaterialPropertyBlock block;

    void OnEnable()
    {
        Fit();
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void LateUpdate()
    {
        Fit();
    }

    void Fit()
    {
        if (island == null || island.sprite == null || distanceMap == null) return;

        Sprite sp = island.sprite;
        float ppu = sp.pixelsPerUnit;

        // size and center of the full sprite rectangle
        Vector2 size = sp.rect.size / ppu;
        Vector2 centerLocal = (sp.rect.size * 0.5f - sp.pivot) / ppu;

        Transform it = island.transform;
        Vector3 center = it.TransformPoint(centerLocal);
        Vector3 s = it.lossyScale;

        // the distance map has an extra border, so the quad is a bit larger
        float rx = distanceMap.width / (float)(distanceMap.width - 2 * padPixels);
        float ry = distanceMap.height / (float)(distanceMap.height - 2 * padPixels);

        transform.position = new Vector3(center.x, center.y, it.position.z + (drawOnTop ? -zOffset : zOffset));
        transform.rotation = it.rotation;
        transform.localScale = new Vector3(Mathf.Abs(size.x * s.x) * rx, Mathf.Abs(size.y * s.y) * ry, 1f);

        // draw order: water (-2) -> waves (-1) -> island
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sortingLayerID = island.sortingLayerID;
        mr.sortingOrder = island.sortingOrder + (drawOnTop ? 1 : -1);

        if (water != null && !drawOnTop)
        {
            water.sortingLayerID = island.sortingLayerID;
            water.sortingOrder = island.sortingOrder - 2;
        }

        if (mr.sharedMaterial != null && mr.sharedMaterial.GetTexture("_SdfTex") != distanceMap)
            mr.sharedMaterial.SetTexture("_SdfTex", distanceMap);

        // tell the shader whether this quad is the "on top" one
        if (block == null) block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        block.SetFloat("_Overlay", drawOnTop ? 1f : 0f);
        mr.SetPropertyBlock(block);
    }
}

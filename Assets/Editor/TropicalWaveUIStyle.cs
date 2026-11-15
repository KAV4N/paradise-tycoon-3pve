using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Applies the existing game artwork to the scene and the shared need-row prefab.
public static class TropicalWaveUIStyle
{
    private static readonly Color Gold = new Color(1f, 0.85f, 0.43f);
    private static readonly Color Cream = new Color(1f, 0.96f, 0.85f);
    private static readonly Color Muted = new Color(0.76f, 0.9f, 0.85f);
    private static readonly Color Teal = new Color(0.035f, 0.16f, 0.19f);
    private static readonly Color Mint = new Color(0.33f, 0.82f, 0.63f);
    private static Sprite bamboo, panelArt;

    [MenuItem("Tools/Waves/Apply Tropical Wave UI Style")]
    private static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before styling the scene.");
        var controller = UnityEngine.Object.FindAnyObjectByType<WaveUIController>(FindObjectsInactive.Include);
        if (controller == null) throw new InvalidOperationException("Open the Game scene first.");
        bamboo = SpriteAt("Assets/Textures/UI/Frames/valueablesFrame.png");
        panelArt = SpriteAt("Assets/Textures/UI/Frames/upgradeFrame.png");
        var data = new SerializedObject(controller);
        var badge = Get<GameObject>(data, "waveStatusIndicatorContainer");
        Frame(badge.GetComponent<Image>(), bamboo);
        Undo.RecordObject(badge.GetComponent<RectTransform>(), "Tropical status badge");
        badge.GetComponent<RectTransform>().sizeDelta = new Vector2(480, 82);
        var badgeLabel = Get<TMP_Text>(data, "waveStatusIndicatorText");
        Text(badgeLabel, 26, Gold, TextAlignmentOptions.Center);
        Stretch(badgeLabel.rectTransform, 38, 10, 38, 10);

        var panel = Get<GameObject>(data, "sidePanel");
        Frame(panel.GetComponent<Image>(), panelArt);
        var heading = Get<TMP_Text>(data, "waveStatusTitleText");
        Place(heading.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -189), new Vector2(424, 60));
        Text(heading, 27, Gold, TextAlignmentOptions.Center);
        heading.textWrappingMode = TextWrappingModes.Normal;
        heading.enableAutoSizing = true; heading.fontSizeMin = 20; heading.fontSizeMax = 27;

        var guestLabel = Get<TMP_Text>(data, "guestCountText");
        var guestBand = panel.transform.Find("GuestCountText") as RectTransform;
        if (guestBand != null)
        {
            Place(guestBand, new Vector2(0.5f, 1), new Vector2(0, -253), new Vector2(432, 50));
            Frame(guestBand.GetComponent<Image>(), bamboo);
            if (guestLabel.rectTransform != guestBand) Stretch(guestLabel.rectTransform, 16, 6, 16, 6);
        }
        Text(guestLabel, 23, Cream, TextAlignmentOptions.Center);
        var progressLabel = Get<TMP_Text>(data, "progressLabel");
        Place(progressLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(-45, -306), new Vector2(355, 32));
        Text(progressLabel, 21, Muted, TextAlignmentOptions.MidlineLeft);
        var progressCount = Get<TMP_Text>(data, "satisfactionScoreText");
        Place(progressCount.rectTransform, new Vector2(0.5f, 1), new Vector2(167, -306), new Vector2(90, 32));
        Text(progressCount, 23, Gold, TextAlignmentOptions.MidlineRight);
        var fill = Get<Image>(data, "satisfactionFill");
        var bar = (RectTransform)fill.transform.parent;
        Place(bar, new Vector2(0.5f, 1), new Vector2(0, -334), new Vector2(424, 12));
        Bar(bar.GetComponent<Image>()); Undo.RecordObject(fill, "Tropical wave progress"); fill.color = Mint;

        var rows = Get<Transform>(data, "needItemsContainer");
        Stretch((RectTransform)rows, 90, 360, 90, 140);
        var layout = rows.GetComponent<VerticalLayoutGroup>();
        Undo.RecordObject(layout, "Space tropical need cards");
        layout.spacing = 12; layout.childControlHeight = false; layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        Arrow(Get<Button>(data, "openSidePanelArrowButton"), "<");
        var close = Get<Button>(data, "closeSidePanelButton");
        Place((RectTransform)close.transform, new Vector2(0, 0.5f), new Vector2(-24, 0), new Vector2(72, 144));
        Arrow(close, ">");
        var feedback = Get<GameObject>(data, "feedbackContainer");
        if (feedback != null)
        {
            var background = feedback.GetComponent<Image>();
            if (background != null) { Undo.RecordObject(background, "Tropical guest feedback"); background.color = Teal; }
            foreach (var text in feedback.GetComponentsInChildren<TMP_Text>(true)) Text(text, 22, Cream, text.alignment);
        }

        var prefab = Get<GameObject>(data, "needItemPrefab");
        string path = AssetDatabase.GetAssetPath(prefab);
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            StyleNeed(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        foreach (var component in controller.GetComponentsInChildren<Component>(true))
            if (component != null) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(badge); EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = panel;
        Debug.Log("Tropical style applied to the wave badge, needs panel, need-row prefab and both arrows.", controller);
    }

    private static void StyleNeed(GameObject root)
    {
        Frame(root.GetComponent<Image>(), bamboo);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(440, 100);
        var item = root.GetComponent<NeedItemUI>(); var data = new SerializedObject(item);
        data.FindProperty("normalBarColor").colorValue = Mint;
        data.FindProperty("warningBarColor").colorValue = new Color(1f, 0.52f, 0.3f);
        data.FindProperty("normalTextColor").colorValue = Cream;
        data.FindProperty("detailTextColor").colorValue = Muted;
        data.ApplyModifiedPropertiesWithoutUndo();
        var icon = Get<Image>(data, "needIcon");
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(49, 0), new Vector2(48, 48));
        icon.preserveAspect = true;
        var name = Get<TMP_Text>(data, "needNameText");
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(200, -25), new Vector2(190, 28));
        Text(name, 24, Gold, TextAlignmentOptions.MidlineLeft);
        var count = Get<TMP_Text>(data, "progressText");
        Place(count.rectTransform, new Vector2(1, 1), new Vector2(-52, -25), new Vector2(76, 28));
        Text(count, 23, Cream, TextAlignmentOptions.MidlineRight);
        var fill = Get<Image>(data, "progressBarFill");
        var bar = (RectTransform)fill.transform.parent;
        Place(bar, new Vector2(0.5f, 0.5f), new Vector2(28, -4), new Vector2(290, 10));
        Bar(bar.GetComponent<Image>()); fill.color = Mint;
        var warning = Get<GameObject>(data, "warningContainer");
        Place(warning.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(28, 30), new Vector2(290, 22));
        var detail = Get<TMP_Text>(data, "warningText");
        Stretch(detail.rectTransform, 0, 0, 0, 0);
        Text(detail, 20, Muted, TextAlignmentOptions.MidlineLeft);
        detail.enableAutoSizing = true; detail.fontSizeMin = 17; detail.fontSizeMax = 20;
    }

    private static void Arrow(Button button, string direction)
    {
        var rect = (RectTransform)button.transform;
        Undo.RecordObject(rect, "Tropical panel arrow"); rect.sizeDelta = new Vector2(72, 144);
        Frame(button.GetComponent<Image>(), bamboo);
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) { Text(text, 45, Gold, TextAlignmentOptions.Center); Stretch(text.rectTransform, 10, 10, 10, 10); text.text = direction; }
        Undo.RecordObject(button, "Tropical arrow states");
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, 0.92f, 0.7f);
        colors.selectedColor = Color.white; colors.pressedColor = new Color(0.76f, 0.9f, 0.8f);
        colors.disabledColor = new Color(1, 1, 1, 0.5f); button.colors = colors;
    }
    private static void Frame(Image image, Sprite art)
    {
        if (image == null) return;
        Undo.RecordObject(image, "Use existing tropical frame"); image.sprite = art; image.type = Image.Type.Simple; image.color = Color.white;
    }
    private static void Bar(Image image)
    {
        Undo.RecordObject(image, "Tropical progress track"); image.color = Teal;
        var edge = image.GetComponent<Outline>(); if (edge == null) edge = Undo.AddComponent<Outline>(image.gameObject);
        Undo.RecordObject(edge, "Gold progress edge"); edge.effectColor = new Color(0.83f, 0.66f, 0.3f, 0.65f); edge.effectDistance = new Vector2(1, -1);
    }
    private static void Text(TMP_Text text, int size, Color color, TextAlignmentOptions alignment)
    {
        Undo.RecordObject(text, "Tropical wave typography");
        text.color = color; text.fontSize = size; text.alignment = alignment;
        text.characterSpacing = 0; text.wordSpacing = 0; text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Undo.RecordObject(rect, "Lay out tropical wave UI"); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        Undo.RecordObject(rect, "Inset tropical UI content"); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static T Get<T>(SerializedObject data, string field) where T : UnityEngine.Object =>
        data.FindProperty(field).objectReferenceValue as T;
    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
}

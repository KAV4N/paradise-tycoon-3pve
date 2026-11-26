using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Author real, editable scene objects using the project's existing artwork.
public static class WaveTaskSetup
{
    private static readonly Color Gold = new Color(1, 0.85f, 0.43f);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Waves/Use Compact Top Forecast")]
    private static void CompactTopForecast()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before editing the scene.");
        var view = UnityEngine.Object.FindAnyObjectByType<WavePreparationUI>(FindObjectsInactive.Include);
        if (view == null) throw new InvalidOperationException("Add the forecast UI first.");
        var data = new SerializedObject(view);
        var panel = ((GameObject)data.FindProperty("forecastRoot").objectReferenceValue).GetComponent<RectTransform>();
        Undo.RecordObject(panel, "Move forecast to Home status position");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1);
        panel.anchoredPosition = new Vector2(0, -75);
        panel.sizeDelta = new Vector2(1000, 110);
        var title = (TMP_Text)data.FindProperty("forecastTitle").objectReferenceValue;
        Undo.RecordObjects(new UnityEngine.Object[] { title, title.rectTransform }, "Compact forecast title");
        title.fontSize = 22;
        title.rectTransform.anchoredPosition = new Vector2(0, 25);
        title.rectTransform.sizeDelta = new Vector2(900, 32);
        var row = (RectTransform)panel.Find("Forecast Need Row");
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        Undo.RecordObjects(new UnityEngine.Object[] { row, layout }, "Compact forecast row");
        row.anchoredPosition = new Vector2(0, -20); row.sizeDelta = new Vector2(900, 44);
        layout.spacing = 8;
        var slots = data.FindProperty("forecastNeeds");
        for (int i = 0; i < slots.arraySize; i++)
        {
            var entry = slots.GetArrayElementAtIndex(i);
            var slot = ((GameObject)entry.FindPropertyRelative("root").objectReferenceValue).GetComponent<RectTransform>();
            var label = (TMP_Text)entry.FindPropertyRelative("label").objectReferenceValue;
            var icon = (RectTransform)slot.Find("Need Icon");
            Undo.RecordObjects(new UnityEngine.Object[] { slot, label, label.rectTransform, icon }, "Compact forecast need");
            slot.sizeDelta = new Vector2(170, 44);
            icon.sizeDelta = new Vector2(32, 32); icon.anchoredPosition = new Vector2(-64, 0);
            label.rectTransform.sizeDelta = new Vector2(130, 40);
            label.rectTransform.anchoredPosition = new Vector2(20, 0);
            label.fontSize = label.fontSizeMax = 22; label.fontSizeMin = 18;
            EditorUtility.SetDirty(slot); EditorUtility.SetDirty(label); EditorUtility.SetDirty(label.rectTransform); EditorUtility.SetDirty(icon);
        }
        var waveUI = UnityEngine.Object.FindAnyObjectByType<WaveUIController>();
        var statePanel = new SerializedObject(waveUI).FindProperty("waveStatusIndicatorContainer").objectReferenceValue as GameObject;
        if (statePanel != null) { Undo.RecordObject(statePanel, "Replace Home ready label with forecast"); statePanel.SetActive(false); }
        EditorUtility.SetDirty(panel); EditorUtility.SetDirty(title); EditorUtility.SetDirty(title.rectTransform);
        EditorUtility.SetDirty(row); EditorUtility.SetDirty(layout);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
    }

    [MenuItem("Tools/Waves/Add Forecast UI")]
    private static void AddUI()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before editing the scene.");
        var existing = UnityEngine.Object.FindAnyObjectByType<WavePreparationUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            UpdateLayout(existing);
            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            Selection.activeGameObject = existing.gameObject;
            return;
        }
        var waveUI = UnityEngine.Object.FindAnyObjectByType<WaveUIController>();
        var wallet = UnityEngine.Object.FindAnyObjectByType<BuildingSystem>();
        if (waveUI == null || wallet == null) throw new InvalidOperationException("Open the Game scene first.");
        font = wallet.energyHud != null ? wallet.energyHud.font : TMP_Settings.defaultFontAsset;
        Transform parent = waveUI.transform.Find("SafeArea");
        if (parent == null) parent = waveUI.transform;
        var root = Rect("Wave Forecast", parent, Vector2.zero, Vector2.zero);
        Stretch(root);
        var view = root.gameObject.AddComponent<WavePreparationUI>();
        var serialized = new SerializedObject(view);

        var forecast = Frame("Next Wave Forecast", root, new Vector2(1180, 150), new Vector2(0, 430), "Frames/valueablesFrame.png");
        forecast.anchorMin = forecast.anchorMax = new Vector2(0.5f, 0);
        Ref(serialized, "forecastRoot", forecast.gameObject);
        Ref(serialized, "forecastTitle", Text("Forecast Title", forecast, new Vector2(1050, 40), new Vector2(0, 36), 26, Gold));
        var types = new[] { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView };
        var names = new[] { "Rooms_icon.png", "Drinks_Icon.png", "Food_Icon.png", "Relax_icon.png", "OceanView_Icon.png" };
        var slots = serialized.FindProperty("forecastNeeds"); slots.arraySize = types.Length;
        for (int i = 0; i < types.Length; i++)
        {
            var slot = Rect(types[i].GetDisplayName(), forecast, new Vector2(210, 60), new Vector2(-444 + i * 222, -23));
            Icon("Need Icon", slot, new Vector2(46, 46), new Vector2(-77, 0), "Icons/Needs/" + names[i]);
            var label = Text("Demand", slot, new Vector2(155, 52), new Vector2(25, 0), 24, i == 4 ? Gold : Color.white);
            label.enableAutoSizing = true; label.fontSizeMin = 19; label.fontSizeMax = 24;
            var entry = slots.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("type").intValue = (int)types[i];
            entry.FindPropertyRelative("root").objectReferenceValue = slot.gameObject;
            entry.FindPropertyRelative("label").objectReferenceValue = label;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        UpdateLayout(view);
        var model = UnityEngine.Object.FindAnyObjectByType<WaveModelComponent>();
        var services = UnityEngine.Object.FindAnyObjectByType<WaveServicesComponent>();
        if (model != null && services != null) view.RenderForecast(services.Forecast(model.Get(1)));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add wave forecast scene UI");
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Forecast UI added. Assign it on Game State Manager in the Inspector.", view);
    }

    private static void UpdateLayout(WavePreparationUI view)
    {
        var data = new SerializedObject(view);
        var forecast = (GameObject)data.FindProperty("forecastRoot").objectReferenceValue;
        var row = forecast.transform.Find("Forecast Need Row") as RectTransform;
        if (row == null)
        {
            row = Rect("Forecast Need Row", forecast.transform, new Vector2(1100, 65), new Vector2(0, -23));
            Undo.RegisterCreatedObjectUndo(row.gameObject, "Center forecast needs");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }
        var slots = data.FindProperty("forecastNeeds");
        var names = new[] { "Rooms_icon.png", "Drinks_Icon.png", "Food_Icon.png", "Relax_icon.png", "OceanView_Icon.png" };
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = (GameObject)slots.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue;
            if (slot.transform.parent != row) Undo.SetTransformParent(slot.transform, row, "Arrange forecast needs");
            var icon = slot.transform.Find("Need Icon").GetComponent<RawImage>();
            SetIcon(icon, "Icons/Needs/" + names[i]);
            EditorUtility.SetDirty(icon);
        }
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static void SetIcon(RawImage icon, string asset)
    {
        string path = "Assets/Textures/UI/" + asset;
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        icon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (sprite != null)
        {
            var crop = sprite.rect;
            icon.uvRect = new Rect(crop.x / icon.texture.width, crop.y / icon.texture.height,
                crop.width / icon.texture.width, crop.height / icon.texture.height);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static RectTransform Frame(string name, Transform parent, Vector2 size, Vector2 position, string asset)
    {
        var rect = Rect(name, parent, size, position);
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/" + asset);
        if (image.texture == null) throw new InvalidOperationException("Missing UI art: " + asset);
        image.raycastTarget = false; return rect;
    }
    private static void Icon(string name, Transform parent, Vector2 size, Vector2 position, string asset)
    {
        var rect = Frame(name, parent, size, position, asset);
        rect.gameObject.GetComponent<RawImage>().raycastTarget = false;
    }
    private static TMP_Text Text(string name, Transform parent, Vector2 size, Vector2 position, int pointSize, Color color)
    {
        var rect = Rect(name, parent, size, position); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = pointSize; text.color = color; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow; return text;
    }
    private static void Ref(SerializedObject target, string name, UnityEngine.Object value) =>
        target.FindProperty(name).objectReferenceValue = value;
}

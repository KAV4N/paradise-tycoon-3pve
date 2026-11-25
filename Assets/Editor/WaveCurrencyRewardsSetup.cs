using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Scene-authoring only: reuse the result window's existing rows and fonts.
public static class WaveCurrencyRewardsSetup
{
    [MenuItem("Tools/Waves/Configure Coins and Reputation Results")]
    private static void Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var view = UnityEngine.Object.FindAnyObjectByType<WaveCompleteUI>(FindObjectsInactive.Include);
        if (view == null) throw new InvalidOperationException("Open the Game scene first.");
        var data = new SerializedObject(view);
        var energy = (TMP_Text)data.FindProperty("energyText").objectReferenceValue;
        var template = energy.transform.parent.gameObject;
        var card = (RectTransform)template.transform.parent;
        var coins = Row(card, template, "CoinsRow", "Coins", "+352", 4);
        var reputation = Row(card, template, "ReputationRow", "Reputation", "+4", 5);
        data.FindProperty("coinsText").objectReferenceValue = coins;
        data.FindProperty("reputationText").objectReferenceValue = reputation;
        data.ApplyModifiedProperties();

        Undo.RecordObject(card, "Fit result rewards");
        card.sizeDelta = new Vector2(card.sizeDelta.x, 650);
        var layout = card.GetComponent<VerticalLayoutGroup>();
        Undo.RecordObject(layout, "Space result rewards"); layout.spacing = 12;
        var button = (Button)data.FindProperty("continueButton").objectReferenceValue;
        Undo.RecordObject(button.gameObject, "Name reward claim button"); button.gameObject.name = "ClaimRewardsButton";
        var buttonLayout = button.GetComponent<HorizontalLayoutGroup>();
        if (buttonLayout != null) { Undo.RecordObject(buttonLayout, "Lay out claim label"); buttonLayout.enabled = false; }
        var element = button.GetComponent<LayoutElement>();
        Undo.RecordObject(element, "Size claim button"); element.minHeight = element.preferredHeight = 110;
        var art = button.transform.Find("Image").GetComponent<Image>();
        Undo.RecordObject(art, "Use tropical claim frame");
        art.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/UI/Frames/valueablesFrame.png").OfType<Sprite>().First();
        art.type = Image.Type.Simple; art.raycastTarget = false;
        var artRect = art.rectTransform;
        Undo.RecordObject(artRect, "Place claim frame");
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f); artRect.anchoredPosition = Vector2.zero;
        artRect.sizeDelta = new Vector2(440, 98);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        Undo.RecordObject(label, "Label claim rewards");
        label.text = "CLAIM REWARDS"; label.font = energy.font;
        label.fontSize = 32; label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = 32;
        label.color = new Color(1f, 0.85f, 0.43f); label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        var labelRect = label.rectTransform;
        Undo.RecordObject(labelRect, "Place claim text");
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(65, 16); labelRect.offsetMax = new Vector2(-65, -16);
        label.transform.SetAsLastSibling();
        var hit = button.GetComponent<Image>();
        Undo.RecordObject(hit, "Enable claim hit area"); hit.enabled = true; hit.color = Color.clear;
        Undo.RecordObject(button, "Use claim button art"); button.targetGraphic = art;
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Selection.activeGameObject = view.gameObject;
        Debug.Log("Coins and Reputation rows and Claim Rewards configured in the existing S4 window.", view);
    }

    private static TMP_Text Row(RectTransform card, GameObject template, string name, string title, string preview, int index)
    {
        var existing = card.Find(name);
        var row = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(template, card, false);
        if (existing == null) Undo.RegisterCreatedObjectUndo(row, "Add result reward row");
        row.name = name; row.SetActive(true); row.transform.SetSiblingIndex(index);
        var label = row.transform.Find("Label").GetComponent<TMP_Text>();
        var value = row.transform.Find("Value").GetComponent<TMP_Text>();
        Undo.RecordObjects(new UnityEngine.Object[] { label, value }, "Set result currency");
        label.text = title; value.text = preview;
        label.raycastTarget = value.raycastTarget = false;
        return value;
    }
}

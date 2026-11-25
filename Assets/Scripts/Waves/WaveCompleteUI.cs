using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// S4 - Wave Complete. Scene-authored presentation: this script spawns nothing, it only fills
// the references you assign in the Inspector. Keep this component on an object that stays
// ACTIVE (not on the panel itself); the panel to show/hide is the "Root" reference.
[AddComponentMenu("Waves/Wave Complete UI (S4)")]
public sealed class WaveCompleteUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The S4 panel that is shown / hidden. Must be a different object than the one holding this script, or at least a child that can be disabled.")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;          // optional: "WAVE 3 COMPLETE"

    [Header("Rating (stars)")]
    [Tooltip("Exactly 5 star Images, left to right.")]
    [SerializeField] private Image[] starImages;
    [Tooltip("Optional. If both sprites are set, stars swap sprites; otherwise the colors below are used.")]
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private TMP_Text ratingText;         // optional: "3 / 5"

    [Header("Results (value only, put the labels as static texts in the scene)")]
    [SerializeField] private TMP_Text guestsServedText;   // "12"
    [SerializeField] private TMP_Text satisfactionText;   // "72%"
    [Tooltip("Optional bar. Set the Image Type to Filled (Horizontal).")]
    [SerializeField] private Image satisfactionFill;

    [Header("Rewards")]
    [SerializeField] private TMP_Text energyText;         // "+1"
    [SerializeField] private TMP_Text materialsText;      // optional, "+2"
    [Tooltip("Optional. Hidden when the wave gave 0 Materials.")]
    [SerializeField] private GameObject materialsRow;

    [Header("Continue")]
    [SerializeField] private Button continueButton;

    public event Action ContinueRequested;

    private bool bound;
    private UnityAction continueAction;

    public bool IsConfigured
    {
        get
        {
            Bind();
            return root != null && continueButton != null && starImages != null && starImages.Length == 5 &&
                   Array.TrueForAll(starImages, s => s != null) &&
                   guestsServedText != null && satisfactionText != null && energyText != null;
        }
    }

    private void Awake() { Bind(); }

    private void OnDestroy()
    {
        if (bound && continueButton != null) continueButton.onClick.RemoveListener(continueAction);
    }

    // Safe to call from anywhere, even when this object was inactive during Awake.
    private void Bind()
    {
        if (bound || continueButton == null) return;
        bound = true;
        continueAction = () => ContinueRequested?.Invoke();
        continueButton.onClick.AddListener(continueAction);
    }

    public void Show(WaveResult result)
    {
        Bind();
        if (result == null || root == null) return;

        if (titleText != null) titleText.text = $"WAVE {result.WaveNumber} COMPLETE";
        ShowStars(result.Stars);
        if (ratingText != null) ratingText.text = $"{result.Stars} / {starImages.Length}";

        guestsServedText.text = result.Customers.ToString();
        satisfactionText.text = $"{result.Satisfaction:F0}%";
        if (satisfactionFill != null) satisfactionFill.fillAmount = Mathf.Clamp01((float)(result.Satisfaction / 100.0));

        energyText.text = $"+{result.Energy}";
        if (materialsText != null) materialsText.text = $"+{result.Materials}";
        if (materialsRow != null) materialsRow.SetActive(result.Materials > 0);

        root.SetActive(true);
    }

    public void Hide()
    {
        Bind();
        if (root != null) root.SetActive(false);
    }

    private void ShowStars(int stars)
    {
        bool useSprites = starFilled != null && starEmpty != null;
        for (int i = 0; i < starImages.Length; i++)
        {
            bool filled = i < stars;
            if (useSprites) starImages[i].sprite = filled ? starFilled : starEmpty;
            else starImages[i].color = filled ? filledColor : emptyColor;
        }
    }
}

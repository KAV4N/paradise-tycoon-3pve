using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Energy boost button inside the building panel ("MVP Building Detail and S9").
// Presentation only: GameStateManager decides if a boost is allowed, WaveServicesComponent applies it.
public partial class BuildingSystem
{
    [Header("Energy Boost - drag objects from the Building panel")]
    [SerializeField] private Button boostButton;
    [SerializeField] private TMP_Text boostLabel;     // optional text on the button
    [SerializeField] private Image boostTimerFill;    // optional, Image Type = Filled

    // Raised when the player presses Boost. GameStateManager listens.
    public event Action<Building> BoostRequested;

    private WaveServicesComponent boostSource;
    private bool boostAllowed;
    private bool hasBackdropClose;

    public void BindBoostSource(WaveServicesComponent source) { boostSource = source; }

    public void SetBoostAllowed(bool allowed)
    {
        boostAllowed = allowed;
        Refresh();
    }

    private void InitBoostUI()
    {
        if (boostButton != null)
            boostButton.onClick.AddListener(OnBoostClicked);

        // During a wave the Close button is hidden, so tapping the dark area around the panel closes it.
        if (overlay == null)
            return;

        var backdrop = overlay.transform.Find("Backdrop");
        var graphic = backdrop != null ? backdrop.GetComponent<Graphic>() : null;

        if (graphic == null)
            return;

        var button = backdrop.GetComponent<Button>();

        if (button == null)
            button = backdrop.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = graphic;
        button.onClick.AddListener(() => { if (waveActive) Close(); });
        hasBackdropClose = true;
    }

    private void OnBoostClicked()
    {
        if (waveActive && selected != null && selected.unlocked && !selected.IsSpa)
            BoostRequested?.Invoke(selected);
    }

    private void RefreshBoostUI(Building b)
    {
        if (boostButton == null)
            return;

        bool show = waveActive && !areaView && b != null && b.unlocked && !b.IsSpa && boostSource != null;

        boostButton.gameObject.SetActive(show);

        if (!show)
            return;

        NeedType need = b.needType;
        bool active = boostSource.IsBoosted(need);
        int cost = boostSource.BoostEnergyCost;

        boostButton.interactable = boostAllowed && !active && boostSource.HasService(need) && energy >= cost;

        if (boostLabel != null)
            boostLabel.text = active
                ? $"BOOST {Mathf.CeilToInt(boostSource.BoostTimeLeft(need))}s"
                : $"BOOST  -{cost} Energy";

        if (boostTimerFill != null)
            boostTimerFill.fillAmount = active && boostSource.BoostDuration > 0f
                ? Mathf.Clamp01(boostSource.BoostTimeLeft(need) / boostSource.BoostDuration)
                : 0f;
    }

    private string BoostStatusText(Building b)
    {
        if (boostSource == null || b.IsSpa)
            return "";

        NeedType need = b.needType;

        if (!boostSource.HasService(need))
            return "No guests need this service in this wave";

        if (boostSource.IsBoosted(need))
            return $"Boost active: {Mathf.CeilToInt(boostSource.BoostTimeLeft(need))}s left";

        return $"Energy boost: x{boostSource.BoostMultiplier:0.##} service speed for {boostSource.BoostDuration:0}s";
    }
}

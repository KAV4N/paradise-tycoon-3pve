using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// S5 - Recommended investment. Scene-authored presentation: spawns nothing, only fills the
// references you assign. Every text is optional except the building name and the two buttons.
// Keep this component on an object that stays ACTIVE; the panel to show/hide is "Root".
[AddComponentMenu("Waves/Upgrade Recommendation UI (S5)")]
public sealed class UpgradeRecommendationUI : MonoBehaviour
{
    [Serializable]
    public struct BuildingIcon
    {
        public string buildingId;   // Reception, Restaurant, Bar, Pool, Bungalow
        public Sprite sprite;
    }

    [Header("Panel")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text buildingNameText;     // "Beach Bar"
    [SerializeField] private TMP_Text reasonText;           // "Drinks guests were the least satisfied..."
    [SerializeField] private Image buildingIcon;            // optional
    [SerializeField] private BuildingIcon[] icons;          // optional

    [Header("Before / After (value only)")]
    [SerializeField] private TMP_Text levelBeforeText;      // "Lv 2"
    [SerializeField] private TMP_Text levelAfterText;       // "Lv 3"
    [SerializeField] private TMP_Text seatsBeforeText;      // "5"
    [SerializeField] private TMP_Text seatsAfterText;       // "7"
    [SerializeField] private TMP_Text workerSlotsBeforeText;
    [SerializeField] private TMP_Text workerSlotsAfterText;

    [Header("Price and gain (value only)")]
    [SerializeField] private TMP_Text priceText;            // "500 Coins" / "2500 Coins + 5 Materials"
    [SerializeField] private TMP_Text resortValueGainText;  // "+75"
    [SerializeField] private TMP_Text statusText;           // "Missing: 120 Coins" (empty when affordable)

    [Header("Buttons")]
    [SerializeField] private Button upgradeNowButton;
    [SerializeField] private Button laterButton;

    public event Action UpgradeNowRequested;
    public event Action LaterRequested;

    private bool bound;
    private UnityAction upgradeAction, laterAction;

    public bool IsConfigured
    {
        get
        {
            Bind();
            return root != null && buildingNameText != null && upgradeNowButton != null && laterButton != null;
        }
    }

    private void Awake() { Bind(); }

    private void OnDestroy()
    {
        if (!bound) return;
        if (upgradeNowButton != null) upgradeNowButton.onClick.RemoveListener(upgradeAction);
        if (laterButton != null) laterButton.onClick.RemoveListener(laterAction);
    }

    private void Bind()
    {
        if (bound || upgradeNowButton == null || laterButton == null) return;
        bound = true;
        upgradeAction = () => UpgradeNowRequested?.Invoke();
        laterAction = () => LaterRequested?.Invoke();
        upgradeNowButton.onClick.AddListener(upgradeAction);
        laterButton.onClick.AddListener(laterAction);
    }

    // Also used to refresh the panel while it is open (e.g. Coins changed).
    public void Show(UpgradeOffer offer, string reason)
    {
        Bind();
        if (offer == null || offer.Building == null || root == null) return;

        buildingNameText.text = offer.Building.displayName;
        Set(reasonText, reason);
        ShowIcon(offer.Building.id);

        Set(levelBeforeText, $"Lv {offer.LevelBefore}");
        Set(levelAfterText, $"Lv {offer.LevelAfter}");
        Set(seatsBeforeText, offer.SeatsBefore.ToString());
        Set(seatsAfterText, offer.SeatsAfter.ToString());
        Set(workerSlotsBeforeText, offer.WorkerSlotsBefore.ToString());
        Set(workerSlotsAfterText, offer.WorkerSlotsAfter.ToString());

        Set(priceText, !offer.IsConfigured ? "Price not configured" :
            offer.MaterialCost > 0 ? $"{offer.CoinCost} Coins + {offer.MaterialCost} Materials" : $"{offer.CoinCost} Coins");
        Set(resortValueGainText, $"+{offer.ResortValueGain}");

        string status = "";
        if (!offer.IsConfigured) status = "Materials cost must be configured before purchase";
        else if (!offer.CanAfford)
        {
            status = "Missing: ";
            if (offer.MissingCoins > 0) status += $"{offer.MissingCoins} Coins";
            if (offer.MissingCoins > 0 && offer.MissingMaterials > 0) status += " / ";
            if (offer.MissingMaterials > 0) status += $"{offer.MissingMaterials} Materials";
        }
        Set(statusText, status);

        upgradeNowButton.interactable = offer.IsAvailable;
        laterButton.interactable = true;
        root.SetActive(true);
    }

    public void Hide()
    {
        Bind();
        if (root != null) root.SetActive(false);
    }

    private void ShowIcon(string buildingId)
    {
        if (buildingIcon == null) return;
        Sprite found = null;
        if (icons != null)
            foreach (var entry in icons)
                if (entry.buildingId == buildingId) { found = entry.sprite; break; }
        buildingIcon.sprite = found;
        buildingIcon.gameObject.SetActive(found != null);
    }

    private static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}

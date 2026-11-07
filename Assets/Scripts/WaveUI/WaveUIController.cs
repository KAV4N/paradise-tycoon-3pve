using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WaveUIController : MonoBehaviour
{
    [System.Serializable]
    public struct NeedDisplayData
    {
        public NeedType type;
        public string displayName;
        public Sprite icon;
        public bool isUnlockedByDefault;
    }

    [Header("Nastavenia Potrieb (Z NeedType.cs)")]
    [SerializeField] private List<NeedDisplayData> needDisplayConfigs = new List<NeedDisplayData>();

    [Header("Hlavná Obrazovka (HUD)")]
    [SerializeField] private Button startWaveButton;
    [SerializeField] private RawImage startWaveButtonTexture;

    [Header("Stavový Indikátor & Bočné Tlačidlo")]
    [SerializeField] private TextMeshProUGUI waveStatusIndicatorText; // Text pre "READY FOR GUESTS" / "ACTIVE WAVE"
    [SerializeField] private GameObject waveStatusIndicatorContainer; // Samotný panel indikátora (bez Button)
    [SerializeField] private Button openSidePanelArrowButton; // Malé tlačidlo so šípkou na otvorenie panelu

    [Header("Postranný Panel Priebehu Vlny")]
    [SerializeField] private GameObject sidePanel;
    [SerializeField] private Button closeSidePanelButton;
    [SerializeField] private TextMeshProUGUI waveStatusTitleText;
    [SerializeField] private TextMeshProUGUI guestCountText;

    [Header("Spokojnosť (Satisfaction)")]
    [SerializeField] private Image satisfactionFill;
    [SerializeField] private TextMeshProUGUI satisfactionScoreText;

    [Header("Dynamický Zoznam Potrieb")]
    [SerializeField] private Transform needItemsContainer;
    [SerializeField] private GameObject needItemPrefab;

    [Header("Spätná Väzba Hostí (Feedback Box)")]
    [SerializeField] private GameObject feedbackContainer;
    [SerializeField] private Image customerPortrait;
    [SerializeField] private TextMeshProUGUI feedbackQuoteText;

    private Dictionary<NeedType, NeedItemUI> activeNeedItems = new Dictionary<NeedType, NeedItemUI>();
    private HashSet<NeedType> unlockedNeeds = new HashSet<NeedType>();
    private bool isWaveInProgress = false;
    public bool IsWaveInProgress => isWaveInProgress;
    public event Action<bool> WaveStateChanged;

    private void Awake()
    {
        if (startWaveButton != null)
        {
            startWaveButton.onClick.AddListener(OnStartWaveClicked);
            if (startWaveButtonTexture == null)
            {
                startWaveButtonTexture = startWaveButton.GetComponent<RawImage>();
            }
        }

        // Bočné tlačidlo so šípkou otvára panel
        if (openSidePanelArrowButton != null)
            openSidePanelArrowButton.onClick.AddListener(ToggleSidePanel);

        if (closeSidePanelButton != null)
            closeSidePanelButton.onClick.AddListener(CloseSidePanel);

        InitializeUnlockedNeeds();
        SetWaveState(false);
        CloseSidePanel();
    }

    private void InitializeUnlockedNeeds()
    {
        unlockedNeeds.Clear();
        foreach (var config in needDisplayConfigs)
        {
            if (config.isUnlockedByDefault)
            {
                unlockedNeeds.Add(config.type);
            }
        }
        RebuildNeedsUI();
    }

    // --- REŽIM VLNY A SIVÉ TLAČIDLO ---

    public void SetWaveState(bool inProgress)
    {
        bool changed = isWaveInProgress != inProgress;
        isWaveInProgress = inProgress;

        // 1. Správanie tlačidla Start Wave
        if (startWaveButton != null)
        {
            startWaveButton.interactable = !inProgress;
        }

        // Zmena farby na čisto sivú (bez zmeny priehľadnosti/alpha)
        if (startWaveButtonTexture != null)
        {
            startWaveButtonTexture.color = inProgress ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
        }

        // 2. Stavový indikátor (nie tlačidlo)
        if (waveStatusIndicatorText != null)
        {
            waveStatusIndicatorText.text = inProgress ? "ACTIVE WAVE" : "READY FOR GUESTS";
        }

        // 3. Bočné tlačidlo so šípkou sa dá použiť IBA vtedy, keď vlna prebieha
        if (openSidePanelArrowButton != null)
        {
            openSidePanelArrowButton.interactable = inProgress;
            // Ak ho chceš radšej kompletne schovať keď vlna nebeží, odkomentuj riadok nižšie:
            // openSidePanelArrowButton.gameObject.SetActive(inProgress);
        }

        if (waveStatusTitleText != null)
        {
            waveStatusTitleText.text = inProgress ? "WAVE IN PROGRESS" : "WAVE READY";
        }

        // Ak vlna skončila, zavrieme panel
        if (!inProgress)
        {
            CloseSidePanel();
        }
        if (changed) WaveStateChanged?.Invoke(inProgress);
    }

    // --- ODOMYKANIE / ZAMYKANIE POTRIEB ---

    public void UnlockNeed(NeedType need)
    {
        if (unlockedNeeds.Add(need))
        {
            RebuildNeedsUI();
        }
    }

    public void LockNeed(NeedType need)
    {
        if (unlockedNeeds.Remove(need))
        {
            RebuildNeedsUI();
        }
    }

    private void RebuildNeedsUI()
    {
        if (needItemsContainer == null || needItemPrefab == null) return;

        foreach (Transform child in needItemsContainer)
        {
            Destroy(child.gameObject);
        }
        activeNeedItems.Clear();

        foreach (NeedType needType in Enum.GetValues(typeof(NeedType)))
        {
            if (unlockedNeeds.Contains(needType))
            {
                NeedDisplayData config = GetConfigForNeed(needType);

                GameObject newObj = Instantiate(needItemPrefab, needItemsContainer);
                NeedItemUI itemUI = newObj.GetComponent<NeedItemUI>();
                if (itemUI != null)
                {
                    string displayName = string.IsNullOrEmpty(config.displayName) ? needType.ToString() : config.displayName;
                    itemUI.Setup(needType, config.icon, displayName);
                    activeNeedItems[needType] = itemUI;
                }
            }
        }
    }

    private NeedDisplayData GetConfigForNeed(NeedType needType)
    {
        foreach (var config in needDisplayConfigs)
        {
            if (config.type == needType)
                return config;
        }

        return new NeedDisplayData
        {
            type = needType,
            displayName = needType.ToString(),
            icon = null,
            isUnlockedByDefault = false
        };
    }

    public void UpdateNeedProgress(NeedType need, int current, int total, bool isBottleneck = false, string warningMsg = "")
    {
        if (activeNeedItems.TryGetValue(need, out NeedItemUI item))
        {
            item.UpdateProgress(current, total, isBottleneck, warningMsg);
        }
    }

    public void UpdateWaveOverview(int activeGuests, int maxGuests, float satisfactionScore, float fillRatio)
    {
        if (guestCountText != null)
            guestCountText.text = $"GUESTS: {activeGuests}/{maxGuests}";

        if (satisfactionScoreText != null)
            satisfactionScoreText.text = $"★ ({satisfactionScore:F1})";

        if (satisfactionFill != null)
            satisfactionFill.fillAmount = Mathf.Clamp01(fillRatio);
    }

    public void SetCustomerFeedback(Sprite portrait, string quote)
    {
        if (feedbackContainer == null) return;

        bool hasQuote = !string.IsNullOrEmpty(quote);
        feedbackContainer.SetActive(hasQuote);

        if (hasQuote)
        {
            if (customerPortrait != null) customerPortrait.sprite = portrait;
            if (feedbackQuoteText != null) feedbackQuoteText.text = $"\"{quote}\"";
        }
    }

    // --- PANOVANIE SIDE PANELU ---

    public void ToggleSidePanel()
    {
        // Otvoriť panel môžeme iba vtedy, ak vlna prebieha
        if (!isWaveInProgress) return;

        if (sidePanel != null)
            sidePanel.SetActive(!sidePanel.activeSelf);
    }

    public void OpenSidePanel()
    {
        if (!isWaveInProgress) return;

        if (sidePanel != null) sidePanel.SetActive(true);
    }

    public void CloseSidePanel()
    {
        if (sidePanel != null) sidePanel.SetActive(false);
    }

    private void OnStartWaveClicked()
    {
        SetWaveState(true);
        OpenSidePanel();
    }
}

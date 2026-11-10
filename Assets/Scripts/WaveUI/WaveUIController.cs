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
    public event Action StartWaveRequested;
    public event Action FlowActionRequested;
    private GameState presentationState = GameState.AWAITING_WAVE;
    public bool IsWavePaused => presentationState == GameState.WAVE_PAUSED;
    private Button flowButton;
    private TMP_Text flowLabel;
    private GameObject flowCanvas;
    [SerializeField] private TMP_Text progressLabel;

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

    private void SetWaveState(bool inProgress)
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

    // presentation adapter: state decisions belong to GameStateManager.
    public void ApplyGameState(GameState state, int wave, bool allComplete)
    {
        presentationState = state;
        SetWaveState(state == GameState.WAVE_ACTIVE || state == GameState.WAVE_PAUSED);
        if (startWaveButton != null) startWaveButton.interactable = state == GameState.AWAITING_WAVE && !allComplete;
        string status;
        switch (state)
        {
            case GameState.AWAITING_WAVE: status = allComplete ? "ALL WAVES COMPLETE" : $"WAVE {wave} READY"; break;
            case GameState.WAVE_INCOMING: status = "GUESTS INCOMING"; break;
            case GameState.WAVE_ACTIVE: status = "GUESTS ARRIVING"; break;
            case GameState.WAVE_PAUSED: status = "WAVE PAUSED"; break;
            case GameState.WAVE_RESOLVING: status = "ALL GUESTS SERVED"; break;
            case GameState.WAVE_COMPLETE: status = $"WAVE {wave} COMPLETE"; break;
            default: status = "READY FOR GUESTS"; break;
        }
        if (waveStatusIndicatorText != null) waveStatusIndicatorText.text = status;
        if (waveStatusTitleText != null) waveStatusTitleText.text = status;
        bool panelAvailable = state != GameState.AWAITING_WAVE && state != GameState.UPGRADE;
        if (openSidePanelArrowButton != null) openSidePanelArrowButton.interactable = panelAvailable;
        if (sidePanel != null) sidePanel.SetActive(panelAvailable);
        EnsureFlowButton();
        flowCanvas.SetActive(state == GameState.WAVE_INCOMING || state == GameState.WAVE_ACTIVE || state == GameState.WAVE_PAUSED);
        flowLabel.text = state == GameState.WAVE_PAUSED ? "Resume" : "Pause";
    }

    public void RenderWave(WaveSnapshot snapshot, bool rebuild)
    {
        if (rebuild)
        {
            unlockedNeeds.Clear();
            foreach (var need in snapshot.Needs) unlockedNeeds.Add(need.Type);
            RebuildNeedsUI();
        }
        foreach (var need in snapshot.Needs)
            if (activeNeedItems.TryGetValue(need.Type, out var item)) item.RenderService(need);
        if (guestCountText != null) guestCountText.text = $"WAVE {snapshot.Number} - GUESTS: {snapshot.Customers}";
        if (progressLabel != null) progressLabel.text = "NEEDS FULFILLED";
        if (satisfactionScoreText != null) satisfactionScoreText.text = $"{snapshot.Completed}/{snapshot.Total}";
        if (satisfactionFill != null)
        {
            satisfactionFill.type = Image.Type.Simple;
            var rect = satisfactionFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(snapshot.Total > 0 ? (float)snapshot.Completed / snapshot.Total : 0, 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (presentationState == GameState.WAVE_ACTIVE)
        {
            string status = snapshot.ArrivalsClosed ? "ALL GUESTS ARRIVED - FINISHING SERVICE" : "GUESTS ARRIVING";
            if (waveStatusTitleText != null) waveStatusTitleText.text = status;
        }
    }

    private void EnsureFlowButton()
    {
        if (flowCanvas != null) return;
        flowCanvas = new GameObject("Wave controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = flowCanvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 101;
        var scaler = flowCanvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        var go = new GameObject("Wave action", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(flowCanvas.transform, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0); rect.anchoredPosition = new Vector2(0, 24); rect.sizeDelta = new Vector2(280, 48);
        go.GetComponent<Image>().color = new Color(0.1f, 0.25f, 0.3f);
        flowButton = go.GetComponent<Button>(); flowButton.onClick.AddListener(() => FlowActionRequested?.Invoke());
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform, false);
        var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        flowLabel = label.GetComponent<TextMeshProUGUI>(); flowLabel.fontSize = 22; flowLabel.alignment = TextAlignmentOptions.Center; flowLabel.raycastTarget = false;
    }

    private void OnDestroy() { if (flowCanvas != null) Destroy(flowCanvas); }

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
        if (presentationState == GameState.AWAITING_WAVE || presentationState == GameState.UPGRADE) return;

        if (sidePanel != null)
            sidePanel.SetActive(!sidePanel.activeSelf);
    }

    public void OpenSidePanel()
    {
        if (presentationState == GameState.AWAITING_WAVE || presentationState == GameState.UPGRADE) return;

        if (sidePanel != null) sidePanel.SetActive(true);
    }

    public void CloseSidePanel()
    {
        if (sidePanel != null) sidePanel.SetActive(false);
    }

    private void OnStartWaveClicked()
    {
        StartWaveRequested?.Invoke();
    }
}

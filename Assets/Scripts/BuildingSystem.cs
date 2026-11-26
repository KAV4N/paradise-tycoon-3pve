using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// FR-003, S9 and level-derived capacity.
public partial class BuildingSystem : MonoBehaviour
{
    [Serializable]
    public class Building
    {
        public string id, displayName;
        public NeedType needType;

        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("need")]
        private string legacyNeed;

        public bool unlocked;

        [Range(1, 5)]
        public int level = 1;

        [Tooltip("Reputation gate. -1 means not specified by the design documents.")]
        public int reputationRequired = -1;

        public int unlockCoins = -1;

        [Min(0)]
        public int unlockResortValueGain;

        [Tooltip("Optional visual progression, indexed by level.")]
        public Sprite[] levelSprites;

        [HideInInspector]
        public int workers;

        // True once the Manager of this building has been hired. Only wave buildings have a Manager.
        [HideInInspector]
        public bool hasManager;

        [NonSerialized]
        public BuildingSlot slot;

        public Building(string key, string label, NeedType service, bool built, int price, int reputationGate = -1)
        {
            id = key;
            displayName = label;
            needType = service;
            unlocked = built;
            unlockCoins = price;
            reputationRequired = reputationGate;
        }

        public bool IsSpa => id == "Spa";

        public void MigrateLegacyNeed()
        {
            if (NeedTypes.TryFromLegacyName(legacyNeed, out var migrated))
            {
                needType = migrated;
                legacyNeed = null;
            }

            if (IsSpa)
                needType = NeedType.None;
        }
    }

    [Header("Score HUD text references")]
    public TMP_Text reputationHud;
    public TMP_Text resortValueHud;

    [SerializeField, Min(0), UnityEngine.Serialization.FormerlySerializedAs("reputation")]
    private int reputationScore;

    public int reputation => reputationScore;
    [Min(0)] public int resortValue;
    public ResortValueSystem resortValueSystem = new ResortValueSystem();

    [Header("Phase - wave controller calls SetWaveActive")]
    [Tooltip("True in WAVE_INCOMING, WAVE_ACTIVE, WAVE_PAUSED and WAVE_RESOLVING. " +
             "False in AWAITING_WAVE, WAVE_COMPLETE and UPGRADE.")]
    public bool waveActive;

    [Header("Wave building levels - capacity and tuning")]
    public int[] capacityByLevel = { 3, 5, 7, 7, 10 };
    public int[] workerSlotsByLevel = { 1, 1, 1, 2, 3 };
    public int[] upgradeCoinsByLevel = { 0, 500, 1200, 2500, 5000 };

    [Tooltip("Indexed by the level being reached. Lv4/5 require Materials, but the design documents " +
             "do not specify the amounts. -1 = not specified, purchase stays blocked until configured.")]
    public int[] upgradeMaterialsByLevel = { 0, 0, 0, -1, -1 };

    [Tooltip("Proposed Resort Value gains, tune with economy data.")]
    public int[] resortValueByLevel = { 0, 75, 150, 250, 500 };

    [Header("Workers - hiring (TUNING)")]
    [Min(0)] public int baseWorkerHireCost = 400;
    [Min(0)] public int workerHireCostStep = 200;
    [Range(0f, 0.95f)] public float workerCapRatio = 0.8f;

    [Header("Workers - utilization (TUNING)")]
    [Tooltip("Utilization when the building has no Worker. Must stay above 0 (WR2). Document range 0.1-0.6.")]
    [Range(0.1f, 0.6f)] public float utilizationEmpty = 0.33f;
    [Tooltip("Additional utilization granted by the second Worker (diminishing, less than the first).")]
    [Range(0f, 0.99f)] public float utilizationSecond = 0.2f;
    [Tooltip("Additional utilization granted by the third Worker (diminishing, less than the second).")]
    [Range(0f, 0.99f)] public float utilizationThird = 0.1f;

    // utilization_staffed is fixed at 1.0 in the design documents.
    private const float UtilizationStaffed = 1f;

    [Header("Managers - hiring and multiplier (TUNING)")]
    [Tooltip("One-time Coins price of a Manager (example value from the design documents).")]
    [Min(0)] public int managerHireCost = 800;
    [Tooltip("manager_multiplier: shortens service on every seat of the building, with or without a Worker. " +
             "Example x1.2. Keep the spread narrow (1.0-1.4).")]
    [Range(1f, 1.4f)] public float managerMultiplier = 1.2f;

    [Header("Buildings - proposed Reputation tuning: 100 / 300 / 600")]
    public Building[] buildings =
    {
        new Building("Reception", "Reception", NeedType.Rooms, true, 0),
        new Building("Restaurant", "Restaurant", NeedType.Food, true, 0),
        new Building("Bar", "Beach Bar", NeedType.Drinks, true, 0),
        new Building("Pool", "Pool", NeedType.Relax, false, 1200, 100),
        new Building("Bungalow", "Bungalows", NeedType.OceanView, false, 2500, 300),
        new Building("Spa", "Spa", NeedType.None, false, 2500, 600)
    };

    // Number of Workers bought for Coins. Workers that came with a building unlock or the starting
    // roster are free and are not counted here.
    [SerializeField, HideInInspector] private int paidHires;

    // Every Worker always stands on a building, so the total is derived from the buildings.
    public int TotalWorkers
    {
        get
        {
            int sum = 0;

            foreach (var b in buildings)
                if (b != null && !b.IsSpa)
                    sum += Mathf.Max(0, b.workers);

            return sum;
        }
    }

    public int HiredWorkers => TotalWorkers;
    public int PaidHires => paidHires;

    public event Action OnChanged;
    public event Action<Building> OnBuildingUnlocked;

    // Raised when a built building is tapped during a wave. The tap leads to Worker Allocation (S3),
    // never to an upgrade. The S3 UI subscribes to this event.
    public event Action<Building> OnWorkerAllocationRequested;

    private readonly List<KeyValuePair<BuildingSlot, UnityAction>> listeners =
        new List<KeyValuePair<BuildingSlot, UnityAction>>();

    private Building selected;
    private bool areaView;

    [SerializeField, HideInInspector] private GameObject overlay, card;
    [SerializeField, HideInInspector] private TMP_Text heading, summary, price, status, balances, actionLabel;
    [SerializeField, HideInInspector] private Button action, closeButton;

    private void Awake()
    {
        reputationScore = ResortValueSystem.RestoreReputation(reputationScore);

        foreach (var b in buildings)
            b.MigrateLegacyNeed();

        foreach (var b in buildings)
        {
            if (b.IsSpa)
            {
                b.level = 1;
                b.workers = 0;
                b.needType = NeedType.None;
            }

            // A Manager only exists on a built wave building.
            if (b.IsSpa || !b.unlocked)
                b.hasManager = false;
        }

        foreach (var slot in GetComponentsInChildren<BuildingSlot>(true))
        {
            var b = Find(slot.buildingId);
            if (b == null)
                continue;

            b.slot = slot;
            slot.startsBuilt = b.unlocked;

            UnityAction open = () => Open(b);

            slot.onBuiltClicked.AddListener(open);
            slot.onSiteClicked.AddListener(open);

            listeners.Add(new KeyValuePair<BuildingSlot, UnityAction>(slot, open));
        }
    }

    private void Start()
    {
        foreach (var b in buildings)
            SyncArt(b);

        BuildUI();

        action.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Close);

        // Only a fresh game gets the starting roster. A loaded game already has its Workers.
        if (TotalWorkers == 0)
            SeedStartingWorkers();

        RefreshHud();
    }

    // Starting roster: always one Worker fewer than buildings (3 buildings -> 2 Workers).
    private void SeedStartingWorkers()
    {
        int cap = WorkerCap();

        foreach (var b in buildings)
        {
            if (b == null || !b.unlocked || b.IsSpa)
                continue;

            if (TotalWorkers >= cap)
                break;

            if (b.workers < WorkerSlots(b))
                b.workers++;
        }
    }

    public Building Find(string id)
    {
        foreach (var b in buildings)
        {
            if (b.id == id)
                return b;
        }

        return null;
    }

    public int Capacity(Building b) =>
        b.IsSpa ? 0 : Value(capacityByLevel, b.level - 1);

    public int MaxLevel(Building b) =>
        b.IsSpa ? 1 : 5;

    public int WorkerSlots(Building b) =>
        b.IsSpa ? 0 : Value(workerSlotsByLevel, b.level - 1);

    private int Value(int[] data, int index) =>
        data != null && index >= 0 && index < data.Length ? data[index] : -1;

    public bool HasRequiredReputation(int required) =>
        ResortValueSystem.HasRequiredReputation(reputation, required);

    private bool GateMet(Building b) =>
        HasRequiredReputation(b.reputationRequired);

    public string UnlockState(Building b) =>
        b.unlocked ? "Unlocked" :
        !GateMet(b) ? "Locked" :
        b.unlockCoins >= 0 && coins < b.unlockCoins ? "Not affordable" :
        "Available";

    // Worker management

    public int GetWorkerCount(Building b)
    {
        if (b == null || b.IsSpa)
            return 0;

        return Mathf.Max(0, b.workers);
    }

    public bool HasFreeWorkerSlot(Building b)
    {
        return CanAddWorker(b);
    }

    public bool CanAddWorker(Building b)
    {
        if (b == null || !b.unlocked || b.IsSpa)
            return false;

        int slots = WorkerSlots(b);

        return slots > 0 && b.workers < slots;
    }

    // Moving a Worker is free and instant, in AWAITING_WAVE and during a wave.
    // The Worker always ends up on a building with a free slot.
    public bool TryMoveWorker(Building from, Building to)
    {
        if (from == null || to == null || from == to || from.IsSpa || to.IsSpa)
            return false;

        if (from.workers <= 0 || !CanAddWorker(to))
            return false;

        from.workers--;
        to.workers++;

        NotifyWorkerChange();
        return true;
    }

    public int TotalWorkerSlots()
    {
        int sum = 0;

        foreach (var b in buildings)
            if (b != null && b.unlocked && !b.IsSpa)
                sum += WorkerSlots(b);

        return sum;
    }

    // Hard rule: there are always fewer Workers than slots, whatever the tuned ratio is.
    public int WorkerCap()
    {
        int slots = TotalWorkerSlots();

        if (slots <= 0)
            return 0;

        int byRatio = Mathf.FloorToInt(workerCapRatio * slots + 0.0001f);

        return Mathf.Clamp(byRatio, 0, slots - 1);
    }

    public int NextWorkerHireCost() =>
        baseWorkerHireCost + workerHireCostStep * Mathf.Max(0, paidHires);

    public bool CanHireWorker(Building target, out string reason)
    {
        reason = null;

        if (waveActive)
        {
            reason = "Hire available between waves";
            return false;
        }

        if (target == null || !target.unlocked || target.IsSpa)
        {
            reason = "Invalid building";
            return false;
        }

        if (TotalWorkers >= WorkerCap())
        {
            reason = "Upgrade buildings to open more shifts";
            return false;
        }

        if (!HasFreeWorkerSlot(target))
        {
            reason = "No free worker slot on this building";
            return false;
        }

        int cost = NextWorkerHireCost();

        if (coins < cost)
        {
            reason = $"Missing: {cost - coins} Coins";
            return false;
        }

        return true;
    }

    public bool TryHireWorker(Building target)
    {
        if (!CanHireWorker(target, out _))
            return false;

        int cost = NextWorkerHireCost();

        if (!SpendCurrencies(cost, 0, 0, 0))
            return false;

        target.workers++;
        paidHires++;
        resortValueSystem.Add(ref resortValue, resortValueSystem.workerHireGain);

        NotifyWorkerChange();
        return true;
    }

    private void NotifyWorkerChange()
    {
        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
    }

    // Utilization for a single building, derived from the number of assigned Workers.
    // Never returns 0 for a built wave building, so an empty building is slow, not broken (WR2).
    public float Utilization(Building b)
    {
        if (b == null || b.IsSpa || !b.unlocked)
            return 0f;

        int w = Mathf.Max(0, b.workers);

        if (w <= 0)
            return utilizationEmpty;

        float u = UtilizationStaffed;

        if (w >= 2)
            u += utilizationSecond;

        if (w >= 3)
            u += utilizationThird;

        return u;
    }

    // Utilization for a wave need, looked up through the building that serves it.
    public float WorkerUtilization(NeedType need)
    {
        foreach (var b in buildings)
        {
            if (b != null && b.unlocked && !b.IsSpa && b.needType == need)
                return Utilization(b);
        }

        return 0f;
    }

    // Managers

    // One Manager per wave building. The Lifeguard and Bellhop mapping to Pool and Bungalows
    // is inferred, the design documents only say "one per wave building".
    public string ManagerTitle(Building b)
    {
        if (b == null)
            return null;

        switch (b.id)
        {
            case "Reception": return "Receptionist";
            case "Restaurant": return "Chef";
            case "Bar": return "Bartender";
            case "Pool": return "Lifeguard";
            case "Bungalow": return "Bellhop";
            default: return null; // The Spa has no Manager.
        }
    }

    public bool HasManager(Building b) =>
        b != null && b.hasManager;

    public float ManagerMultiplier(Building b) =>
        HasManager(b) ? managerMultiplier : 1f;

    private string ManagerMultiplierText() =>
        managerMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // Hiring is only possible between waves. API only: the UI that calls it lives elsewhere (e.g. S5).
    public bool CanHireManager(Building b, out string reason)
    {
        reason = null;

        if (waveActive)
        {
            reason = "Hire available between waves";
            return false;
        }

        if (b == null || !b.unlocked || ManagerTitle(b) == null)
        {
            reason = "Invalid building";
            return false;
        }

        if (b.hasManager)
        {
            reason = "Manager already hired";
            return false;
        }

        if (coins < managerHireCost)
        {
            reason = $"Missing: {managerHireCost - coins} Coins";
            return false;
        }

        return true;
    }

    public bool TryHireManager(Building b)
    {
        if (!CanHireManager(b, out _))
            return false;

        if (!SpendCurrencies(managerHireCost, 0, 0, 0))
            return false;

        b.hasManager = true;
        resortValueSystem.Add(ref resortValue, resortValueSystem.managerHireGain);

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();

        return true;
    }

    // Combined service speed multiplier of a building: utilization x manager_multiplier.
    // Energy is not part of it yet. Never 0 for a built wave building.
    public float ServiceMultiplier(Building b)
    {
        float u = Utilization(b);

        return u > 0f ? u * ManagerMultiplier(b) : 0f;
    }

    public float ServiceMultiplier(NeedType need)
    {
        foreach (var b in buildings)
        {
            if (b != null && b.unlocked && !b.IsSpa && b.needType == need)
                return ServiceMultiplier(b);
        }

        return 0f;
    }

    public void AddReputation(int amount)
    {
        reputationScore = ResortValueSystem.AddReputation(reputation, amount);

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
    }

    private void RefreshHud()
    {
        RefreshCurrencyHud();
        ResortValueSystem.RefreshReputationHud(reputationHud, reputation);
        resortValueSystem.RefreshHud(resortValueHud, resortValue);
    }

    // Detached data snapshot only. No file I/O, PlayerPrefs, JSON or Android storage code.
    public GameProgressData CaptureProgressData()
    {
        var records = new List<BuildingProgressData>();

        foreach (var b in buildings)
        {
            if (b == null)
                continue;

            records.Add(new BuildingProgressData
            {
                buildingId = b.id,
                level = b.level,
                unlocked = b.unlocked,
                assignedWorkers = b.workers,
                hasManager = b.hasManager
            });
        }

        return new GameProgressData
        {
            economy = CaptureEconomy(),
            buildings = records.ToArray(),
            hiredWorkers = TotalWorkers,
            paidWorkerHires = paidHires
        };
    }

    // Restores a snapshot made by CaptureProgressData. Values are clamped to the current rules.
    public bool ApplyProgressData(GameProgressData data)
    {
        if (data == null)
            return false;

        if (data.economy != null)
        {
            RestoreCurrencies(data.economy);
            reputationScore = ResortValueSystem.RestoreReputation(data.economy.reputation);
            resortValue = Mathf.Max(0, data.economy.resortValue);
        }

        if (data.buildings != null)
        {
            foreach (var record in data.buildings)
            {
                if (record == null)
                    continue;

                var b = Find(record.buildingId);
                if (b == null)
                    continue;

                b.unlocked = record.unlocked;
                b.level = b.IsSpa || !b.unlocked ? 1 : Mathf.Clamp(record.level, 1, MaxLevel(b));
                b.workers = b.IsSpa || !b.unlocked
                    ? 0
                    : Mathf.Clamp(record.assignedWorkers, 0, WorkerSlots(b));
                b.hasManager = !b.IsSpa && b.unlocked && record.hasManager;
            }
        }

        paidHires = Mathf.Max(0, data.paidWorkerHires);
        resortValueSystem.ClearReviewLabel();

        foreach (var b in buildings)
            SyncArt(b);

        Close();

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();

        return true;
    }

    // Wave controller contract: true in WAVE_INCOMING, WAVE_ACTIVE, WAVE_PAUSED and WAVE_RESOLVING,
    // false in AWAITING_WAVE, WAVE_COMPLETE and UPGRADE.
    public void SetWaveActive(bool active)
    {
        waveActive = active;

        // Upgrade and unlock are not offered during a wave, so a panel that is open closes.
        if (active)
            Close();

        Refresh();
    }

    public void AddEnergy(int amount)
    {
        if (amount <= 0)
            return;

        CreditCurrencies(0, 0, amount, 0);
        RefreshHud();
        OnChanged?.Invoke();
    }

    public void AddRewards(int coinGain, int materialGain, int reputationGain, int valueGain)
    {
        CreditCurrencies(coinGain, materialGain, 0, 0);
        reputationScore = ResortValueSystem.AddReputation(reputation, reputationGain);
        resortValueSystem.Add(ref resortValue, valueGain);

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
    }

    public void AddReviewResortValue(int valueGain)
    {
        resortValueSystem.AddReview(ref resortValue, valueGain);

        OnChanged?.Invoke();
        RefreshHud();
        Refresh();
    }

    public void Open(Building b)
    {
        if (b == null)
            return;

        // During a wave a tap on a built building leads to Worker Allocation (S3), never to an upgrade.
        // A construction site does nothing during a wave.
        if (waveActive)
        {
            if (b.unlocked && !b.IsSpa)
                OnWorkerAllocationRequested?.Invoke(b);

            return;
        }

        if (overlay == null)
            BuildUI();

        selected = b;
        areaView = !b.unlocked || b.IsSpa;

        overlay.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        selected = null;

        if (overlay != null)
            overlay.SetActive(false);
    }

    private void Update()
    {
        RefreshHud();

        if (waveActive && selected != null)
            Close();

        if (selected != null)
            Refresh();
    }

    private void Refresh()
    {
        if (selected == null || overlay == null)
            return;

        var b = selected;

        heading.text = b.displayName + (areaView ? " - Area Unlock" : "");

        balances.text =
            $"Coins {coins}    Materials {materials}    {ResortValueSystem.ReputationBalanceText(reputation)}    {resortValueSystem.BalanceText(resortValue)}";

        action.gameObject.SetActive(!waveActive && !(areaView && b.unlocked));

        if (areaView)
        {
            summary.text = b.IsSpa
                ? "Adds: Spa requests for VIP guests\nVIP service - no wave need or Worker slots"
                : $"Adds: {b.needType.GetDisplayName()} demand\nIncludes: 1 Worker";

            summary.text += "\n\nGate: " +
                ResortValueSystem.ReputationGateText(reputation, b.reputationRequired);

            price.text =
                (b.unlockCoins < 0
                    ? "Unlock price not configured"
                    : $"Cost: {b.unlockCoins} Coins") +
                resortValueSystem.UnlockPreview(b);

            status.text = UnlockState(b);

            if (GateMet(b) && !b.unlocked && b.unlockCoins >= 0 && coins < b.unlockCoins)
                status.text += $" - Missing: {b.unlockCoins - coins} Coins";

            action.interactable =
                !b.unlocked &&
                !waveActive &&
                GateMet(b) &&
                b.unlockCoins >= 0 &&
                coins >= b.unlockCoins;

            actionLabel.text = ""; // The button caption is provided by the authored UI artwork.
        }
        else
        {
            bool next = !b.IsSpa && b.level < MaxLevel(b);

            string levelText = $"Level {b.level}";

            if (b.IsSpa)
            {
                levelText += "\nVIP service - unlock only";
            }
            else
            {
                levelText += $" / 5\nSeats: {Capacity(b)}" +
                    (next ? $" -> {Value(capacityByLevel, b.level)}" : "") +
                    $"\nWorker slots: {WorkerSlots(b)}" +
                    (next ? $" -> {Value(workerSlotsByLevel, b.level)}" : "") +
                    "\nManager: " +
                    (b.hasManager
                        ? $"{ManagerTitle(b)} x{ManagerMultiplierText()}"
                        : "not hired");

                if (next && b.level >= 3)
                    levelText += "\nUpgrade improves base quality";
            }

            summary.text = levelText;

            int cost = Value(upgradeCoinsByLevel, b.level);
            int mats = Value(upgradeMaterialsByLevel, b.level);

            bool configured =
                next &&
                cost >= 0 &&
                mats >= 0 &&
                (b.level < 3 || mats > 0);

            price.text = !next
                ? ""
                : $"Upgrade: {cost} Coins" +
                  (mats < 0
                      ? " + Materials (not configured)"
                      : mats > 0
                          ? $" + {mats} Materials"
                          : "") +
                  resortValueSystem.UpgradePreview(resortValueSystem.UpgradeGain(resortValueByLevel, b.level));

            status.text = !next
                ? "Max level"
                : !configured
                    ? "Materials cost must be configured before purchase"
                    : coins < cost || materials < mats
                        ? $"Missing: {Mathf.Max(0, cost - coins)} Coins / {Mathf.Max(0, mats - materials)} Materials"
                        : "Ready to upgrade";

            action.gameObject.SetActive(next && !waveActive);

            action.interactable =
                configured &&
                !waveActive &&
                coins >= cost &&
                materials >= mats;

            // Previous Upgrade Now (don't change this)
            actionLabel.text = "";
        }
    }

    public bool TryUpgrade(Building b)
    {
        if (b == null || !b.unlocked || b.IsSpa || waveActive || b.level < 1 || b.level >= 5)
            return false;

        int cost = Value(upgradeCoinsByLevel, b.level);
        int mats = Value(upgradeMaterialsByLevel, b.level);

        if ((b.level >= 3 && mats == 0) || !SpendCurrencies(cost, mats, 0, 0))
            return false;

        int gain = resortValueSystem.UpgradeGain(resortValueByLevel, b.level);

        resortValueSystem.Add(ref resortValue, gain);
        b.level++;

        SyncArt(b);

        OnChanged?.Invoke();
        Refresh();

        return true;
    }

    public bool TryUnlock(Building b)
    {
        if (b == null || b.unlocked || waveActive || !GateMet(b) || !SpendCurrencies(b.unlockCoins, 0, 0, 0))
            return false;

        b.unlocked = true;
        resortValueSystem.Add(ref resortValue, resortValueSystem.UnlockGain(b));

        // A wave building comes with one Worker, placed on the new building. The Spa has no slots.
        if (!b.IsSpa && WorkerSlots(b) > 0)
            b.workers = Mathf.Max(b.workers, 1);

        SyncArt(b);

        OnBuildingUnlocked?.Invoke(b);
        OnChanged?.Invoke();
        RefreshHud();
        Refresh();

        return true;
    }

    private void Confirm()
    {
        if (areaView)
            TryUnlock(selected);
        else
            TryUpgrade(selected);
    }

    private void SyncArt(Building b)
    {
        if (b.slot == null)
            return;

        b.slot.SetBuilt(b.unlocked);

        if (b.unlocked &&
            b.slot.art != null &&
            b.levelSprites != null &&
            b.level <= b.levelSprites.Length &&
            b.levelSprites[b.level - 1] != null)
        {
            b.slot.art.sprite = b.levelSprites[b.level - 1];
        }
    }

    private void BuildUI()
    {
        if (overlay == null)
        {
            overlay = new GameObject(
                "Building Detail and S9",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var shade = ImageObject("Backdrop", overlay.transform, new Color(0, 0, 0, 0.65f));
            Anchor(shade.transform, 0, 0, 1, 1);

            card = ImageObject("Building panel", overlay.transform, new Color(0.055f, 0.14f, 0.18f, 1));

            heading = Text("Title", 0, 0, 1, 1, 32);
            summary = Text("Level capacity and benefit", 0, 0, 1, 1, 26);
            price = Text("Cost", 0, 0, 1, 1, 23);
            status = Text("State", 0, 0, 1, 1, 21);
            balances = Text("Balances", 0, 0, 1, 1, 17);

            action = Button("Upgrade Now", 0.53f, 0.08f, 0.92f, 0.18f, out actionLabel);

            TMP_Text closeLabel;
            closeButton = Button("Later / Close", 0.08f, 0.08f, 0.47f, 0.18f, out closeLabel);

            LayoutPanel();

            overlay.SetActive(false);
        }

        // A panel saved by an earlier version may still contain the removed Manager hire button.
        var staleButton = card.transform.Find("Hire Manager");

        if (staleButton != null)
        {
            if (Application.isPlaying)
                Destroy(staleButton.gameObject);
            else
                DestroyImmediate(staleButton.gameObject);
        }

        // LayoutPanel();
    }

    // Single place that defines the panel layout, so it also fixes panels created earlier.
    private void LayoutPanel()
    {
        Anchor(card.transform, 0.22f, 0.10f, 0.78f, 0.90f);

        Anchor(heading.transform, 0.08f, 0.84f, 0.92f, 0.95f);
        Anchor(summary.transform, 0.08f, 0.43f, 0.92f, 0.82f);
        Anchor(price.transform, 0.08f, 0.29f, 0.92f, 0.43f);
        Anchor(status.transform, 0.08f, 0.19f, 0.92f, 0.29f);
        Anchor(balances.transform, 0.05f, 0.01f, 0.95f, 0.07f);

        Anchor(closeButton.transform, 0.08f, 0.08f, 0.47f, 0.18f);
        Anchor(action.transform, 0.53f, 0.08f, 0.92f, 0.18f);
    }

    private GameObject ImageObject(string label, Transform parent, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));

        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        return go;
    }

    private void Anchor(Transform transform, float x0, float y0, float x1, float y1)
    {
        var r = (RectTransform)transform;

        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private TMP_Text Text(string label, float x0, float y0, float x1, float y1, int size)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));

        go.transform.SetParent(card.transform, false);
        Anchor(go.transform, x0, y0, x1, y1);

        var text = go.GetComponent<TextMeshProUGUI>();

        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    private Button Button(string label, float x0, float y0, float x1, float y1, out TMP_Text text)
    {
        var go = ImageObject(label, card.transform, new Color(0.85f, 0.34f, 0.08f));

        Anchor(go.transform, x0, y0, x1, y1);

        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        text = Text(label + " label", 0, 0, 1, 1, 25);
        text.transform.SetParent(go.transform, false);
        text.text = label;

        return button;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Diminishing returns: the second Worker adds less than the first, the third less than the second.
        utilizationThird = Mathf.Min(utilizationThird, utilizationSecond);

        if (buildings == null)
            return;

        foreach (var b in buildings)
        {
            if (b != null)
                b.MigrateLegacyNeed();
        }
    }

    [ContextMenu("Debug: Seed starting Workers")]
    private void DebugSeedWorkers()
    {
        if (Application.isPlaying)
            return;

        SeedStartingWorkers();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void ConfigureHudNumber(TMP_Text text, bool iconOnRight)
    {
        if (text == null)
            return;

        UnityEditor.Undo.RecordObject(text, "Single line HUD number");

        var rect = text.rectTransform;

        UnityEditor.Undo.RecordObject(rect, "Anchor HUD number beside icon");

        var before = new Vector3[4];
        var after = new Vector3[4];

        rect.GetWorldCorners(before);

        rect.pivot = new Vector2(iconOnRight ? 1f : 0f, rect.pivot.y);

        rect.GetWorldCorners(after);

        int corner = iconOnRight ? 2 : 1;

        rect.position += before[corner] - after[corner];

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        text.alignment = iconOnRight
            ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft;

        var fitter = text.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = UnityEditor.Undo.AddComponent<ContentSizeFitter>(text.gameObject);

        UnityEditor.Undo.RecordObject(fitter, "Grow number horizontally");

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        UnityEditor.EditorUtility.SetDirty(text);
        UnityEditor.EditorUtility.SetDirty(rect);
        UnityEditor.EditorUtility.SetDirty(fitter);
    }

    [ContextMenu("Validate Building Configuration")]
    private void ValidateBuildingConfiguration()
    {
        foreach (var b in buildings)
        {
            if (b.id == "Pool" || b.id == "Bungalow" || b.id == "Spa")
            {
                if (b.reputationRequired < 0 || b.unlockCoins < 0)
                    throw new InvalidOperationException(b.id + ": missing unlock price or gate");
            }

            if (!b.IsSpa && !b.needType.IsWaveNeed())
                throw new InvalidOperationException(b.id + ": missing wave need type");

            if (b.IsSpa)
            {
                if (b.level != 1 || b.needType != NeedType.None || b.workers != 0 || b.hasManager ||
                    Capacity(b) != 0 || WorkerSlots(b) != 0)
                {
                    throw new InvalidOperationException(
                        "Spa must be unlock-only, without wave capacity or Workers"
                    );
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    if (Value(upgradeCoinsByLevel, i) < 0 ||
                        Value(upgradeMaterialsByLevel, i) < 0 ||
                        resortValueSystem.UpgradeGain(resortValueByLevel, i) < 0)
                    {
                        throw new InvalidOperationException(
                            b.id + ": missing cost for Lv" + (i + 1) +
                            " (the Materials amounts for Lv4/5 are not specified by the design documents)"
                        );
                    }

                    if (i >= 3 && Value(upgradeMaterialsByLevel, i) == 0)
                        throw new InvalidOperationException("Lv4/5 must require Materials");

                    if (Value(capacityByLevel, i) < 1 || Value(workerSlotsByLevel, i) < 1)
                        throw new InvalidOperationException(b.id + ": invalid level stats");
                }
            }

            Debug.Log(
                b.displayName +
                (b.IsSpa ? ": unlock only; gate " : ": configured Lv1-5; gate ") +
                b.reputationRequired +
                "; unlock " +
                b.unlockCoins +
                " Coins",
                this
            );
        }

        Debug.Log(
            "Building configuration valid for all six buildings. Spa: unlock only, VIP service, no wave need or Worker slots.",
            this
        );
    }

    private void BindExistingHud()
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text.gameObject.scene != gameObject.scene || text.transform.parent == null)
                continue;

            BindCurrencyHud(text);

            switch (text.transform.parent.name)
            {
                case "Reputation":
                    reputationHud = text;
                    break;

                case "ResortValue":
                    resortValueHud = text;
                    break;
            }
        }
    }

    [ContextMenu("Configure Scene")]
    private void ConfigureScene()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.Undo.RecordObject(this, "Configure buildings");

        var spriteIds = UnityEditor.AssetDatabase.FindAssets("t:Sprite");

        foreach (var b in buildings)
        {
            b.MigrateLegacyNeed();

            string prefix =
                b.id == "Bar" ? "Beach_bar" :
                b.id == "Bungalow" ? "Bungalows" :
                b.id;

            b.levelSprites = new Sprite[MaxLevel(b)];

            foreach (var guid in spriteIds)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

                if (path.IndexOf(prefix + "_lvl_", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var sprite = asset as Sprite;

                    if (sprite == null)
                        continue;

                    for (int i = 0; i < b.levelSprites.Length; i++)
                    {
                        if (sprite.name.StartsWith(
                            prefix + "_lvl_" + (i + 1) + "_",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            b.levelSprites[i] = sprite;
                        }
                    }
                }
            }
        }

        // Preserve any already configured thresholds.
        foreach (var b in buildings)
        {
            if (b.reputationRequired >= 0)
                continue;

            if (b.id == "Pool")
                b.reputationRequired = 100;
            else if (b.id == "Bungalow")
                b.reputationRequired = 300;
            else if (b.id == "Spa")
                b.reputationRequired = 600;
        }

        foreach (var b in buildings)
        {
            if (b.id == "Spa")
            {
                b.needType = NeedType.None;
                b.workers = 0;
                b.level = 1;

                if (b.unlockCoins < 0)
                    b.unlockCoins = 2500;

                if (b.unlockResortValueGain == 0)
                    b.unlockResortValueGain = 500;
            }
            else if (b.id == "Pool" && b.unlockResortValueGain == 0)
            {
                b.unlockResortValueGain = 200;
            }
            else if (b.id == "Bungalow" && b.unlockResortValueGain == 0)
            {
                b.unlockResortValueGain = 350;
            }
        }

        if (resortValueByLevel != null &&
            Array.TrueForAll(resortValueByLevel, value => value == 0))
        {
            resortValueByLevel = new[] { 0, 75, 150, 250, 500 };
        }

        BindExistingHud();

        ConfigureCurrencyHud();
        ConfigureHudNumber(reputationHud, false);
        ConfigureHudNumber(resortValueHud, false);

        RefreshHud();
        BuildUI();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private void OnDestroy()
    {
        foreach (var pair in listeners)
        {
            if (pair.Key != null)
            {
                pair.Key.onBuiltClicked.RemoveListener(pair.Value);
                pair.Key.onSiteClicked.RemoveListener(pair.Value);
            }
        }

        if (overlay != null)
            Destroy(overlay);
    }
}

// Storage-neutral, versioned data contract. No Unity object or scene references.
[Serializable]
public class GameProgressData
{
    public int schemaVersion = 1;
    public EconomyData economy = new EconomyData();
    public BuildingProgressData[] buildings = new BuildingProgressData[0];

    // Total number of Workers on buildings (derived, informational).
    public int hiredWorkers;

    // Workers bought for Coins. Drives the price of the next hire.
    public int paidWorkerHires;
}

[Serializable]
public class EconomyData
{
    public int coins;
    public int materials;
    public int energy;
    public int gems;
    public int reputation;
    public int resortValue;
}

[Serializable]
public class BuildingProgressData
{
    public string buildingId;
    public int level;
    public bool unlocked;
    public int assignedWorkers;
    public bool hasManager;
}
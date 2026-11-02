using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// FR-003, S9 and level-derived capacity. MVP only.
public class MvpBuildingSystem : MonoBehaviour
{
    [Serializable]
    public class Building
    {
        public string id, displayName;
        public NeedType needType;
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("need")]
        private string legacyNeed;
        public bool unlocked;
        [Range(1, 5)] public int level = 1;
        [Tooltip("MVP Reputation gate. -1 means not specified by the design documents.")]
        public int reputationRequired = -1;
        public int unlockCoins = -1;
        [Min(0)] public int unlockResortValueGain;
        [Tooltip("Optional visual progression, indexed by level.")]
        public Sprite[] levelSprites;
        [HideInInspector] public int workers;
        [NonSerialized] public BuildingSlot slot;
        public Building(string key, string label, NeedType service, bool built, int price, int reputationGate = -1)
        { id = key; displayName = label; needType = service; unlocked = built; unlockCoins = price; reputationRequired = reputationGate; }
        public bool IsSpa => id == "Spa";
        public void MigrateLegacyNeed()
        {
            if (NeedTypes.TryFromLegacyName(legacyNeed, out var migrated))
            { needType = migrated; legacyNeed = null; }
            if (IsSpa) needType = NeedType.None;
        }
    }

    [Header("Balances - connect rewards through AddRewards")]
    [Min(0)] public int coins;
    [Min(0)] public int materials;
    [Min(0)] public int energy;
    [Min(0)] public int gems;
    [Header("Existing HUD text references")]
    public TMP_Text coinsHud;
    public TMP_Text materialsHud;
    public TMP_Text energyHud;
    public TMP_Text gemsHud;
    public TMP_Text reputationHud;
    public TMP_Text resortValueHud;
    [Min(0)] public int reputation;
    [Min(0)] public int resortValue;
    [Header("Phase - wave controller calls SetWaveActive")]
    public bool waveActive;
    [Header("Wave building levels - Build document 5.3.6 examples / tuning")]
    public int[] capacityByLevel = { 3, 5, 7, 7, 10 };
    public int[] workerSlotsByLevel = { 1, 1, 1, 2, 3 };
    public int[] upgradeCoinsByLevel = { 0, 500, 1200, 2500, 5000 };
    [Tooltip("Lv4/5 require Materials. Costs are tuning. A negative value blocks purchase until configured.")]
    public int[] upgradeMaterialsByLevel = { 0, 0, 0, 1000, 2500 };
    [Tooltip("Proposed Resort Value gains, tune with economy data.")]
    public int[] resortValueByLevel = { 0, 75, 150, 250, 500 };
    [Header("MVP buildings - proposed Reputation tuning: 100 / 300 / 600")]
    public Building[] buildings = {
        new Building("Reception", "Reception", NeedType.Rooms, true, 0),
        new Building("Restaurant", "Restaurant", NeedType.Food, true, 0),
        new Building("Bar", "Beach Bar", NeedType.Drinks, true, 0),
        new Building("Pool", "Pool", NeedType.Relax, false, 1200, 100),
        new Building("Bungalow", "Bungalows", NeedType.OceanView, false, 2500, 300),
        new Building("Spa", "Spa", NeedType.None, false, 2500, 600)
    };
    public event Action OnChanged;
    public event Action<Building> OnBuildingUnlocked;
    private readonly List<KeyValuePair<BuildingSlot, UnityAction>> listeners = new List<KeyValuePair<BuildingSlot, UnityAction>>();
    private Building selected;
    private bool areaView;
    [SerializeField, HideInInspector] private GameObject overlay, card;
    [SerializeField, HideInInspector] private TMP_Text heading, summary, price, status, balances, actionLabel;
    [SerializeField, HideInInspector] private Button action, closeButton;

    private void Awake()
    {
        foreach (var b in buildings) b.MigrateLegacyNeed();
        foreach (var b in buildings)
            if (b.IsSpa) { b.level = 1; b.workers = 0; b.needType = NeedType.None; }
        foreach (var slot in GetComponentsInChildren<BuildingSlot>(true))
        {
            var b = Find(slot.buildingId);
            if (b == null) continue;
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
        foreach (var b in buildings) SyncArt(b);
        BuildUI();
        action.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Close);
        RefreshHud();
    }

    public Building Find(string id)
    {
        foreach (var b in buildings) if (b.id == id) return b;
        return null;
    }
    public int Capacity(Building b) => b.IsSpa ? 0 : Value(capacityByLevel, b.level - 1);
    public int MaxLevel(Building b) => b.IsSpa ? 1 : 5;
    public int WorkerSlots(Building b) => b.IsSpa ? 0 : Value(workerSlotsByLevel, b.level - 1);
    private int Value(int[] data, int index) => data != null && index >= 0 && index < data.Length ? data[index] : -1;
    private bool GateMet(Building b) => b.reputationRequired >= 0 && reputation >= b.reputationRequired;
    public string UnlockState(Building b) => b.unlocked ? "Unlocked" : !GateMet(b) ? "Locked" :
        b.unlockCoins >= 0 && coins < b.unlockCoins ? "Not affordable" : "Available";

    private void SetHud(TMP_Text target, int value)
    {
        if (target == null) return;
        string label = value.ToString();
        if (target.text != label) target.text = label;
    }
    public void AddReputation(int amount)
    {
        reputation += Mathf.Max(0, amount);
        OnChanged?.Invoke(); RefreshHud(); Refresh();
    }
    private void RefreshHud()
    {
        SetHud(coinsHud, coins); SetHud(materialsHud, materials);
        SetHud(energyHud, energy); SetHud(gemsHud, gems);
        SetHud(reputationHud, reputation); SetHud(resortValueHud, resortValue);
    }

    // Detached data snapshot only. No file I/O, PlayerPrefs, JSON or Android storage code.
    // Covers implemented balances and building progress; wave/VIP systems can extend it later.
    public MvpGameProgressData CaptureProgressData()
    {
        var records = new List<MvpBuildingProgressData>();
        foreach (var b in buildings)
        {
            if (b == null) continue;
            records.Add(new MvpBuildingProgressData {
                buildingId = b.id, level = b.level, unlocked = b.unlocked, assignedWorkers = b.workers
            });
        }
        return new MvpGameProgressData {
            economy = new MvpEconomyData {
                coins = coins, materials = materials, energy = energy, gems = gems,
                reputation = reputation, resortValue = resortValue
            },
            buildings = records.ToArray()
        };
    }

    public void SetWaveActive(bool active) { waveActive = active; Refresh(); }
    public void AddRewards(int coinGain, int materialGain, int reputationGain, int valueGain)
    {
        coins += Mathf.Max(0, coinGain); materials += Mathf.Max(0, materialGain);
        reputation += Mathf.Max(0, reputationGain); resortValue += Mathf.Max(0, valueGain);
        OnChanged?.Invoke(); Refresh();
    }
    public void Open(Building b)
    {
        if (overlay == null) BuildUI();
        selected = b; areaView = !b.unlocked || b.IsSpa;
        overlay.SetActive(true); Refresh();
    }
    public void Close() { selected = null; if (overlay != null) overlay.SetActive(false); }
    private void Update() { RefreshHud(); if (selected != null) Refresh(); }

    private void Refresh()
    {
        if (selected == null || overlay == null) return;
        var b = selected;
        var rect = (RectTransform)card.transform;
        rect.anchorMin = new Vector2(0.18f, 0.08f);
        rect.anchorMax = new Vector2(0.82f, 0.92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        heading.text = b.displayName + (areaView ? " - Area Unlock" : "");
        balances.text = $"Coins {coins}    Materials {materials}    Reputation {reputation}    Resort Value {resortValue}";
        action.gameObject.SetActive(!waveActive && !(areaView && b.unlocked));
        if (areaView)
        {
            summary.text = b.IsSpa ? "Adds: Spa requests for VIP guests\nVIP service - no wave need or Worker slots" :
                $"Adds: {b.needType.GetDisplayName()} demand\nIncludes: 1 Worker";
            summary.text += "\n\nGate: " + (b.reputationRequired < 0 ? "Reputation threshold not configured" : $"Reputation {reputation} / {b.reputationRequired}");
            price.text = (b.unlockCoins < 0 ? "Unlock price not configured" : $"Cost: {b.unlockCoins} Coins") + (b.unlockResortValueGain > 0 ? $"\nResort Value: +{b.unlockResortValueGain}" : "");
            status.text = UnlockState(b);
            if (GateMet(b) && !b.unlocked && b.unlockCoins >= 0 && coins < b.unlockCoins)
                status.text += $" - Missing: {b.unlockCoins - coins} Coins";
            if (waveActive) status.text += "\nUnlock available between waves";
            action.interactable = !b.unlocked && !waveActive && GateMet(b) && b.unlockCoins >= 0 && coins >= b.unlockCoins;
            actionLabel.text = "Unlock";
        }
        else
        {
            bool next = !b.IsSpa && b.level < MaxLevel(b);
            summary.text = $"Level {b.level}" + (b.IsSpa ? "\nVIP service - unlock only" :
                $" / 5\nSeats: {Capacity(b)}" + (next ? $" -> {Value(capacityByLevel, b.level)}" : "") +
                $"\nWorker slots: {WorkerSlots(b)}" + (next ? $" -> {Value(workerSlotsByLevel, b.level)}" : ""));
            if (next && !b.IsSpa && b.level >= 3) summary.text += "\nUpgrade improves base quality";
            int cost = Value(upgradeCoinsByLevel, b.level), mats = Value(upgradeMaterialsByLevel, b.level);
            bool configured = next && cost >= 0 && mats >= 0 && (b.level < 3 || mats > 0);
            price.text = !next ? "" : $"Upgrade: {cost} Coins" + (mats < 0 ? " + Materials (not configured)" : mats > 0 ? $" + {mats} Materials" : "") +
                (Value(resortValueByLevel, b.level) > 0 ? $"\nResort Value gain: +{Value(resortValueByLevel, b.level)}" : "");
            status.text = !next ? "Max level" :
                !configured ? "Materials cost must be configured before purchase" :
                coins < cost || materials < mats ? $"Missing: {Mathf.Max(0, cost - coins)} Coins / {Mathf.Max(0, mats - materials)} Materials" : "Ready to upgrade";
            if (waveActive) status.text = "Upgrades available between waves";
            action.gameObject.SetActive(next && !waveActive);
            action.interactable = configured && !waveActive && coins >= cost && materials >= mats;
            actionLabel.text = "Upgrade Now";
        }
    }

    public bool TryUpgrade(Building b)
    {
        if (b == null || !b.unlocked || b.IsSpa || waveActive || b.level < 1 || b.level >= 5) return false;
        int cost = Value(upgradeCoinsByLevel, b.level), mats = Value(upgradeMaterialsByLevel, b.level);
        if (cost < 0 || mats < 0 || (b.level >= 3 && mats == 0) || coins < cost || materials < mats) return false;
        int gain = Mathf.Max(0, Value(resortValueByLevel, b.level));
        coins -= cost; materials -= mats; resortValue += gain; b.level++;
        SyncArt(b); OnChanged?.Invoke(); Refresh(); return true;
    }
    public bool TryUnlock(Building b)
    {
        if (b == null || b.unlocked || waveActive || !GateMet(b) || b.unlockCoins < 0 || coins < b.unlockCoins) return false;
        coins -= b.unlockCoins; b.unlocked = true; resortValue += Mathf.Max(0, b.unlockResortValueGain);
        if (!b.IsSpa) b.workers++;
        SyncArt(b); OnBuildingUnlocked?.Invoke(b); OnChanged?.Invoke(); Refresh(); return true;
    }
    private void Confirm() { if (areaView) TryUnlock(selected); else TryUpgrade(selected); }
    private void SyncArt(Building b)
    {
        if (b.slot == null) return;
        b.slot.SetBuilt(b.unlocked);
        if (b.unlocked && b.slot.art != null && b.levelSprites != null && b.level <= b.levelSprites.Length && b.levelSprites[b.level - 1] != null)
            b.slot.art.sprite = b.levelSprites[b.level - 1];
    }

    private void BuildUI()
    {
        if (overlay != null) return;
        overlay = new GameObject("MVP Building Detail and S9", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        var shade = ImageObject("Backdrop", overlay.transform, new Color(0, 0, 0, 0.65f));
        Anchor(shade.transform, 0, 0, 1, 1);
        card = ImageObject("Building panel", overlay.transform, new Color(0.055f, 0.14f, 0.18f, 1));
        heading = Text("Title", 0.08f, 0.84f, 0.92f, 0.95f, 32);
        summary = Text("Level capacity and benefit", 0.08f, 0.43f, 0.92f, 0.82f, 26);
        price = Text("Cost", 0.08f, 0.29f, 0.92f, 0.43f, 23);
        status = Text("State", 0.08f, 0.19f, 0.92f, 0.29f, 21);
        balances = Text("Balances", 0.05f, 0.01f, 0.95f, 0.07f, 17);
        action = Button("Upgrade Now", 0.53f, Confirm, out actionLabel);
        TMP_Text closeLabel; closeButton = Button("Later / Close", 0.08f, Close, out closeLabel);
        overlay.SetActive(false);
    }
    private GameObject ImageObject(string label, Transform parent, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color; return go;
    }
    private void Anchor(Transform transform, float x0, float y0, float x1, float y1)
    {
        var r = (RectTransform)transform; r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
    private TMP_Text Text(string label, float x0, float y0, float x1, float y1, int size)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(card.transform, false);
        Anchor(go.transform, x0, y0, x1, y1);
        var text = go.GetComponent<TextMeshProUGUI>(); text.fontSize = size; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    private Button Button(string label, float left, UnityAction click, out TMP_Text text)
    {
        var go = ImageObject(label, card.transform, new Color(0.85f, 0.34f, 0.08f));
        Anchor(go.transform, left, 0.08f, left + 0.39f, 0.18f);
        var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); 
        text = Text(label + " label", 0, 0, 1, 1, 25); text.transform.SetParent(go.transform, false); text.text = label;
        return button;
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (buildings == null) return;
        foreach (var b in buildings) if (b != null) b.MigrateLegacyNeed();
    }
    private void ConfigureHudNumber(TMP_Text text, bool iconOnRight)
    {
        if (text == null) return;
        UnityEditor.Undo.RecordObject(text, "Single line HUD number");
        var rect = text.rectTransform;
        UnityEditor.Undo.RecordObject(rect, "Anchor HUD number beside icon");
        var before = new Vector3[4]; var after = new Vector3[4];
        rect.GetWorldCorners(before);
        rect.pivot = new Vector2(iconOnRight ? 1f : 0f, rect.pivot.y);
        rect.GetWorldCorners(after);
        int corner = iconOnRight ? 2 : 1;
        rect.position += before[corner] - after[corner];
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = iconOnRight ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        var fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = UnityEditor.Undo.AddComponent<ContentSizeFitter>(text.gameObject);
        UnityEditor.Undo.RecordObject(fitter, "Grow number horizontally");
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        UnityEditor.EditorUtility.SetDirty(text);
        UnityEditor.EditorUtility.SetDirty(rect);
        UnityEditor.EditorUtility.SetDirty(fitter);
    }
    [ContextMenu("Validate MVP Building Configuration")]
    private void ValidateBuildingConfiguration()
    {
        foreach (var b in buildings)
        {
            if (b.id == "Pool" || b.id == "Bungalow" || b.id == "Spa")
                if (b.reputationRequired < 0 || b.unlockCoins < 0) throw new InvalidOperationException(b.id + ": missing unlock price or gate");
            if (!b.IsSpa && !b.needType.IsWaveNeed())
                throw new InvalidOperationException(b.id + ": missing wave need type");
            if (b.IsSpa)
            {
                if (b.level != 1 || b.needType != NeedType.None || b.workers != 0 || Capacity(b) != 0 || WorkerSlots(b) != 0)
                    throw new InvalidOperationException("Spa must be unlock-only, without wave capacity or Workers");
            }
            else for (int i = 0; i < 5; i++)
            {
                if (Value(upgradeCoinsByLevel, i) < 0 || Value(upgradeMaterialsByLevel, i) < 0 || Value(resortValueByLevel, i) < 0)
                    throw new InvalidOperationException(b.id + ": missing level costs");
                if (i >= 3 && Value(upgradeMaterialsByLevel, i) == 0) throw new InvalidOperationException("Lv4/5 must require Materials");
                if (Value(capacityByLevel, i) < 1 || Value(workerSlotsByLevel, i) < 1)
                    throw new InvalidOperationException(b.id + ": invalid level stats");
            }
            Debug.Log(b.displayName + (b.IsSpa ? ": unlock only; gate " : ": configured Lv1-5; gate ") + b.reputationRequired + "; unlock " + b.unlockCoins + " Coins", this);
        }
        Debug.Log("MVP configuration valid for all six buildings. Spa: unlock only, VIP service, no wave need or Worker slots.", this);
    }
    private void BindExistingHud()
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text.gameObject.scene != gameObject.scene || text.transform.parent == null) continue;
            switch (text.transform.parent.name)
            {
                case "Coins": coinsHud = text; break;
                case "Materials": materialsHud = text; break;
                case "Energy": energyHud = text; break;
                case "Gems": gemsHud = text; break;
                case "Reputation": reputationHud = text; break;
                case "ResortValue": resortValueHud = text; break;
            }
        }
    }
    [ContextMenu("Configure MVP Scene")]
    private void ConfigureScene()
    {
        if (Application.isPlaying) return;
        UnityEditor.Undo.RecordObject(this, "Configure MVP buildings");
        var spriteIds = UnityEditor.AssetDatabase.FindAssets("t:Sprite");
        foreach (var b in buildings)
        {
            b.MigrateLegacyNeed();
            // Spa is unlock-only and uses its single built sprite.
            string prefix = b.id == "Bar" ? "Beach_bar" : b.id == "Bungalow" ? "Bungalows" : b.id;
            b.levelSprites = new Sprite[MaxLevel(b)];
            foreach (var guid in spriteIds)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf(prefix + "_lvl_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var sprite = asset as Sprite;
                    if (sprite == null) continue;
                    for (int i = 0; i < b.levelSprites.Length; i++)
                        if (sprite.name.StartsWith(prefix + "_lvl_" + (i + 1) + "_", StringComparison.OrdinalIgnoreCase))
                            b.levelSprites[i] = sprite;
                }
            }
        }
        // Proposed tuning approved by the user; preserve any already configured thresholds.
        foreach (var b in buildings)
        {
            if (b.reputationRequired >= 0) continue;
            if (b.id == "Pool") b.reputationRequired = 100;
            else if (b.id == "Bungalow") b.reputationRequired = 300;
            else if (b.id == "Spa") b.reputationRequired = 600;
        }
        foreach (var b in buildings)
        {
            if (b.id == "Spa")
            {
                b.needType = NeedType.None; b.workers = 0; b.level = 1;
                if (b.unlockCoins < 0) b.unlockCoins = 2500;
                if (b.unlockResortValueGain == 0) b.unlockResortValueGain = 500;
            }
            else if (b.id == "Pool" && b.unlockResortValueGain == 0) b.unlockResortValueGain = 200;
            else if (b.id == "Bungalow" && b.unlockResortValueGain == 0) b.unlockResortValueGain = 350;
        }
        if (resortValueByLevel != null && Array.TrueForAll(resortValueByLevel, value => value == 0))
            resortValueByLevel = new[] { 0, 75, 150, 250, 500 };
        BindExistingHud();
        ConfigureHudNumber(coinsHud, true);
        ConfigureHudNumber(materialsHud, true);
        ConfigureHudNumber(energyHud, true);
        ConfigureHudNumber(gemsHud, true);
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
            if (pair.Key != null) { pair.Key.onBuiltClicked.RemoveListener(pair.Value); pair.Key.onSiteClicked.RemoveListener(pair.Value); }
        if (overlay != null) Destroy(overlay);
    }
}

// Storage-neutral, versioned data contract. No Unity object or scene references.
// Building IDs reconnect progress to design data; capacities and prices stay in design configuration.
[Serializable]
public class MvpGameProgressData
{
    public int schemaVersion = 1;
    public MvpEconomyData economy = new MvpEconomyData();
    public MvpBuildingProgressData[] buildings = new MvpBuildingProgressData[0];
}

[Serializable]
public class MvpEconomyData
{
    public int coins;
    public int materials;
    public int energy;
    public int gems;
    public int reputation;
    public int resortValue;
}

[Serializable]
public class MvpBuildingProgressData
{
    public string buildingId;
    public int level;
    public bool unlocked;
    public int assignedWorkers;
}

using System;
using UnityEngine;

// the sole Unity owner of wave lifecycle, using the original GameState/GameData.
public class GameStateManager : MonoBehaviour
{
    [SerializeField] private GameData game_data;
    [SerializeField] private BuildingSystem buildings;
    [SerializeField] private WaveUIController waveUI;
    [Header("Scene components - drag the task objects here")]
    [SerializeField] private WaveModelComponent waveModel;
    [SerializeField] private WaveStatesComponent states;
    [SerializeField] private WaveServicesComponent services;
    [Header("Next wave forecast")]
    [SerializeField] private WavePreparationUI preparationUI;
    [Header("Ship")]
    [SerializeField] private ShipController ship; // SHIP: optional, drag the ShipController object here
    [Header("Wave results")]
    [SerializeField] private WaveRewardsComponent waveRewards;
    [Header("Wave complete (S4) and recommended investment (S5)")]
    [SerializeField] private WaveCompleteUI waveCompleteUI;
    [SerializeField] private UpgradeRecommendationUI upgradeUI;
    [Header("lifecycle")]
    [SerializeField, Min(0)] private float incomingSeconds = 3;
    private float incomingRemaining;
    private int waveNumber = 1;
    private int lastEnergyRewardedWave;
    private bool allComplete;
    private bool ready;
    // Data of the wave that just ended, kept until the player leaves S4 / S5.
    private WaveResult completedResult;
    private WaveSnapshot completedSnapshot;
    private UpgradeOffer pendingOffer;
    private string pendingReason = "";
    private bool applicationPaused, focusLost, automaticallyPaused;
    public GameState State => states != null ? states.State : GameState.AWAITING_WAVE;
    public int WaveNumber => waveNumber;
    public WaveSnapshot CurrentSnapshot { get; private set; }
    public event Action<GameState> StateChanged;
    public event Action<WaveSnapshot> ProgressChanged;

    private void Start()
    {
        if (game_data == null) game_data = GetComponent<GameData>();
        if (game_data == null || buildings == null || waveUI == null || waveModel == null ||
            states == null || services == null || !services.IsConfigured || waveModel.Count == 0 ||
            preparationUI == null || !preparationUI.IsConfigured || waveRewards == null || !waveRewards.IsConfigured)
        {
            Debug.LogError("Assign GameData, Buildings, Wave UI and all three task components; configure the wave list and service Buildings reference.", this);
            enabled = false;
            return;
        }
        if (waveCompleteUI == null || !waveCompleteUI.IsConfigured || upgradeUI == null || !upgradeUI.IsConfigured)
        {
            Debug.LogError("Assign Wave Complete UI (S4) and Upgrade Recommendation UI (S5) and fill in their required references.", this);
            enabled = false;
            return;
        }
        try { waveRewards.ValidateConfiguration(); }
        catch (Exception exception) { Debug.LogError("Wave rewards: " + exception.Message, this); enabled = false; return; }
        ready = true;
        states.Changed += ApplyState;
        waveUI.StartWaveRequested += StartWave;
        waveUI.FlowActionRequested += FlowAction;
        buildings.OnChanged += RefreshForecast;
        buildings.OnChanged += RefreshUpgradeOffer;
        waveCompleteUI.ContinueRequested += ContinueFromWaveComplete;
        upgradeUI.UpgradeNowRequested += UpgradeNow;
        upgradeUI.LaterRequested += FinishUpgrade;
        ApplyState(State);
        RefreshForecast();
    }

    public void StartWave()
    {
        if (!ready || !isActiveAndEnabled || State != GameState.AWAITING_WAVE || allComplete) return;
        try
        {
            services.BeginWave(waveModel.Get(waveNumber));
        }
        catch (ArgumentException exception) { Debug.LogError("Wave configuration: " + exception.Message, this); return; }
        buildings.Close();
        completedResult = null; completedSnapshot = null;
        pendingOffer = null; pendingReason = "";
        waveUI.SetCustomerFeedback(null, null);
        incomingRemaining = Mathf.Max(0, incomingSeconds);
        Publish(true);
        states.TryEnter(GameState.WAVE_INCOMING);
        if (ship != null) ship.Arrive(); // SHIP: spawn and sail to the dock
        CheckApplicationPause();
    }

    private void Update()
    {
        if (!ready) return;
        switch (State)
        {
            case GameState.WAVE_INCOMING:
                // SHIP: wait for the ship to arrive; fall back to the timer if no ship is assigned
                if (ship != null)
                {
                    if (ship.HasArrived) states.TryEnter(GameState.WAVE_ACTIVE);
                }
                else
                {
                    incomingRemaining -= Time.deltaTime;
                    if (incomingRemaining <= 0) states.TryEnter(GameState.WAVE_ACTIVE);
                }
                break;
            case GameState.WAVE_ACTIVE:
                services.Tick(Time.deltaTime);
                Publish(false);
                if (ship != null && CurrentSnapshot.ArrivalsClosed) ship.SetEmpty(); // SHIP: all guests are out
                if (services.IsComplete) states.TryEnter(GameState.WAVE_RESOLVING, true);
                break;
            case GameState.WAVE_RESOLVING:
                if (ship != null) ship.Depart(); // SHIP: sail back to the spawn point
                EvaluateWaveResult();            // Evaluate and award Energy before ClearWave.
                states.TryEnter(GameState.WAVE_COMPLETE); // S4 stays open until the player presses Continue.
                if (completedResult == null) ContinueFromWaveComplete(); // nothing to rate -> no S4
                break;
        }
    }

    private void Publish(bool rebuild)
    {
        CurrentSnapshot = services.Snapshot();
        waveUI.RenderWave(CurrentSnapshot, rebuild);
        ProgressChanged?.Invoke(CurrentSnapshot);

        // Optional debug: live satisfaction roughly once per 60 frames. Delete after testing.
        if (waveRewards != null && !rebuild && Time.frameCount % 60 == 0)
            Debug.Log($"[Wave {CurrentSnapshot.Number}] live satisfaction: {waveRewards.EvaluateLive(CurrentSnapshot):F1}");
    }

    private void RefreshForecast()
    {
        if (!ready || (State != GameState.AWAITING_WAVE && State != GameState.UPGRADE) || allComplete) return;
        try
        {
            var forecast = services.Forecast(waveModel.Get(waveNumber));
            waveUI.RenderWave(forecast, true);
            preparationUI.RenderForecast(forecast);
        }
        catch (ArgumentException exception) { Debug.LogError("Wave configuration: " + exception.Message, this); }
    }

    private void ApplyState(GameState state)
    {
        game_data.SetState(state);
        bool canUpgrade = state == GameState.AWAITING_WAVE || state == GameState.UPGRADE;
        buildings.SetWaveActive(!canUpgrade);
        waveUI.ApplyGameState(state, waveNumber, allComplete);
        preparationUI.ApplyState(state, allComplete);
        if (ship != null) ship.SetPaused(state == GameState.WAVE_PAUSED); // SHIP: freeze ship while paused
        RenderResultScreens(state);
        StateChanged?.Invoke(state);
    }

    public void PauseWave()
    {
        automaticallyPaused = false;
        states.TryEnter(GameState.WAVE_PAUSED);
    }
    public void ResumeWave()
    {
        if (applicationPaused || focusLost) return;
        automaticallyPaused = false;
        states.Resume();
    }
    // S4 -> S5. Player pressed Continue on the Wave Complete screen.
    private void ContinueFromWaveComplete()
    {
        if (!ready || State != GameState.WAVE_COMPLETE) return;
        allComplete = waveNumber >= waveModel.Count;
        if (!allComplete) waveNumber++;

        pendingOffer = null; pendingReason = "";
        if (!allComplete &&
            UpgradeRecommender.TryRecommend(buildings, completedSnapshot, waveRewards.EvaluateNeed, out var offer, out var reason))
        {
            pendingOffer = offer; pendingReason = reason;
        }

        services.ClearWave();
        if (!states.TryEnter(GameState.UPGRADE)) return;
        // After the last wave, or when everything is maxed, there is nothing to recommend.
        if (pendingOffer == null) FinishUpgrade();
    }

    // S5 -> next wave. Reached by "Later" and after a successful "Upgrade Now".
    private void FinishUpgrade()
    {
        if (!ready || State != GameState.UPGRADE) return;
        pendingOffer = null; pendingReason = "";
        states.TryEnter(GameState.AWAITING_WAVE);
        RefreshForecast();
    }

    private void UpgradeNow()
    {
        if (!ready || State != GameState.UPGRADE || pendingOffer == null) return;
        var offer = pendingOffer;
        pendingOffer = null; // OnChanged fires inside TryUpgrade; S5 must not re-render with the next level.
        if (buildings.TryUpgrade(offer.Building)) { FinishUpgrade(); return; }
        pendingOffer = offer;
        RefreshUpgradeOffer(); // not affordable (anymore): update the panel instead of closing it
    }

    // Keeps S5 live while it is open (Coins / Materials changed).
    private void RefreshUpgradeOffer()
    {
        if (!ready || State != GameState.UPGRADE || pendingOffer == null) return;
        if (!buildings.TryGetUpgradeOffer(pendingOffer.Building, out var fresh)) return;
        pendingOffer = fresh;
        upgradeUI.Show(fresh, pendingReason);
    }

    private void RenderResultScreens(GameState state)
    {
        if (state == GameState.WAVE_COMPLETE && completedResult != null) waveCompleteUI.Show(completedResult);
        else waveCompleteUI.Hide();

        if (state == GameState.UPGRADE && pendingOffer != null) upgradeUI.Show(pendingOffer, pendingReason);
        else upgradeUI.Hide();
    }

    // Final wave evaluation. Must run before ContinueFromWaveComplete(), which clears the wave.
    private void EvaluateWaveResult()
    {
        completedResult = null; completedSnapshot = null;
        if (waveRewards == null) return;
        var snapshot = services.Snapshot();
        // A wave with no available services has no customers to rate.
        if (snapshot == null || !snapshot.IsComplete || snapshot.Total == 0) return;
        try
        {
            var result = waveRewards.Prepare(snapshot);
            completedResult = result;       // shown on S4
            completedSnapshot = snapshot;   // used by the S5 recommendation
            if (lastEnergyRewardedWave == result.WaveNumber) return;
            lastEnergyRewardedWave = result.WaveNumber;
            buildings.AddEnergy(result.Energy);
        }
        catch (Exception exception) { Debug.LogError("Wave result: " + exception.Message, this); }
    }

    private void FlowAction()
    {
        switch (State)
        {
            case GameState.WAVE_INCOMING:
            case GameState.WAVE_ACTIVE: PauseWave(); break;
            case GameState.WAVE_PAUSED: ResumeWave(); break;
        }
    }
    // Integration point for future Worker/boost tasks; active work is never reset.
    public void SetServiceSpeed(NeedType need, double multiplier) { services?.SetSpeedMultiplier(need, multiplier); }

    private void OnApplicationPause(bool paused) { applicationPaused = paused; CheckApplicationPause(); }
    private void OnApplicationFocus(bool focused) { focusLost = !focused; CheckApplicationPause(); }
    private void CheckApplicationPause()
    {
        if (!ready) return;
        if (applicationPaused || focusLost)
        {
            if (State == GameState.WAVE_INCOMING || State == GameState.WAVE_ACTIVE)
            {
                automaticallyPaused = true;
                states.TryEnter(GameState.WAVE_PAUSED);
            }
        }
        else if (automaticallyPaused)
        {
            automaticallyPaused = false;
            states.Resume();
        }
    }
    private void OnDisable()
    {
        if (ready && (State == GameState.WAVE_ACTIVE || State == GameState.WAVE_INCOMING)) PauseWave();
    }
    private void OnDestroy()
    {
        if (states != null) states.Changed -= ApplyState;
        if (waveUI != null) { waveUI.StartWaveRequested -= StartWave; waveUI.FlowActionRequested -= FlowAction; }
        if (buildings != null) { buildings.OnChanged -= RefreshForecast; buildings.OnChanged -= RefreshUpgradeOffer; }
        if (waveCompleteUI != null) waveCompleteUI.ContinueRequested -= ContinueFromWaveComplete;
        if (upgradeUI != null) { upgradeUI.UpgradeNowRequested -= UpgradeNow; upgradeUI.LaterRequested -= FinishUpgrade; }
    }
}

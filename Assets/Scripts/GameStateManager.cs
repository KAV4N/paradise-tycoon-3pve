using System;
using UnityEngine;

// the sole Unity owner of wave lifecycle, using the original GameState/GameData.
public class GameStateManager : MonoBehaviour
{
    [SerializeField] private GameData game_data;
    [SerializeField] private MvpBuildingSystem buildings;
    [SerializeField] private WaveUIController waveUI;
    [Header("Scene components - drag the task objects here")]
    [SerializeField] private WaveModelComponent waveModel;
    [SerializeField] private WaveStatesComponent states;
    [SerializeField] private WaveServicesComponent services;
    [Header("lifecycle")]
    [SerializeField, Min(0)] private float incomingSeconds = 3;
    private float incomingRemaining;
    private int waveNumber = 1;
    private bool allComplete;
    private bool ready;
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
            states == null || services == null || !services.IsConfigured || waveModel.Count == 0)
        {
            Debug.LogError("Assign GameData, Buildings, Wave UI and all three task components; configure the wave list and service Buildings reference.", this);
            enabled = false;
            return;
        }
        ready = true;
        states.Changed += ApplyState;
        waveUI.StartWaveRequested += StartWave;
        waveUI.FlowActionRequested += FlowAction;
        buildings.OnChanged += RefreshForecast;
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
        waveUI.SetCustomerFeedback(null, null);
        incomingRemaining = Mathf.Max(0, incomingSeconds);
        Publish(true);
        states.TryEnter(GameState.WAVE_INCOMING);
        CheckApplicationPause();
    }

    private void Update()
    {
        if (!ready) return;
        switch (State)
        {
            case GameState.WAVE_INCOMING:
                incomingRemaining -= Time.deltaTime;
                if (incomingRemaining <= 0) states.TryEnter(GameState.WAVE_ACTIVE);
                break;
            case GameState.WAVE_ACTIVE:
                services.Tick(Time.deltaTime);
                Publish(false);
                if (services.IsComplete) states.TryEnter(GameState.WAVE_RESOLVING, true);
                break;
            case GameState.WAVE_RESOLVING:
                states.TryEnter(GameState.WAVE_COMPLETE);
                PrepareNextWave();
                break;
        }
    }

    private void Publish(bool rebuild)
    {
        CurrentSnapshot = services.Snapshot();
        waveUI.RenderWave(CurrentSnapshot, rebuild);
        ProgressChanged?.Invoke(CurrentSnapshot);
    }

    private void RefreshForecast()
    {
        if (!ready || (State != GameState.AWAITING_WAVE && State != GameState.UPGRADE) || allComplete) return;
        try
        {
            waveUI.RenderWave(services.Forecast(waveModel.Get(waveNumber)), true);
        }
        catch (ArgumentException exception) { Debug.LogError("Wave configuration: " + exception.Message, this); }
    }

    private void ApplyState(GameState state)
    {
        game_data.SetState(state);
        bool canUpgrade = state == GameState.AWAITING_WAVE || state == GameState.UPGRADE;
        buildings.SetWaveActive(!canUpgrade);
        waveUI.ApplyGameState(state, waveNumber, allComplete);
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
    private void PrepareNextWave()
    {
        // Completion and upgrade are internal lifecycle steps, not player confirmations.
        if (!states.TryEnter(GameState.UPGRADE)) return;
        allComplete = waveNumber >= waveModel.Count;
        if (!allComplete) waveNumber++;
        services.ClearWave();
        states.TryEnter(GameState.AWAITING_WAVE);
        RefreshForecast();
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
        if (buildings != null) buildings.OnChanged -= RefreshForecast;
    }
}

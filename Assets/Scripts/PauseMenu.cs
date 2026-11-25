using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pause / Esc menu: pause button opens the panel and freezes the game, the panel has a music
// volume slider, a game speed slider and a "Show FPS" toggle. Settings are remembered between sessions.
// Put this script on the OUTER "EscMenu" object (it must stay active; the panel is toggled).
// Spawns no UI: it only uses the references you assign in the Inspector.
[AddComponentMenu("UI/Pause Menu (Esc Menu)")]
public sealed class PauseMenu : MonoBehaviour
{
    private const string MusicKey = "MusicVolume";
    private const string TimeScaleKey = "TimeScale";
    private const string ShowFpsKey = "ShowFps";
    private const int MinTimeScale = 1;
    private const int MaxTimeScale = 30;

    [Header("Open / close")]
    [SerializeField] private Button pauseButton;
    [Tooltip("The panel that is shown while paused (the inner EscMenu / EscPanel object).")]
    [SerializeField] private GameObject menuPanel;
    [Tooltip("Optional Resume / Close button inside the panel.")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private bool closeWithEscape = true;   // Esc on PC, Back button on Android

    [Header("Music")]
    [SerializeField] private Slider musicSlider;
    [Tooltip("Optional, shows e.g. 70%.")]
    [SerializeField] private TMP_Text musicValueText;
    [Tooltip("The AudioSource that plays the music. If empty, the slider controls the master volume.")]
    [SerializeField] private AudioSource music;

    [Header("Game speed")]
    [Tooltip("Optional. Whole numbers from 1 to 30.")]
    [SerializeField] private Slider timeScaleSlider;
    [Tooltip("Optional, shows e.g. x5.")]
    [SerializeField] private TMP_Text timeScaleValueText;

    [Header("FPS")]
    [SerializeField] private Toggle showFpsToggle;
    [Tooltip("Your existing FPS_MAX object. Needed for the FPS text and for the speed / pause (it controls Time.timeScale).")]
    [SerializeField] private FPS_MAX fpsCounter;

    public static bool IsPaused { get; private set; }
    private int resumeTimeScale = 1;

    private void Awake()
    {
        IsPaused = false;
        if (menuPanel == null)
        {
            Debug.LogError("Pause Menu: assign the Menu Panel.", this);
            enabled = false;
            return;
        }

        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.5f));
        int savedScale = Mathf.Clamp(PlayerPrefs.GetInt(TimeScaleKey, 1), MinTimeScale, MaxTimeScale);
        bool showFps = PlayerPrefs.GetInt(ShowFpsKey, 1) == 1;

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.wholeNumbers = false;   // must be false, otherwise the slider only has 0 and 1
            musicSlider.SetValueWithoutNotify(volume);
            musicSlider.onValueChanged.AddListener(OnMusicSlider);
        }
        if (timeScaleSlider != null)
        {
            timeScaleSlider.minValue = MinTimeScale;
            timeScaleSlider.maxValue = MaxTimeScale;
            timeScaleSlider.wholeNumbers = true;
            timeScaleSlider.SetValueWithoutNotify(savedScale);
            timeScaleSlider.onValueChanged.AddListener(OnTimeScaleSlider);
        }
        if (showFpsToggle != null)
        {
            showFpsToggle.SetIsOnWithoutNotify(showFps);
            showFpsToggle.onValueChanged.AddListener(OnShowFpsToggle);
        }
        if (pauseButton != null) pauseButton.onClick.AddListener(Open);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);

        ApplyMusic(volume);
        ApplyTimeScale(savedScale);
        ApplyShowFps(showFps);
        menuPanel.SetActive(false);
    }

    private void Update()
    {
        if (!closeWithEscape || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (IsPaused) Close(); else Open();
    }

    public void Open()
    {
        if (IsPaused) return;
        // FPS_MAX writes Time.timeScale every frame from its time_scale field, so we change that field.
        resumeTimeScale = fpsCounter != null ? fpsCounter.time_scale : Mathf.RoundToInt(Time.timeScale);
        if (resumeTimeScale <= 0) resumeTimeScale = 1;
        SetTimeScale(0);
        IsPaused = true;
        menuPanel.SetActive(true);
    }

    public void Close()
    {
        if (!IsPaused) return;
        SetTimeScale(resumeTimeScale);
        IsPaused = false;
        menuPanel.SetActive(false);
    }

    private void SetTimeScale(int value)
    {
        if (fpsCounter != null) fpsCounter.time_scale = value;
        Time.timeScale = value;
    }

    private void OnMusicSlider(float value)
    {
        ApplyMusic(value);
        PlayerPrefs.SetFloat(MusicKey, value);
    }

    // Slider events always send a float, even for whole numbers.
    private void OnTimeScaleSlider(float value)
    {
        int scale = Mathf.Clamp(Mathf.RoundToInt(value), MinTimeScale, MaxTimeScale);
        ApplyTimeScale(scale);
        PlayerPrefs.SetInt(TimeScaleKey, scale);
    }

    private void OnShowFpsToggle(bool show)
    {
        ApplyShowFps(show);
        PlayerPrefs.SetInt(ShowFpsKey, show ? 1 : 0);
    }

    private void ApplyMusic(float volume)
    {
        if (music != null) music.volume = volume; else AudioListener.volume = volume;
        if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    private void ApplyTimeScale(int scale)
    {
        resumeTimeScale = scale;                // used when the menu closes
        if (!IsPaused) SetTimeScale(scale);     // while paused the game must stay frozen
        if (timeScaleValueText != null) timeScaleValueText.text = "x" + scale;
    }

    private void ApplyShowFps(bool show)
    {
        // Disables only the text component, never the object, so FPS_MAX keeps running.
        if (fpsCounter != null && fpsCounter.fps_text != null) fpsCounter.fps_text.enabled = show;
    }

    private void OnDestroy()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSlider);
        if (timeScaleSlider != null) timeScaleSlider.onValueChanged.RemoveListener(OnTimeScaleSlider);
        if (showFpsToggle != null) showFpsToggle.onValueChanged.RemoveListener(OnShowFpsToggle);
        if (pauseButton != null) pauseButton.onClick.RemoveListener(Open);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);
        PlayerPrefs.Save();
        if (IsPaused) { IsPaused = false; SetTimeScale(resumeTimeScale); } // never leave the game frozen
    }
}
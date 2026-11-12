using TMPro;
using UnityEngine;


public class FPS_MAX : MonoBehaviour
{
    public bool vSync = true;
    public int FPS = 30;
    public TMP_Text fps_text;
    public int time_scale;

    private float deltaTime;


    void Awake()
    {
    
    }


    void Start()
    {
        
    }


    void Update()
    {
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        Application.targetFrameRate = FPS;
        Time.timeScale = time_scale;

        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        fps_text.SetText($"Max FPS: {FPS}\nCurrent FPS: {fps:0}");
    }
}

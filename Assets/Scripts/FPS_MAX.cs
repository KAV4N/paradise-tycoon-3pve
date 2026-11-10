using TMPro;
using UnityEngine;


public class FPS_MAX : MonoBehaviour
{
    public bool vSync = true;
    public int FPS = 30;
    public TMP_Text fps_text;

    private float deltaTime;


    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FPS;
    }


    void Start()
    {
        
    }


    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float fps = 1.0f / deltaTime;

        fps_text.SetText($"Max FPS: {FPS}\nCurrent FPS: {fps:0}");
    }
}

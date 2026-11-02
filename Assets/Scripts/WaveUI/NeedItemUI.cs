using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NeedItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image needIcon;
    [SerializeField] private TextMeshProUGUI needNameText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressBarFill;

    [Header("Warning Elements")]
    [SerializeField] private GameObject warningContainer;
    [SerializeField] private Image warningIcon;
    [SerializeField] private TextMeshProUGUI warningText;

    [Header("Color Settings")]
    [SerializeField] private Color normalBarColor = new Color(0.2f, 0.8f, 0.4f); // Zelená
    [SerializeField] private Color warningBarColor = new Color(1f, 0.3f, 0.3f);  // Èervená / Oranžová

    public NeedType Type { get; private set; }

    public void Setup(NeedType needType, Sprite icon, string displayName)
    {
        Type = needType;
        if (needIcon != null) needIcon.sprite = icon;
        if (needNameText != null) needNameText.text = displayName;

        SetWarning(false, string.Empty);
    }

    public void UpdateProgress(int current, int total, bool isBottleneck = false, string warningMsg = "")
    {
        float ratio = total > 0 ? (float)current / total : 0f;

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = Mathf.Clamp01(ratio);
            progressBarFill.color = isBottleneck ? warningBarColor : normalBarColor;
        }

        if (progressText != null)
        {
            if (isBottleneck)
            {
                progressText.text = $"{current}/{total} BOTTLENECK";
                progressText.color = warningBarColor;
            }
            else
            {
                progressText.text = $"{current}/{total}";
                progressText.color = Color.black;
            }
        }

        SetWarning(isBottleneck, warningMsg);
    }

    private void SetWarning(bool active, string message)
    {
        if (warningContainer != null)
            warningContainer.SetActive(active);

        if (warningText != null)
            warningText.text = message;
    }
}
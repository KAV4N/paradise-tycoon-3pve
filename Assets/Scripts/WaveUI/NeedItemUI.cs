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
    [SerializeField] private Color warningBarColor = new Color(1f, 0.3f, 0.3f);  // Červená / Oranžová
    [SerializeField] private Color normalTextColor = new Color(1f, 0.96f, 0.85f);
    [SerializeField] private Color detailTextColor = new Color(0.76f, 0.9f, 0.85f);
    [SerializeField] private Color recoveringColor = new Color(0.25f, 0.8f, 1f);
    private NeedStatus currentStatus = NeedStatus.OK;
    private NeedSnapshot lastSnapshot;

    public NeedType Type { get; private set; }

    public void Setup(NeedType needType, Sprite icon, string displayName)
    {
        Type = needType;
        currentStatus = NeedStatus.OK;
        lastSnapshot = null;
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
                progressText.color = normalTextColor;
            }
        }

        SetWarning(isBottleneck, warningMsg);
    }

    // snapshot presentation; no service logic lives in the UI.
    public void RenderService(NeedSnapshot need)
    {
        lastSnapshot = need;
        UpdateProgress(need.Completed, need.Total, currentStatus == NeedStatus.BOTTLENECK);
        if (progressBarFill != null)
        {
            progressBarFill.type = Image.Type.Simple;
            var rect = progressBarFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(need.Total > 0 ? (float)need.Completed / need.Total : 0, 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        string status = $"Serving {need.InService} | Queue {need.Waiting}";
        if (need.TracksWaitingTime) status += $" | Wait {need.LongestCurrentWait:F0}s";
        bool alert = currentStatus == NeedStatus.BOTTLENECK || currentStatus == NeedStatus.RECOVERING;
        Color color = currentStatus == NeedStatus.BOTTLENECK ? warningBarColor
            : currentStatus == NeedStatus.RECOVERING ? recoveringColor : normalBarColor;
        // Keep the existing single-line detail area and narrow progress counter.
        if (currentStatus == NeedStatus.BOTTLENECK) status = $"Queue growing | Waiting {need.Waiting}";
        else if (currentStatus == NeedStatus.RECOVERING) status = $"Catching up | Waiting {need.Waiting}";
        else if (currentStatus == NeedStatus.SATISFIED) status = "All guests served";
        SetWarning(true, status);
        if (progressBarFill != null) progressBarFill.color = color;
        if (progressText != null)
        {
            progressText.text = $"{need.Completed}/{need.Total}";
            progressText.color = alert ? color : normalTextColor;
        }
        if (warningText != null) warningText.color = alert ? color : detailTextColor;
        if (warningIcon != null)
        {
            warningIcon.gameObject.SetActive(alert);
            warningIcon.color = color;
        }
    }

    public void SetStatus(NeedStatus status)
    {
        currentStatus = status;
        if (lastSnapshot != null) RenderService(lastSnapshot);
    }

    private void SetWarning(bool active, string message)
    {
        if (warningContainer != null)
            warningContainer.SetActive(active);

        if (warningText != null)
            warningText.text = message;
    }
}

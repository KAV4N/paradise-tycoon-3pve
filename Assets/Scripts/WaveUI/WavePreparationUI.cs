using System;
using TMPro;
using UnityEngine;

// Scene-authored presentation. The existing wave/services model owns the forecast data.
public sealed class WavePreparationUI : MonoBehaviour
{
    [Serializable]
    public struct ForecastNeed
    {
        public NeedType type;
        public GameObject root;
        public TMP_Text label;
    }
    [Header("Home - next wave forecast")]
    [SerializeField] private GameObject forecastRoot;
    [SerializeField] private TMP_Text forecastTitle;
    [SerializeField] private ForecastNeed[] forecastNeeds;
    public bool IsConfigured => forecastRoot != null && forecastTitle != null &&
        forecastNeeds != null && forecastNeeds.Length == 5;

    public void ApplyState(GameState state, bool allComplete)
    {
        if (forecastRoot != null) forecastRoot.SetActive(state == GameState.AWAITING_WAVE && !allComplete);
    }

    public void RenderForecast(WaveSnapshot forecast)
    {
        if (forecast == null || !IsConfigured) return;
        forecastTitle.text = $"NEXT WAVE {forecast.Number}   |   {forecast.Customers} GUESTS";
        foreach (var slot in forecastNeeds)
        {
            int total = 0;
            foreach (var need in forecast.Needs) if (need.Type == slot.type) { total = need.Total; break; }
            if (slot.root != null) slot.root.SetActive(total > 0);
            if (slot.label != null) slot.label.text = $"{slot.type.GetDisplayName()} {total}";
        }
    }

}

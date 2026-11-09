using System;
using UnityEngine;

[AddComponentMenu("Waves/Wave Model")]
public sealed class WaveModelComponent : MonoBehaviour
{
    [Serializable]
    public sealed class WaveEntry
    {
        [Min(0)] public int customers;
        [Min(0)] public int rooms, drinks, food, relax, oceanView;
        public WaveEntry(int customers, int rooms, int drinks, int food, int relax, int oceanView)
        {
            this.customers = customers; this.rooms = rooms; this.drinks = drinks;
            this.food = food; this.relax = relax; this.oceanView = oceanView;
        }
    }

    [SerializeField, Min(0.1f)] private float arrivalSeconds = 90;
    [SerializeField]
    private WaveEntry[] waves = {
        new WaveEntry(12,8,8,6,0,0), new WaveEntry(18,12,16,10,0,0),
        new WaveEntry(24,16,18,14,0,0), new WaveEntry(26,16,18,14,8,0),
        new WaveEntry(34,20,22,18,16,0), new WaveEntry(40,25,28,22,20,0),
        new WaveEntry(42,25,28,22,20,10), new WaveEntry(52,31,35,28,25,16)
    };
    public int Count => waves == null ? 0 : waves.Length;
    public WaveDefinition Get(int number)
    {
        if (number < 1 || number > Count || waves[number - 1] == null)
            throw new ArgumentOutOfRangeException(nameof(number), "Configure this wave in the Wave Model scene component.");
        var wave = waves[number - 1];
        return new WaveDefinition(number, wave.customers, arrivalSeconds,
            wave.rooms, wave.drinks, wave.food, wave.relax, wave.oceanView);
    }
}

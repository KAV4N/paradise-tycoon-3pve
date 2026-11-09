using System;
using UnityEngine;

[AddComponentMenu("Waves/Wave States")]
public sealed class WaveStatesComponent : MonoBehaviour
{
    private readonly WaveStates runtime = new WaveStates();
    public GameState State => runtime.State;
    public event Action<GameState> Changed
    {
        add { runtime.Changed += value; }
        remove { runtime.Changed -= value; }
    }
    public bool TryEnter(GameState next, bool servicesFinished = false) => runtime.TryEnter(next, servicesFinished);
    public bool Resume() => runtime.Resume();
}

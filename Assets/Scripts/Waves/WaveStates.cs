using System;

// legal transitions built on the project's existing GameState enum.
public sealed class WaveStates
{
    public GameState State { get; private set; } = GameState.AWAITING_WAVE;
    private GameState pausedFrom;
    public event Action<GameState> Changed;
    public bool TryEnter(GameState next, bool servicesFinished = false)
    {
        bool allowed =
            (State == GameState.AWAITING_WAVE && next == GameState.WAVE_INCOMING) ||
            (State == GameState.WAVE_INCOMING && next == GameState.WAVE_ACTIVE) ||
            ((State == GameState.WAVE_INCOMING || State == GameState.WAVE_ACTIVE) && next == GameState.WAVE_PAUSED) ||
            (State == GameState.WAVE_PAUSED && next == pausedFrom) ||
            (State == GameState.WAVE_ACTIVE && next == GameState.WAVE_RESOLVING && servicesFinished) ||
            (State == GameState.WAVE_RESOLVING && next == GameState.WAVE_COMPLETE) ||
            (State == GameState.WAVE_COMPLETE && next == GameState.UPGRADE) ||
            (State == GameState.UPGRADE && next == GameState.AWAITING_WAVE);
        if (!allowed) return false;
        if (next == GameState.WAVE_PAUSED) pausedFrom = State;
        State = next;
        Changed?.Invoke(State);
        return true;
    }
    public bool Resume() => State == GameState.WAVE_PAUSED && TryEnter(pausedFrom);
}

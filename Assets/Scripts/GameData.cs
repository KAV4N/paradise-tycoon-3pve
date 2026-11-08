using UnityEngine;


public enum GameState
{
    AWAITING_WAVE,
    WAVE_INCOMING,
    WAVE_ACTIVE,
    WAVE_PAUSED,
    WAVE_RESOLVING,
    WAVE_COMPLETE,
    UPGRADE
}


public class GameData : MonoBehaviour
{
    // only GameStateManager writes the state; other systems read it.
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("game_state")]
    private GameState currentState = GameState.AWAITING_WAVE;
    public GameState game_state => currentState;
    internal void SetState(GameState state) { currentState = state; }
}

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
    public GameState game_state = GameState.UPGRADE;
}
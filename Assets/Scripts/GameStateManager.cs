using UnityEngine;


public class GameStateManager : MonoBehaviour
{
    private GameData game_data;


    void Start()
    {
        game_data = GetComponent<GameData>();

        if (game_data == null)
        {
            Debug.LogError("GameData not found!");
            return;
        }
    }


    void Update()
    {
        
    }


    private void setGameState(GameState game_state)
    {
        game_data.game_state = game_state;
    }
}

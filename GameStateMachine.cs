public enum GameState
{
    Boot,
    MainMenu,
    Gameplay,
    Pause
}

public class GameStateMachine
{
    public GameState CurrentState = GameState.Boot;

    public void Update()
    {
        switch (CurrentState)
        {
            case GameState.Boot:
                UpdateBoot();
                break;

            case GameState.MainMenu:
                UpdateMainMenu();
                break;

            case GameState.Gameplay:
                UpdateGameplay();
                break;

            case GameState.Pause:
                UpdatePause();
                break;
        }
    }

    private void UpdateBoot()
    {
    }

    private void UpdateMainMenu()
    {
    }

    private void UpdateGameplay()
    {
    }

    private void UpdatePause()
    {
    }
}
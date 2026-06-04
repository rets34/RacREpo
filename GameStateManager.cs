using System;

public class GameStateManager
{
    private int currentState = 0;

    private Action[] stateTable;

    public GameStateManager()
    {
        stateTable = new Action[]
        {
            State_0_Boot,
            State_1_Init,
            State_2_Menu,
            State_3_Gameplay
            // expand as you map more
        };
    }

    public void Update()
    {
        if (currentState < stateTable.Length)
        {
            stateTable[currentState]?.Invoke();
        }
    }

    // ---- STATES (replace as you identify real ones) ----

    private void State_0_Boot()
    {
        Console.WriteLine("Boot state");
        currentState = 1;
    }

    private void State_1_Init()
    {
        Console.WriteLine("Init state");
        currentState = 2;
    }

    private void State_2_Menu()
    {
        Console.WriteLine("Menu state");
    }

    private void State_3_Gameplay()
    {
        Console.WriteLine("Gameplay state");
    }
}
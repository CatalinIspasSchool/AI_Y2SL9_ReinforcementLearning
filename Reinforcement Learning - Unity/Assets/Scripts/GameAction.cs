using UnityEngine;


/**
 * GameAction stores the components of a single game action taken by the Ship
 */
public class GameAction
{
    // define the possible actions
    public enum Action { None = 0, Left , Right , Fire}

    public Action action;


    GameAction()
    {

    }

    public GameAction(Action action)
    {
        this.action = action;
    }
}

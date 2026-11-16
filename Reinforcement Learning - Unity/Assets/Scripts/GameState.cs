using System;
using UnityEngine;
using static GameController;


/**
 * GameState stores data for a GameState for any ship
 */
public class GameState
{
    private ImpactDirectionCategory direction;
    private ImpactDistanceCategory distance;
    private ImpactSpeedCategory speed;
    private ImpactSizeCategory size;

    // the unique ID based on the combination of the 3 status variables
    public int gameStateID;

    private static string[] dirCatStr = { "Left   ", "Centre", "Right " };
    private static string[] distCatStr = { "Near   ", "Medium", "Far    " };


    /**
     * UpdateState will set the game state based on the provided values, and update the gameStateID value automatically
     */
    public void UpdateState(ImpactDirectionCategory _direction, ImpactDistanceCategory _distance, ImpactSpeedCategory _speed, ImpactSizeCategory _size)
    {
        distance = _distance;
        direction = _direction;
        speed = _speed;
        size = _size;

        SetGameStateID();
    }


    /**
     * SetGameStateID will calculate the unique ID for the current game state based on the 3 status variables
     */
    private void SetGameStateID()
    {
        // for now, limit to 2 dimensions - Distance and Direction
        int v1 = Enum.GetNames(typeof(ImpactDirectionCategory)).Length;
        int v2 = Enum.GetNames(typeof(ImpactDistanceCategory)).Length;

        int s1 = (int)direction;
        int s2 = (int)distance;

        gameStateID = (v2  * s1) +   s2;

    }

    public ImpactDirectionCategory GetAsteroidDirection()
    {
        return direction;
    }


    public static string GetStateDescription(int _stateID)
    {
        int v1 = Enum.GetNames(typeof(ImpactDirectionCategory)).Length;
        int v2 = Enum.GetNames(typeof(ImpactDistanceCategory)).Length;

        int _direction = _stateID / v2;
        int _distance = _stateID % v2;

        string stateDesc = dirCatStr[_direction] +"_" +distCatStr[_distance];

        return stateDesc;
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;



/**
 * GameController will manage the game loop and asteroid waves, as well we call the GA algorithm steps when needed
 * 
 */
public class GameController : MonoBehaviour
{

    // define the game state variables
    public enum ImpactDirectionCategory { Left, Centre, Right };
    public enum ImpactDistanceCategory { Near, Medium, Far };
    public enum ImpactSpeedCategory { Fast, Medium, Slow };
    public enum ImpactSizeCategory { Small, Medium, Large };

    // create string lookups for output/UI
    private string[] dirCatStr = { "Left  ", "Centre", "Right " };
    private string[] distCatStr = { "Near  ", "Medium", "Far  " };
    private string[] speedCatStr = { "Fast  ", "Medium", "Slow " };
    private string[] sizeCatStr = { "Small ", "Medium", "Large " };

    // define the bounds for the ships and asteroids
    public float xBound = 5;
    public float yBound = 7;

    public float xAsteroidBound = 13;
    public float yAsteroidBound = 8;



    // RL Parameters
    private int populationSize = 1;
    private int maxGenerations = 100; 

    // Simulation Parameters
    public float simTimeScale = 5f; 

    // Wave config
    private int asteroidWaveTotal = 25; // number of asteroids per wave
    private int asteroidPerWave = 1; 

    // Wave tracking
    private int asteroidWaveCounter = 0;
    private int asteroidSpawnCounter = 0;

    // Asteroid currentAsteroid;
    List<Asteroid> currentAsteroids = new List<Asteroid>();
    WaveData waveData = new WaveData();

    // Prefabs to be used when spawning ships and astroids
    public GameObject asteroidPrefab;
    public GameObject shipPrefab;
    public GameObject shipSpawnPoint;


    // status tracking
    bool gameRunning = false;


    // UI & Reporting
    float averageCollisionCount;
    float averageAstroidsDestroyed;
    public TMP_Text impactAngleText;
    public TMP_Text impactSpeedText;
    public TMP_Text impactDistanceText;
    public TMP_Text impactSizeText;

    public TMP_Text impactAngleCatText;
    public TMP_Text impactSpeedCatText;
    public TMP_Text impactDistanceCatText;
    public TMP_Text impactSizeCatText;

    public TMP_Text averageDestroyedText;
    public TMP_Text averageCollisionText;
    public TMP_Text currentAsteroidText;
    public TMP_Text currentGenerationText;

    // UI text for the learning matrix
    public TMP_Text[] leaningMatrixUI;


    private int shipDataView = 0;


    // Game State Tracking


    private float impactAngleData;
    private float impactSpeedData;
    private float impactDistanceData;
    private float impactSizeData;

    private ImpactDirectionCategory impactDirectionCategoryData;
    private ImpactDistanceCategory impactDistanceCategoryData;
    private ImpactSpeedCategory impactSpeedCategoryData;
    private ImpactSizeCategory impactSizeCategoryData;


    // AI & Tracking
    private int curGeneration = 0;

    // for RL, only 1 ship is used, but this could be expanded
    private List<Ship> allShips = new List<Ship>();


    private int numStates; // calcluated on startup based on enums
    private Boolean simulationInProgress;


    // frequency of AU actons
    private float actionTimer = 1f;
    private const float actionFrequency = 1f;




    void Start()
    {
        // cacluate the nubmer of states needed based on enums

        // curreently, only direction and distance are used, but size and speed could be added
        int v1 = Enum.GetNames(typeof(ImpactDirectionCategory)).Length;
        int v2 = Enum.GetNames(typeof(ImpactDistanceCategory)).Length;
  
        numStates = v1 * v2;

        // set the time scale
        Time.timeScale = simTimeScale;


    }

    // Update is called once per frame
    void Update()
    {
        if (gameRunning)
        {
            DisplayUI();

            simulationInProgress = !IsLevelComplete();

            if (simulationInProgress)
            {
                HandleInput();

                // only call the ship to make it's action every step 
                actionTimer = actionTimer - Time.deltaTime;
                if (actionTimer<=0)
                {
                    ClearShipFiring();
               
                    SelectShipsAction(); // select the action want to take
                    ApplyShipsAction(); // update the ship's state according to action                  
                    UpdateShipsState(); // move ship to next state - this step will involve recording rewards
                    ProcessShipFiring(); // create bullets as needed

                    // reset timer
                    actionTimer = actionFrequency;
                }

                // move the astroids
                ProcessAsteroids();

                // ship the learning matrix for the first ship

                if(allShips.Count > 0)
                    ShowLearningMatrix(0);

                    
            }
            else
            {
                // checkpoint reached, report and continue
                //  // unlike GA, no strict 'generation' but a simialr setup us used



                // UI and logging
                float rewardTotal = 0;
                float colTotal = 0;
                float destroyTotal = 0;
                for (int i = 0; i < allShips.Count; i++)
                {
                    colTotal += allShips[i].GetCollisions();
                    rewardTotal += allShips[i].GetTotalReward();
                    destroyTotal += allShips[i].GetAsteroidDestroyed();

                    // record learning / rewards to file
                    allShips[i].LogLearningMatrix();
                    allShips[i].LogReward();
                }
             
                averageCollisionCount = colTotal / allShips.Count;
                averageAstroidsDestroyed = destroyTotal / allShips.Count;
                float averageTotalReward = rewardTotal / allShips.Count;    
                Debug.Log(" Generation"+ curGeneration +" Av Collison is " + averageCollisionCount+  " Astroids Destoyed is " + averageAstroidsDestroyed + " avTotalReward is " + averageTotalReward);

              

                curGeneration++;

                // if reached the generation limit, end RL process
                if (curGeneration >= maxGenerations)
                {
                    StopGame();
                    ReportEndResults();
            
                }
                else
                {

                    ResetShipData();
                    ClearShipFiring();

                    asteroidWaveCounter = 0;
                    simulationInProgress = true;
                }
            }

        }

    
    }


    /**
     * StartGame is called when the user preses the 'Start' button, and will begin the GA process
     */
    public void StartGame()
    {

        if (gameRunning)
            return;

        // crete a new wave data

        CreateShips();
        asteroidWaveCounter = 0;

        //start the level
        NewAsteroidWave();   
        gameRunning = true;
        simulationInProgress = true;


    }


    void StopGame()
    {

        gameRunning = false;
        simulationInProgress = false;


    }



    /**
     * ProcessAsteroids will manage the astroied movement, removing when necessary and trigger a new wave when all current astroids completed journey
     */
    void ProcessAsteroids()
    {

        for (int i = currentAsteroids.Count - 1; i >= 0; i--)
        {

            if (currentAsteroids[i].IsDesinationReached())
            {
                Asteroid _a = currentAsteroids[i];
                currentAsteroids.Remove(_a);
                GameObject.Destroy(_a.gameObject);

            }
        }

        if (currentAsteroids.Count == 0)
        {
            NewAsteroidWave();
        }
    }

    /**
     * NewAsteroidWave will create a new asteroids wave
     */
    void NewAsteroidWave()
    {

        asteroidSpawnCounter = 0;

        waveData.CreateWave(asteroidPerWave, xAsteroidBound, yAsteroidBound);

        for (int i=0;i< waveData.spawnPoint.Count; i++) 
            SpawnAsteroid();

        asteroidWaveCounter++;
    }

    void ReportEndResults()
    {
        Debug.Log(" --- END RESULTS --- ");


    }





    /**
     * IsLevelComplete will return true if al the asteroids for the level have been sent, otherise false
     */
    bool IsLevelComplete()
    {
        if (asteroidWaveCounter >= asteroidWaveTotal)
            return true;

        return false;
    }


 

    /**
     * CreateShips will spawn the ships within the Scene 
     */
    void CreateShips()
    {

        for(int i=0;i< populationSize; i++) 
        {
            // create the ship based on the prefab  and set the DNA based on the ga data
            GameObject go = GameObject.Instantiate(shipPrefab, shipSpawnPoint.transform.position, Quaternion.identity);
            Ship newShip = go.GetComponent<Ship>();
            newShip.Init(numStates, xBound, yBound, i);

            newShip.SetGameController(this);

            allShips.Add(newShip);
        }

    }



    /**
     * SpawnAsteroid will create a new astroid based on the WaveData
     */
    void SpawnAsteroid()
    {

        if(asteroidSpawnCounter >= waveData.spawnPoint.Count)
        {
            Debug.Log("WARNING: No spawn data for asteroid " + asteroidSpawnCounter);
            return;
        }

        // use the counter to retrieve the Asteroid initialisation data
        Vector2 _spawn = waveData.spawnPoint[asteroidSpawnCounter];
        Vector2 _dest = waveData.destPoint[asteroidSpawnCounter];
        float _speed = waveData.speed[asteroidSpawnCounter];
        int _size = waveData.size[asteroidSpawnCounter];

        GameObject go = GameObject.Instantiate(asteroidPrefab, new Vector3(_spawn.x, _spawn.y, 0), Quaternion.identity);
        Asteroid a = go.GetComponent<Asteroid>();
        a.Init(new Vector2(_dest.x, _dest.y), _speed, _size);
        a.StartMovement();


        currentAsteroids.Add(a);

        asteroidSpawnCounter++;


    }



    /**
 * ResetShipData should be called at the end of each generation to reset the logging data
 */
    void ResetShipData()
    {

        foreach (Ship ship in allShips)
        {
            ship.ResetGenerationTracking();
        }

    }

    /**
     * SelectShipsAction should be called each step for each ship to select an action based on their RL policy
     */
    void SelectShipsAction()
    {

        foreach (Ship ship in allShips)
        {
            ship.SelectAction();
        }

    }



    /**
     * ApplyShipsAction will apply the selected action to the Ships
     */
    void ApplyShipsAction()
    {

        foreach (Ship ship in allShips)
        {
            ship.ApplyAction(GetClosestAsteroidToPoint(ship.transform.position).transform.position.x);
        }

    }



    void ProcessShipFiring()
    {

        foreach (Ship ship in allShips)
        {
            ship.ApplyFiring();
        }

    }

    void ClearShipFiring()
    {

        foreach (Ship ship in allShips)
        {
            ship.ClearBullets();
        }
    }


    /**
     * UpdateShips will update the game stata for each ship. Should be called once per frame. 
     * 
     */
    void UpdateShipsState()
    {

        if (currentAsteroids.Count == 0)
            return;

        int _shipTracker = 0;

        foreach(Ship ship in allShips)
        {

            // get closest asteroid (as only one per wave will always be current, but could be expanded for multiple per wave)
            Asteroid _a = GetClosestAsteroidToPoint(new Vector2(ship.transform.position.x, ship.transform.position.y));


            // calc x dist from ship to asteroid
            float _directionDiff = ship.transform.position.x - _a.transform.position.x;

            // get speed of asteroid
            float _impactSpeed = _a.GetSpeed();

            // get distance of asteroid
            float _impactDistance = Mathf.Sqrt(((_a.transform.position.x - ship.transform.position.x) * (_a.transform.position.x - ship.transform.position.x)) + ((_a.transform.position.y - ship.transform.position.y) * (_a.transform.position.y - ship.transform.position.y)));

            // convert the real numbers to discrete IDs
            ImpactDistanceCategory _impactDistanceCategory = ConvertDistanceToCategory(_impactDistance);
            ImpactDirectionCategory _impactDirectionCategory = ConvertDirectionToCategory(_directionDiff);
            ImpactSpeedCategory _impactSpeedCategory = ConvertSpeedToCategory(_impactSpeed);
            ImpactSizeCategory _impactSizeCategory = ConvertSizeToCategory(_a.GetAsteroidSize());

            // set current game state for the ship
            ship.MoveToNewGameState(_impactDirectionCategory, _impactDistanceCategory, _impactSpeedCategory, _impactSizeCategory);


            // if this is the ship we are viewing data for, update the trackers for the UI
            if(_shipTracker==shipDataView)
            {
                impactAngleData = _directionDiff;
                impactSpeedData = _impactSpeed;
                impactDistanceData = _impactDistance;

                impactDistanceCategoryData = _impactDistanceCategory;
                impactDirectionCategoryData = _impactDirectionCategory;
                impactSpeedCategoryData = _impactSpeedCategory;
            }

            _shipTracker++;

            
        }



    }


    /**
     * GetClosestAsteroidToPoint will return the closest astroid to the point provided
     */
    Asteroid GetClosestAsteroidToPoint(Vector2 point)
    {


        int closestIndex = 0;
        float closestDist = 10000;

        for (int i = 0; i < currentAsteroids.Count; i++)
        {
            float _dist = Mathf.Sqrt(((currentAsteroids[i].transform.position.x - point.x) * (currentAsteroids[i].transform.position.x - point.x)) + ((currentAsteroids[i].transform.position.y - point.y) * (currentAsteroids[i].transform.position.y - point.y)));

            if (_dist < closestDist)
            {
                closestIndex = i;
                closestDist = _dist;
            }

        }

        return currentAsteroids[closestIndex];

    }




    void HandleInput()
    {


    }


    void DisplayUI()
    {
        impactAngleText.text = "Impact Angle: " + impactAngleData;
        impactSpeedText.text = "Impact Speed: " + impactSpeedData;
        impactDistanceText.text = "Impact Distance: " + impactDistanceData;
        impactSizeText.text = "Impact Size: " + impactSizeData;

        impactAngleCatText.text = "Angle Category: " + GetDirectionCategoryString();
        impactSpeedCatText.text = "Speed Category: " + GetSpeedCategoryString();
        impactDistanceCatText.text = "Distance Category: " + GetDistanceCategoryString();
        impactSizeCatText.text = "Size Category: " + GetSizeCategoryString();

        averageDestroyedText.text = "Destroyed: " + averageAstroidsDestroyed;
        averageCollisionText.text = "Collision: " + averageCollisionCount;
        currentAsteroidText.text = "Current Wave: " + asteroidWaveCounter + "/" + asteroidWaveTotal;
        currentGenerationText.text = "Current Generation: " + curGeneration;

    }






    /**
     * Conversion functions - will map real numbers to discrete IDs
     */

    ImpactSpeedCategory ConvertSpeedToCategory(float speed)
    {
        if (speed == 4)
            return ImpactSpeedCategory.Fast;
        else if (speed == 3)
            return ImpactSpeedCategory.Medium;
        else
            return ImpactSpeedCategory.Slow;

    }


    ImpactSizeCategory ConvertSizeToCategory(int size)
    {
        if (size == 2)
            return ImpactSizeCategory.Large;
        else if (size == 1)
            return ImpactSizeCategory.Medium;
        else
            return ImpactSizeCategory.Small;

    }


    ImpactDistanceCategory ConvertDistanceToCategory(float distance)
    {
        if (distance > 9)
            return ImpactDistanceCategory.Far;
        else if (distance > 6)
            return ImpactDistanceCategory.Medium;
        else
            return ImpactDistanceCategory.Near;

    }

    ImpactDirectionCategory ConvertDirectionToCategory(float direction)
    {

        // horizonal distance only
        // 2 is arbritary, and could be adjusted
        if (direction > 2)
            return ImpactDirectionCategory.Left;
        else if (direction < -2)
            return ImpactDirectionCategory.Right;
        else
            return ImpactDirectionCategory.Centre;



    }


    /**
     * ShipDataChange will be called when the HUD data input value for the ship data view changes
     */

    public void ShipDataChange(string value)
    {


        int _shipData;


        int.TryParse(value, out _shipData);

        if (_shipData < 0 || _shipData >= allShips.Count)
        {
            Debug.Log("Warning: Cannot view data for ship " + value);

        }
        else
        {
            shipDataView = _shipData;
        }
    }


    /**
     *  UI Text functions
     */

    string GetDirectionCategoryString()
    {


        int index = (int)impactDirectionCategoryData;

        if (index >= 0 && index < dirCatStr.Length)
            return dirCatStr[index];
        else
            return "Invalid Direction Category " + index;

    }


    string GetDistanceCategoryString()
    {


        int index = (int)impactDistanceCategoryData;

        if (index >= 0 && index < distCatStr.Length)
            return distCatStr[index];
        else
            return "Invalid Distance Category " + index;

    }


    string GetSpeedCategoryString()
    {


        int index = (int)impactSpeedCategoryData;

        if (index >= 0 && index < speedCatStr.Length)
            return speedCatStr[index];
        else
            return "Invalid Speed Category " + index;

    }

    string GetSizeCategoryString()
    {


        int index = (int)impactSizeCategoryData;

        if (index >= 0 && index < sizeCatStr.Length)
            return sizeCatStr[index];
        else
            return "Invalid Size Category " + index;

    }


    /**
     * 
     * ShowLearningMatrix will update the learning matrix on the UI
     * If the number of states changes, leaningMatrixUI will need to be updated to match the number of states 
     */



    public void ShowLearningMatrix(int shipIndexID)
    {
        string outputPath = Application.dataPath;

        float[][] _lm = allShips[shipIndexID].GetLearningMatrix();

        if(leaningMatrixUI.Length-1 != _lm.Length)
        {
            Debug.Log("Cannot output leanring matrix, dimension mismatch " + leaningMatrixUI.Length +"  / " + _lm.Length) ;
            return;
        }

        // simple col formatting
        leaningMatrixUI[0].text = "State\t\t\t\tNone\tLeft\tRight\t Fire";


        for (int i = 0; i < _lm.Length; i++)
        {
            string line = GameState.GetStateDescription(i) + "\t\t";
            for (int j = 0; j < _lm[i].Length; j++)
            {
                line += _lm[i][j].ToString("#.00") + "\t";
            }
            leaningMatrixUI[i + 1].text = line;
        }

        
    }

}


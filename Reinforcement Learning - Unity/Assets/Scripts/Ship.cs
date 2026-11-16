using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using static GameController;



/**
 * 
 * Ship represents the AI's ship in the level
 */
public class Ship : MonoBehaviour
{


    Rigidbody2D rb2D;
    private const int shipRireRange = 5;


    // the bullet object created when the ship fires
    public GameObject bulletPrefab;


    // movement slection
    Vector2 steerVec = new Vector2(0, 0);
    GameAction.Action action;

    private bool isFiring = false;

    // current speed multiplier
    private float speed =1;

    // current position on the grid
    private Vector2Int gridPos;

    // tracking data
    private int collisions;
    private float stepReward = 0;
    private int stepCount = 0;
    private float totalReward = 0;
    private int asteroidDestroyed = 0;

    private int numStates;
    private int numActions;

    // visual options
    public Sprite stopSprite;
    public Sprite slowSprite;
    public Sprite fastSprite;


    private int actionSize;

    // the map boundries the ship cannot move beyond - symetrical, so valid range is from -xBount to +xBound etc

    private float xBound = 9;
    private float yBound = 5;    // unused by default - can be used if movement is enabled

    // store the current data for this ship - each ship will be different as data releative to closest Asteroid
    private GameState currentState = new GameState();

    private List<Bullet> allBullets = new List<Bullet>();

    GameController gameController;

    private int shipIndexID;

    int lastGameStateID = 0;



    // Action Tracking
    private List<GameAction> possibleActions = new List<GameAction>();
    private GameAction selectedAction;
    private int selectedActionIndex;

    // ammo system - cam be disabled by ammoLimit
    private bool ammoLimit = true;
    private float ammoCount =  100;


    // RL learning matrix [state][action]
    private float[][] learningMatrix;


    // Q Learning
    float learningRate = 0.5f;
    float discountFactor = 0.5f;

    private int shipGeneration = 0;


    private void Awake()
    {


        // calcuate the number of possible action based on the enum
        GameAction.Action[] s = (GameAction.Action[])Enum.GetValues(typeof(GameAction.Action));

        actionSize = s.Length;
        numActions = actionSize;


        // add all possible actions to the list possibleActions
 
        for (int k = 0; k < actionSize; k++)
        {
            possibleActions.Add(new GameAction(s[k]));
        }

    }


    void Start()
    {
        action = GameAction.Action.None;
        rb2D = gameObject.GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {


    }


    public float[][] GetLearningMatrix()
    {
        return learningMatrix;
    }



    public void SetGameController(GameController _gc)
    {
        gameController = _gc;
    }


    /**
     * Init should be called to initliase a ship at the start of any level / simulation phase
     */
    public void Init(int _stateSize, float _xBound, float _yBound, int _indexID)
    {
        collisions = 0;

        numStates = _stateSize;
        xBound = _xBound;
        yBound = _yBound;

        shipIndexID = _indexID;

        shipGeneration = 0;

        isFiring = false;

        InitLearningMatrix();
    }

    void InitLearningMatrix()
    {
        //create the learning matrix - [State][Action]
        learningMatrix = new float[numStates][];
        for (int i = 0; i < numStates; i++)
        {
            learningMatrix[i] = new float[numActions];
        }

    }

    public int GetAsteroidDestroyed()
    {
        return asteroidDestroyed;
    }

    public int GetCollisions()
    {
        return collisions;
    }


    public bool GetFire()
    {
        return isFiring;
    }


    public Vector2Int GetGridPos()
    {
        return gridPos;
    }


    /**
     * 
    void ApplyFiring will create the laser beam particles in space if the ships laser is activated
     * 
     */

    public void ApplyFiring()
    {


        if (GetFire() == true)
        {

            Vector2 missilePos = new Vector2(this.transform.position.x, this.transform.position.y);

            Vector2Int _fireVec = GetVecForFire();

            for (int i = 0; i < shipRireRange; i++)
            {
                missilePos.x += _fireVec.x;
                missilePos.y += _fireVec.y;

                // spawn bullet in space
                GameObject _b = GameObject.Instantiate(bulletPrefab, new Vector3(missilePos.x, missilePos.y, 0), Quaternion.identity);

                Bullet _bullet = _b.GetComponent<Bullet>();
                _bullet.Init(this);

                _bullet.SetTargetHit(false);

                allBullets.Add(_bullet);

            }


        }

    }



    public void ClearBullets()
    {
        for (int i = allBullets.Count - 1; i >= 0; i--)
        {
            GameObject.Destroy(allBullets[i].gameObject);
        }

        allBullets.Clear();
    }

    /**
     * GetVecForFire returns a direciton vector based on ship alignment
     */

    Vector2Int GetVecForFire()
    {
        // static alignment (N) - set vector accordingly. 

        // this could be adapted for fire in multiple alignments
        Vector2Int _fireVec = new Vector2Int(0, 1);
        return _fireVec;

    }



    /**
     * SetGameState will update and store a new game state based on the provided values
     */
    public void MoveToNewGameState(ImpactDirectionCategory _direction, ImpactDistanceCategory _distance, ImpactSpeedCategory _speed, ImpactSizeCategory _size)
    {

        // log the last state 
        lastGameStateID = currentState.gameStateID;

        // update the game state to the next
        currentState.UpdateState(_direction, _distance, _speed, _size);

        RecordReward(lastGameStateID, currentState.gameStateID);
        ResetStepReward();

    }


    /**
     * ApplyAction will set the Steer and Fire values based on the currently selected Action
     */
    public void ApplyAction(float _asteroidX)
    {

        if (selectedAction == null)
            return;

        ApplyMovement(selectedAction.action, _asteroidX);
        SetFire(selectedAction.action);

    }


    /**
     * IncStepReward will increment the step reward by the provided value
     */
    public void IncStepReward(float _inc)
    {
        stepReward += _inc;
    }

    public Vector2 GetVelocity()
    {
        return steerVec * speed;
    }



    public float GetTotalReward()
    {
        return totalReward;
    }

    /**
     * ResetStepReward will set the step reward to 0 and increment the step counter for tracking
     */
    public void ResetStepReward()
    {
        stepCount++;
        totalReward += stepReward;

        stepReward = 0;
    }


    public void ResetGenerationTracking()
    {
        totalReward = 0;
        collisions = 0;
        asteroidDestroyed = 0;
        shipGeneration++;

    }









    /*
     * ApplyMovement will be called each update and should result in the ship being moved according to the provided '_action'
     */
    public void ApplyMovement(GameAction.Action _action, float _asteroidX)
    {

        //TASK TODO - add code to move the ship left and right based on the _action parameter
        // the postition of the ship can be achieved by changing the 'this.transform.position' of this Ship object
        // transform.position is a Vector3 (x, y, z)
        // the amount the ship should move by is given by 'speed'
        // transform.position components (e.g. x) cannot be set directly: the whole vector should be modified
        // xBound gives the ship's boundry to the right, -xBound gives the ships boundry to the left


        /*
         PSEUDOCODE

        IF action == Action Left
            decrease ship position x by 1
        ELSE IF action == Action Right
            incerase ship position x by 1
        ELSE
            return
        
        IF ship position x is less than left boundry
            ship positon x = left boundry
        ELSE IF ship position x is greater than right boundry
            ship positon x = right boundry
         
         */
        if (_action == GameAction.Action.Left)
        {
            steerVec = new Vector2(-1,0);
            if (_asteroidX <= transform.position.x)
            {
                IncStepReward(1);
            }
            else
            {
                IncStepReward(-1);
            }
        }
        else if (_action == GameAction.Action.Right)
        {
            steerVec = new Vector2(1, 0);
			if (_asteroidX >= transform.position.x)
			{
				IncStepReward(1);
			}
			else
			{
				IncStepReward(-1);
			}
		}
        else
        {
            steerVec = new Vector2(0, 0);
            return;
        }

		rb2D.MovePosition(rb2D.position + steerVec);

		if (transform.position.x > xBound)
		{
			rb2D.MovePosition(new Vector2(xBound,transform.position.y));
			return;
        }
        else if (transform.position.x < -xBound)
		{
			rb2D.MovePosition(new Vector2(-xBound, transform.position.y));
			return;
        }


	}




    /**
     * RecordReward is called each step to log the reward value in the learning matrix
     */

    public void RecordReward(int _stateIndex, int _nextStateIndex)
    {



        //TASK TODO (Step 4) - Add code to update learningMatrix with the step reward provided (stepReward)
        //   Dimensions are learningMatrix[State][Action]
        //   _stateIndex is the current state

        //   class variable selectedActionIndex is the index of the selected action
        learningMatrix[_stateIndex][selectedActionIndex] = stepReward;





        //TASK TODO (Step 9) - Replace the code from Step 4 to update learningMatrix using Q learning
        //   _nextStateIndex is St+1  (next state)
        //  learningRate and discountFactor are class variables


        
    }


    /**
     * SelectAction will select an Action based on the current Game State
     * 
     */
    public void SelectAction()
    {

        // TASK TODO -	Replace the current code below to select the action with the highest Reward
        //  Rewards are stored in learningMatrix[][]
        //      Dimensions are learningMatrix[State][Action]
        //  gameStateIndex will contain the State index


        //  At the end of the function 2 variables should be set
        //      selectedAction – the selected GameAction from the possibleActions list
        //  	selectedActionIndex - the index of the selectedAction
        //
        //  Maintain a chance the AI will select a random action to balance 'exploration' and 'exploitation'
        //      UnityEngine.Random.Range will return a random value
        //  	
        if (UnityEngine.Random.Range(0,100) <= 15)
        {
			int randomIndex = UnityEngine.Random.Range(0, possibleActions.Count);
			selectedAction = possibleActions[randomIndex];

			selectedActionIndex = randomIndex;
		}
        else
        {
            float maxReward = -9999;
            for (int i = 0; i < possibleActions.Count; i++) 
            {
                if (maxReward < learningMatrix[currentState.gameStateID][i])
                {
					maxReward = learningMatrix[currentState.gameStateID][i];
                    selectedAction = possibleActions[i];
                    selectedActionIndex = i;
				}
			}
            if (maxReward == -9999)
            {
				int randomIndex = UnityEngine.Random.Range(0, possibleActions.Count);
				selectedAction = possibleActions[randomIndex];

				selectedActionIndex = randomIndex;
			}
        }

   



       

    }


    /**
     * SetFire will update the Ship's laser beam based on the provided value
     */

    void SetFire(GameAction.Action _fire)
    {

        if (ammoLimit && ammoCount <= 0)
            isFiring = false;
        else
        {

            if (_fire == GameAction.Action.Fire)
            {
                isFiring = true;
                ammoCount--;
        
            }
            else
            {
                isFiring = false;
            }


        }

    }


    /**
     * AsteroidColliion will be called when the ship collides with an asteroid
     */

    public void AsteroidColliion()
    {
        collisions++;

    }


    /**
     * TargetHit will be called when the ship destroyed an astroid with the laser
     */
    public void TargetHit()
    {
        asteroidDestroyed++;

    }




    /**
     * 
     * LogLearningMatrix will save to file the latest learning matrix for this ship
     */
    public void LogLearningMatrix()
    {

        string fileName = "LearningMatrix_" + shipIndexID + ".csv";

        using (StreamWriter outputFile = new StreamWriter(fileName))
        {
            string header = "State, None, Left, Right, Fire";
            outputFile.WriteLine(header);
            for (int i = 0; i < learningMatrix.Length; i++)
            {
                string line = GameState.GetStateDescription(i) + ",";
                for (int j = 0; j < learningMatrix[i].Length; j++)
                {
                    line += learningMatrix[i][j].ToString() + ",";
                }
                outputFile.WriteLine(line);
            }
            
        }
    }

    /**
     * LogReward should log the reward obtained by this ship's configuration to file
     * 
     */
    public void LogReward()
    {


        // TASK TODO -
        // update the below code to log this ships current total Reward to file
        //   totalReward is a class variable
        //   learningRate and discountFactor are class variables
        // log the shipGeneration or time for the time dimension  

        // create a filename 
        string fileName = "EditMe.csv";

        string timestamp = DateTime.Now.ToString();


        // either create or append depend on if the file exists
        if (!File.Exists(fileName))
        {
    
            using (StreamWriter sw = File.CreateText(fileName))
            {
             
                
            }
        }
        else
        {
            using (StreamWriter sw = File.AppendText(fileName))
            {
               
            }
        }
  

  
    }

}


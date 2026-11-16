using UnityEngine;



/**
 * Asteroid represents the asteroids that are spawned in the level that the Ship must avoid
 */
public class Asteroid : MonoBehaviour
{

    // movement and tracking
    private float moveSpeed = 2;     
    private Vector2 destination;
    private Vector2 startPoint;
    private float movePoint;
    private bool isMoving = false;
    private float moveTime;
    private bool destinationReached = false;
    private Vector3 velocity;
    private int asteroidSize = 2;

    private bool collisionActive = false;

    public Sprite[] sprites;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isMoving)
        {
            ProgressMovement();
        }
    }


    /**
     * Init should be called before StartMovement() to set the origin and destination
     */
    public void Init(Vector2 _destination, float _speed, int _size)
    {
        moveSpeed = _speed;
        destination = _destination;

        asteroidSize = _size;

        SetSpriteForSize();
    }

    public bool IsDesinationReached()
    {
        return destinationReached;  
    }

    /**
     * StartMovement will init the movement data and trigger the movement to being
     *  - Desintation and SPeed should be set by calling Init() before calling this function
     */
    public void StartMovement()
    {

        // update the tracking data for the positon updates
        //  - Note: this is controled by postion adjustments, not physics
        startPoint = this.transform.position;
        float moveDist = Mathf.Sqrt(((startPoint.x - destination.x) * (startPoint.x - destination.x)) + ((startPoint.y - destination.y) * (startPoint.y - destination.y)));
        moveTime = moveDist / moveSpeed;
        movePoint = 0;
        isMoving = true;

        velocity = destination - startPoint;
        velocity.Normalize();

    }

    public float GetSpeed()
    {
        return moveSpeed;   
    }

    public int GetAsteroidSize()
    {
        return asteroidSize;
    }


    public Vector3 GetVelocity()
    {
        return velocity* moveSpeed;
    }

    public Vector2Int GetGridPos()
    {
        return new Vector2Int((int)transform.position.x, (int)transform.position.y);
    }


    private void SetSpriteForSize()
    {
        if (asteroidSize < 0 || asteroidSize >= sprites.Length)
        {
            return;

        }

        GetComponent<SpriteRenderer>().sprite = sprites[asteroidSize];
    }

    /**
     * ProgressMovement will update the position of the astroid in the world space , and hsould be called each update() tick
     * 
     */
    void ProgressMovement()
    {
        Vector3 newPos = Vector3.zero;

        newPos.x = Mathf.Lerp(startPoint.x, destination.x, movePoint);
        newPos.y = Mathf.Lerp(startPoint.y, destination.y, movePoint);

        movePoint += Time.deltaTime / moveTime;

        this.transform.position = newPos;

        if(movePoint>=1)         // reached desination, stop movement and set flag for astroid to be destroyed
        {
            isMoving = false;
            destinationReached = true;
        }
    }


    /**
     * OnTriggerEnter2D will be called when the astroid collides with anothter object (ship, bullet)
     */
    void OnTriggerEnter2D(Collider2D col)
    {
 
        if (col.gameObject.CompareTag("Bullet"))
        {
            // ship has collided with bullet - update the bullet to register hit
            col.gameObject.GetComponent<Bullet>().SetTargetHit(true);

            // cause asetroid to be cleared
            destinationReached = true;
        }else if(col.gameObject.CompareTag("Ship"))
        {
            // only want to log one collision per asteroid
            if(!collisionActive)
            {
 
                collisionActive = true;
                // register collision with the Ship
                col.gameObject.GetComponent<Ship>().AsteroidColliion();
            }
       
        }
    }


    void OnTriggerExit(Collider col)
    {
        if (col.gameObject.CompareTag("Ship"))
        {
            collisionActive = false;
        }
    }




}

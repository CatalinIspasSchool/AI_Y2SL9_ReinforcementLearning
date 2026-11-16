using UnityEngine;


/**
 * Bullet represnts the laser fired by the Ship that can destroy Asteroids
 */
public class Bullet : MonoBehaviour
{

    // allow sprites to be set in editor
    public Sprite bullet;
    public Sprite explosion;

    private SpriteRenderer spriteRanderer;


    public bool TargetHit;

    // reference to ship that fired the bullet
    private Ship originShip;


    void Awake()
    {
        spriteRanderer = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void Init(Ship _originShip)
    {
        originShip = _originShip;

    }

    /**
     * SetTargetHit should be called when the bullet hits an Asteroid
     */
    public void SetTargetHit(bool _hit)
    {
        TargetHit = _hit;

        if(spriteRanderer!=null)
        {
            // update the sprite and register with Ship
            if (TargetHit)
            {

                spriteRanderer.sprite = explosion;
                originShip.TargetHit();
            }
            else
                spriteRanderer.sprite = bullet;
        }
          
               
    }
}

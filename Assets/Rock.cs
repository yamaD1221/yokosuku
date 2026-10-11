using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Rock : MonoBehaviour
{
    [SerializeField] private Gate gate;                 // ‹ó‚Å‚àOK
    [SerializeField] private int damage = 99;
    [SerializeField] private float minImpactSpeed = 3f;
    [SerializeField] private float fallGravity = 3f;

    private Rigidbody2D rb;
    private bool dropped;
    private bool landed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;        // ’Ý‚ç‚ê‚Ä‚¢‚éŠÔ‚Í“®‚©‚È‚¢
    }

    public void Drop()
    {
        if (dropped) return;
        dropped = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = fallGravity;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!dropped) return;

        var enemy = c.collider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (c.relativeVelocity.magnitude >= minImpactSpeed)
            {
                int dir = enemy.transform.position.x >= transform.position.x ? 1 : -1;
                enemy.TakeDamage(damage, dir);
            }
            return;
        }

        if (!landed)                                    // “GˆÈŠO‚É’…’n‚µ‚½‚ç–å‚ðŠJ‚­
        {
            landed = true;
            if (gate != null) gate.Open();
        }
    }
}
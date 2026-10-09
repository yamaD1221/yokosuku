using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Knife : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float maxFlyTime = 0.6f;
    [SerializeField] private float embedDepth = 0.25f;   // 壁に食い込ませる深さ
    [SerializeField] private LayerMask stickLayer;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private int damage = 1;

    private Rigidbody2D rb;
    private Collider2D col;
    private bool stuck;
    private int dir = 1;
    private float deadline;
    private float halfLength;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        var box = GetComponent<BoxCollider2D>();
        col = box;
        halfLength = box.size.x * Mathf.Abs(transform.localScale.x) * 0.5f;
    }

    public void Launch(int direction, Vector2 castOrigin)
    {
        dir = direction;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;

        // プレイヤー側から、ナイフの先端が届く範囲に壁がないか先に調べる
        float reach = Vector2.Distance(castOrigin, rb.position) + halfLength * 2f;
        RaycastHit2D hit = Physics2D.Raycast(castOrigin, Vector2.right * dir, reach, stickLayer);
        if (hit.collider != null)
        {
            EmbedAt(hit.point);      // 密着なら、飛ばさずその場で刺す
            return;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * speed, 0f);
        deadline = Time.time + maxFlyTime;
    }

    void EmbedAt(Vector2 surface)
    {
        // 先端が「壁の表面 + embedDepth」になる位置にそろえる
        float tipX = surface.x + dir * embedDepth;
        Vector2 pos = new Vector2(tipX - dir * halfLength, rb.position.y);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        rb.position = pos;
        Stick();
    }

    void Update()
    {
        if (!stuck && Time.time > deadline) Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (stuck) return;

        // ナイフの先端から、次の1ステップで進む距離だけ前を調べる
        float dist = speed * Time.fixedDeltaTime;
        Vector2 tip = rb.position + Vector2.right * dir * halfLength;
        RaycastHit2D hit = Physics2D.Raycast(tip, Vector2.right * dir, dist, stickLayer);

        if (hit.collider != null)
        {
            if (hit.collider != null)
            {
                EmbedAt(hit.point);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (stuck) return;
        int bit = 1 << other.gameObject.layer;

        if ((enemyLayer.value & bit) != 0)
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(damage, dir);
            Destroy(gameObject);
        }
    }

    void Stick()
    {
        stuck = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        col.isTrigger = false;
        col.usedByEffector = true;
    }
}
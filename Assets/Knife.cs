using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Knife : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float maxFlyTime = 0.6f;
    [SerializeField] private float embedDepth = 0.25f;   // 壁に食い込ませる深さ
    [SerializeField] private LayerMask stickLayer;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask ropeLayer;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private float stuckLifetime = 5f;     // 刺さってから消えるまでの秒数
    [SerializeField] private float blinkDuration = 1f;     // 消える直前に、点滅する秒数

    private Rigidbody2D rb;
    private Collider2D col;
    private bool stuck;
    private float stuckAt;
    private SpriteRenderer sr;
    private bool finished;      // 敵やロープに当たって役目を終えた
    private int dir = 1;
    private float deadline;
    private float halfLength;
    private int hitMask;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        var box = GetComponent<BoxCollider2D>();
        col = box;
        halfLength = box.size.x * Mathf.Abs(transform.localScale.x) * 0.5f;
        hitMask = stickLayer.value | enemyLayer.value | ropeLayer.value | blockLayer.value;
    }

    public void Launch(int direction, Vector2 castOrigin)
    {
        dir = direction;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;

        // 密着して投げたとき用:プレイヤー側から先端までを先に調べる
        float reach = Vector2.Distance(castOrigin, rb.position) + halfLength;
        RaycastHit2D hit = Physics2D.Raycast(castOrigin, Vector2.right * dir, reach, hitMask);
        if (hit.collider != null)
        {
            Resolve(hit);
            return;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * speed, 0f);
        deadline = Time.time + maxFlyTime;
    }

    void Update()
    {
        if (!stuck && !finished && Time.time > deadline)
        {
            Destroy(gameObject);
            return;
        }

        if (!stuck) return;

        // 刺さってからの時間で、消える
        float remaining = stuckLifetime - (Time.time - stuckAt);
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 消える直前は、点滅して知らせる(この間も、足場として使える)
        if (sr != null && remaining <= blinkDuration)
            sr.enabled = ((int)(Time.time * 12f)) % 2 == 0;
    }

    void FixedUpdate()
    {
        if (stuck || finished) return;

        // 先端から、次の1ステップで進む距離を調べる(壁・敵・ロープをまとめて)
        float dist = speed * Time.fixedDeltaTime;
        Vector2 tip = rb.position + Vector2.right * dir * halfLength;
        RaycastHit2D hit = Physics2D.Raycast(tip, Vector2.right * dir, dist, hitMask);
        if (hit.collider != null) Resolve(hit);
    }

    // 当たったものに応じて処理を分ける
    void Resolve(RaycastHit2D hit)
    {
        int bit = 1 << hit.collider.gameObject.layer;

        if ((ropeLayer.value & bit) != 0)
        {
            finished = true;
            var rope = hit.collider.GetComponentInParent<Rope>();
            if (rope != null) rope.Cut();
            Destroy(gameObject);
        }
        else if ((enemyLayer.value & bit) != 0)
        {
            finished = true;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, dir);
            }
            else
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();     // ボスなど
                if (target != null) target.TakeDamage(damage, dir);
            }
            Destroy(gameObject);
        }
        else if ((blockLayer.value & bit) != 0)
        {
            // 刺さらない壁:跳ね返って落ちて消える
            finished = true;
            rb.gravityScale = 3f;
            rb.linearVelocity = new Vector2(-dir * 4f, 5f);
            Destroy(gameObject, 0.5f);
        }
        else
        {
            EmbedAt(hit.point);
        }
    }

    void EmbedAt(Vector2 surface)
    {
        float tipX = surface.x + dir * embedDepth;
        Vector2 pos = new Vector2(tipX - dir * halfLength, rb.position.y);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        rb.position = pos;
        Stick();
    }

    void Stick()
    {
        stuck = true;
        stuckAt = Time.time;
        rb.linearVelocity = Vector2.zero;
        col.isTrigger = false;
        col.usedByEffector = true;

        var arena = ArenaRotator.Instance;
        if (arena != null)
        {
            // 箱の壁と一緒に動くよう、箱の子にして、ナイフ自身の剛体は外す
            // (コライダーが、箱の剛体の一部になる)
            transform.SetParent(arena.transform, true);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Static;     // 箱がないシーンでは、これまでどおり
        }
    }
}
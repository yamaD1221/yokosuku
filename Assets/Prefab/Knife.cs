using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
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
    private Vector2 dir = Vector2.right;
    private float deadline;
    private float halfLength;
    private int hitMask;
    private PlatformEffector2D platformEffector;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        var box = GetComponent<BoxCollider2D>();
        col = box;
        halfLength = box.size.x * Mathf.Abs(transform.localScale.x) * 0.5f;
        hitMask = stickLayer.value | enemyLayer.value | ropeLayer.value | blockLayer.value;
        platformEffector = GetComponent<PlatformEffector2D>();
    }

    public void Launch(Vector2 direction, Vector2 castOrigin)
    {
        dir = direction.normalized;

        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.right;

        // ナイフの向きを飛ぶ方向に合わせる
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 密着して投げたとき用:プレイヤー側から先端までを先に調べる
        float reach = Vector2.Distance(castOrigin, rb.position) + halfLength;

        RaycastHit2D hit = Physics2D.Raycast(castOrigin, dir, reach, hitMask);

        if (hit.collider != null)
        {
            Resolve(hit);
            return;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = dir * speed;
        deadline = Time.time + maxFlyTime;
    }

    void Update()
    {
        if (!stuck && !finished && Time.time > deadline)
        {
            //Destroy(gameObject);
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

        float dist = speed * Time.fixedDeltaTime;

        Vector2 tip = rb.position + dir * halfLength;

        RaycastHit2D hit = Physics2D.Raycast(
            tip,
            dir,
            dist,
            hitMask
        );

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
                enemy.TakeDamage(damage, dir.x >= 0 ? 1 : -1);
            }
            else
            {
                var target = hit.collider.GetComponentInParent<IDamageable>(); // ボスなど
                if (target != null)
                {
                    target.TakeDamage(damage, dir.x >= 0 ? 1 : -1);
                }
            }
            Destroy(gameObject);
        }
        else if ((blockLayer.value & bit) != 0)
        {
            // 刺さらない壁:跳ね返って落ちて消える
            finished = true;
            rb.gravityScale = 3f;
            rb.linearVelocity = -dir * 4f + Vector2.up * 5f;
            Destroy(gameObject, 0.5f);
        }
        else
        {
            EmbedAt(hit.point);
        }
    }

    void EmbedAt(Vector2 surface)
    {
        Vector2 pos = surface - dir * (halfLength - embedDepth);

        transform.position = new Vector3(
            pos.x,
            pos.y,
            transform.position.z
        );

        rb.position = pos;
        Stick();
    }

    void Stick()
    {
        stuck = true;
        stuckAt = Time.time;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        col.isTrigger = false;
        col.usedByEffector = true;

        // 足場判定の向きを画面上方向に固定
        if (platformEffector != null)
        {
            platformEffector.useOneWay = true;
            platformEffector.rotationalOffset = -transform.eulerAngles.z;
        }

        var arena = ArenaRotator.Instance;

        if (arena != null)
        {
            // 箱の壁と一緒に動くように、箱の子にする
            transform.SetParent(arena.transform, true);

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Static;
        }
    } 
}
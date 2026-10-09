using UnityEngine;

public class Enemy : MonoBehaviour
{
    private enum State { Patrol, Chase, Windup, Recover }

    [Header("Status")]
    [SerializeField] private int hp = 2;
    [SerializeField] private float knockbackX = 6f;
    [SerializeField] private float knockbackY = 4f;
    [SerializeField] private float stunTime = 0.25f;

    [Header("Move")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 2.5f;
    [SerializeField] private float detectRange = 6f;   // この距離で気づく
    [SerializeField] private float loseRange = 9f;     // この距離まで離れると見失う
    [SerializeField] private LayerMask groundLayer;    // 壁・床のレイヤー

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float windupTime = 0.4f;  // 攻撃前の溜め(見切れる時間)
    [SerializeField] private float recoverTime = 0.7f; // 攻撃後の隙
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 attackOffset = new Vector2(0.9f, 0f);
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 0.8f);
    [SerializeField] private LayerMask playerLayer;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;
    private Transform player;

    private State state = State.Patrol;
    private int dir = 1;
    private float stateTimer;
    private float stunUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        dir = transform.localScale.x >= 0 ? 1 : -1;
    }

    void FixedUpdate()
    {
        if (Time.time < stunUntil) return;   // 吹き飛び中は速度をそのままにする

        if (player == null) { Move(0f); return; }

        float dx = player.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);
        bool sameLevel = Mathf.Abs(player.position.y - transform.position.y) < 2f;

        switch (state)
        {
            case State.Patrol:
                if (!CanAdvance()) Face(-dir);
                Move(dir * patrolSpeed);
                if (sameLevel && dist < detectRange) state = State.Chase;
                break;

            case State.Chase:
                Face(dx >= 0 ? 1 : -1);
                if (!sameLevel || dist > loseRange) { state = State.Patrol; break; }
                if (dist <= attackRange) { StartWindup(); break; }
                Move(CanAdvance() ? dir * chaseSpeed : 0f);
                break;

            case State.Windup:
                Move(0f);
                if (Time.time >= stateTimer)
                {
                    DoAttack();
                    SetTint(baseColor);
                    state = State.Recover;
                    stateTimer = Time.time + recoverTime;
                }
                break;

            case State.Recover:
                Move(0f);
                if (Time.time >= stateTimer) state = State.Chase;
                break;
        }
    }

    void Move(float vx) => rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);

    void Face(int d)
    {
        if (d == dir) return;
        dir = d;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;
    }

    void StartWindup()
    {
        state = State.Windup;
        stateTimer = Time.time + windupTime;
        SetTint(new Color(1f, 0.45f, 0.45f));   // 赤くなって攻撃の予告
    }

    void DoAttack()
    {
        Vector2 center = (Vector2)transform.position + new Vector2(attackOffset.x * dir, attackOffset.y);
        var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f, playerLayer);
        foreach (var h in hits)
        {
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null) { ph.TakeDamage(attackDamage, dir); break; }
        }
    }

    // 前に壁がなく、足元に床がある=進める
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        Vector2 wallOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.05f), b.center.y);
        bool wall = Physics2D.Raycast(wallOrigin, Vector2.right * dir, 0.2f, groundLayer);

        Vector2 footOrigin = new Vector2(b.center.x + dir * (b.extents.x + 0.2f), b.min.y + 0.1f);
        bool ground = Physics2D.Raycast(footOrigin, Vector2.down, 0.5f, groundLayer);

        return !wall && ground;
    }

    void SetTint(Color c) { if (sr != null) sr.color = c; }

    public void TakeDamage(int damage, int fromDir)
    {
        hp -= damage;
        if (hp <= 0) { Destroy(gameObject); return; }

        stunUntil = Time.time + stunTime;
        rb.linearVelocity = new Vector2(fromDir * knockbackX, knockbackY);
        SetTint(baseColor);
        state = State.Chase;     // 殴られたら溜めが中断され、追跡に戻る
    }

    void OnDrawGizmosSelected()
    {
        int d = Application.isPlaying ? dir : 1;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((Vector2)transform.position + new Vector2(attackOffset.x * d, attackOffset.y), attackSize);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
using System.Collections;
using UnityEngine;

// α版のボス「フレーム」。攻撃は、突進・遠距離(弾)・回転の3つ。攻撃の前に、前兆(予告)を出す
[RequireComponent(typeof(Collider2D))]
public class FrameBoss : MonoBehaviour, IDamageable
{
    public static FrameBoss Instance { get; private set; }

    private enum Attack { Charge, Shoot, Rotate }

    [Header("体力")]
    [SerializeField] private int maxHp = 20;

    [Header("移動範囲(箱がないシーンでだけ使う)")]
    [SerializeField] private Vector2 xRange = new Vector2(-3f, 3f);

    [Header("行動の間隔(秒)")]
    [SerializeField] private float idleTime = 0.5f;
    [SerializeField] private float recoverTime = 0.3f;

    [Header("行動の選ばれやすさ(重み。順番は決まっていない)")]
    [SerializeField] private float chargeWeight = 4f;
    [SerializeField] private float shootWeight = 4f;
    [SerializeField] private float rotateWeight = 2f;
    [SerializeField] private float rotateCooldown = 3f;       // 回転が終わってから、次の回転を選ばない秒数

    [Header("突進")]
    [SerializeField] private float chargeTelegraph = 0.5f;    // 前兆の長さ
    [SerializeField] private float chargeSpeed = 22f;
    [SerializeField] private int contactDamage = 1;           // 体に触れたときのダメージ

    [Header("遠距離攻撃(弾)")]
    [SerializeField] private float shootTelegraph = 0.4f;     // 前兆の長さ
    [SerializeField] private int shotCount = 3;
    [SerializeField] private float shotInterval = 0.12f;
    [SerializeField] private float bulletSpeed = 14f;
    [SerializeField] private int bulletDamage = 1;
    [SerializeField] private LayerMask blockLayers;           // 弾が止まる壁・足場のレイヤー(StickWall と NoStickWall)

    [Header("回転")]
    [SerializeField] private float rotateTelegraph = 1.2f;    // 前兆の長さ
    [SerializeField] private bool actDuringRotation = true;   // 回転の最中も、ほかの攻撃を続ける
    [SerializeField] private float rotateAfterDelay = 0.3f;   // 上が false のときの、回転のあとの待ち

    [Header("撃破")]
    [SerializeField] private float defeatDelay = 0.8f;

    [SerializeField] private Material lineMaterial;           // 前兆の線の素材(空でもOK)

    private int hp;
    private bool defeated;
    private Vector3 startPos;
    private float minX, maxX;
    private Collider2D col;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;
    private Color brainColor = Color.white;
    private float flashUntil;
    private string label;
    private LineRenderer chargeLine;
    private Transform player;
    private Collider2D playerCol;
    private PlayerHealth playerHealth;
    private Coroutine brain;
    private GUIStyle bigStyle;
    private bool wasRotating;
    private float nextRotateTime;

    void Awake()
    {
        Instance = this;
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        brainColor = baseColor;

        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        startPos = transform.position;
        hp = maxHp;
        chargeLine = MakeLine();
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerCol = p.GetComponent<Collider2D>();
            playerHealth = p.GetComponent<PlayerHealth>();
        }

        RefreshLayout();
        transform.position = startPos;
        Physics2D.SyncTransforms();
        brain = StartCoroutine(Brain());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 箱の大きさに合わせて、移動範囲と、開始位置(床の上の真ん中)を決める
    void RefreshLayout()
    {
        var arena = ArenaRotator.Instance;
        if (arena != null)
        {
            Rect inner = arena.InnerBounds;
            Vector3 ext = sr != null ? sr.bounds.extents : col.bounds.extents;
            minX = inner.xMin + ext.x;
            maxX = inner.xMax - ext.x;
            startPos = new Vector3(inner.center.x, inner.yMin + ext.y, transform.position.z);
        }
        else
        {
            minX = xRange.x;
            maxX = xRange.y;
        }
    }

    void Update()
    {
        if (LevelEditor.IsEditing || defeated || Time.timeScale == 0f) return;

        // 回転が終わったら、次の回転を選べるまでの待ちを始める
        var arena = ArenaRotator.Instance;
        bool rotatingNow = arena != null && arena.IsRotating;
        if (wasRotating && !rotatingNow) nextRotateTime = Time.time + rotateCooldown;
        wasRotating = rotatingNow;

        CheckContact();
    }

    void LateUpdate()
    {
        if (sr == null) return;
        sr.color = Time.time < flashUntil ? Color.white : brainColor;
    }

    // ---------- ダメージを受ける ----------
    public void TakeDamage(int damage, int fromDir)
    {
        if (defeated || LevelEditor.IsEditing) return;

        hp = Mathf.Max(0, hp - damage);
        flashUntil = Time.time + 0.08f;

        if (hp <= 0) StartCoroutine(DefeatRoutine());
    }

    IEnumerator DefeatRoutine()
    {
        defeated = true;
        if (brain != null) StopCoroutine(brain);
        if (chargeLine != null) chargeLine.enabled = false;
        ClearBullets();
        col.enabled = false;
        brainColor = Color.gray;
        label = "撃破!";

        yield return new WaitForSeconds(defeatDelay);

        if (RunController.Instance != null) RunController.Instance.EndRun(true);
    }

    // ---------- 初期状態に戻す(編集に戻るとき、やり直すとき) ----------
    public void ResetState()
    {
        StopAllCoroutines();
        ClearBullets();
        defeated = false;
        hp = maxHp;
        col.enabled = true;
        label = null;
        flashUntil = 0f;
        brainColor = baseColor;
        wasRotating = false;
        nextRotateTime = 0f;
        if (chargeLine != null) chargeLine.enabled = false;

        RefreshLayout();
        transform.position = startPos;
        Physics2D.SyncTransforms();
        brain = StartCoroutine(Brain());
    }

    static void ClearBullets()
    {
        foreach (var b in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
            Destroy(b.gameObject);
    }

    // ---------- 行動(順番は決まっておらず、重み付きの抽選) ----------
    IEnumerator Brain()
    {
        while (!defeated)
        {
            yield return new WaitForSeconds(idleTime);

            switch (PickAttack())
            {
                case Attack.Charge: yield return ChargeAttack(); break;
                case Attack.Shoot: yield return ShootAttack(); break;
                default: yield return RotateAttack(); break;
            }

            yield return new WaitForSeconds(recoverTime);
        }
    }

    Attack PickAttack()
    {
        var arena = ArenaRotator.Instance;
        bool canRotate = arena != null && !arena.IsRotating && Time.time >= nextRotateTime;
        float rw = canRotate ? rotateWeight : 0f;

        float r = Random.value * (chargeWeight + shootWeight + rw);
        if (r < chargeWeight) return Attack.Charge;
        r -= chargeWeight;
        if (r < shootWeight) return Attack.Shoot;
        return rw > 0f ? Attack.Rotate : Attack.Charge;
    }

    // 突進: 前兆(赤く点滅 + 進む線)のあと、箱の端まで走る
    IEnumerator ChargeAttack()
    {
        if (player == null) yield break;

        label = "突進!";
        int dir = 1;
        float t = 0f;
        chargeLine.enabled = true;
        while (t < chargeTelegraph)
        {
            t += Time.deltaTime;
            dir = player.position.x >= transform.position.x ? 1 : -1;     // 前兆の間は、プレイヤーの方を向く
            float endX = dir > 0 ? maxX : minX;
            chargeLine.SetPosition(0, transform.position);
            chargeLine.SetPosition(1, new Vector3(endX, transform.position.y, 0f));
            brainColor = ((int)(Time.time * 14f) % 2 == 0) ? Color.red : baseColor;
            yield return null;
        }
        chargeLine.enabled = false;
        brainColor = baseColor;
        label = null;

        float targetX = dir > 0 ? maxX : minX;
        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            float nx = Mathf.MoveTowards(transform.position.x, targetX, chargeSpeed * Time.deltaTime);
            transform.position = new Vector3(nx, transform.position.y, transform.position.z);
            Physics2D.SyncTransforms();
            yield return null;
        }
    }

    // 遠距離: 前兆(橙に点滅)のあと、プレイヤーを狙って、弾を連射する
    IEnumerator ShootAttack()
    {
        if (player == null) yield break;

        label = "遠距離!";
        float t = 0f;
        while (t < shootTelegraph)
        {
            t += Time.deltaTime;
            brainColor = ((int)(Time.time * 14f) % 2 == 0) ? new Color(1f, 0.55f, 0.1f) : baseColor;
            yield return null;
        }
        brainColor = baseColor;
        label = null;

        for (int i = 0; i < shotCount; i++)
        {
            Vector2 origin = transform.position;
            Vector2 dir = ((Vector2)player.position - origin).normalized;
            BossProjectile.Spawn(origin, dir * bulletSpeed, bulletDamage, blockLayers,
                playerCol, playerHealth, new Color(1f, 0.55f, 0.1f));
            yield return new WaitForSeconds(shotInterval);
        }
    }

    // 回転: 前兆(紫に点滅 + 方向の表示)のあと、箱を90度回す
    IEnumerator RotateAttack()
    {
        var arena = ArenaRotator.Instance;
        if (arena == null || arena.IsRotating) yield break;

        var dir = Random.value < 0.5f
            ? ArenaRotator.Direction.Clockwise
            : ArenaRotator.Direction.CounterClockwise;
        label = dir == ArenaRotator.Direction.Clockwise ? "回転!  時計回り" : "回転!  反時計回り";

        float t = 0f;
        while (t < rotateTelegraph)
        {
            t += Time.deltaTime;
            brainColor = ((int)(Time.time * 10f) % 2 == 0) ? new Color(0.7f, 0.3f, 1f) : baseColor;
            yield return null;
        }
        brainColor = baseColor;
        label = null;

        arena.TryRotate(dir);

        if (!actDuringRotation)
        {
            while (arena.IsRotating) yield return null;
            yield return new WaitForSeconds(rotateAfterDelay);
        }
    }

    // ---------- 接触ダメージ ----------
    void CheckContact()
    {
        if (playerCol == null || playerHealth == null) return;
        if (!col.bounds.Intersects(playerCol.bounds)) return;

        int fromDir = player.position.x >= transform.position.x ? 1 : -1;
        playerHealth.TakeDamage(contactDamage, fromDir);       // 無敵時間は、PlayerHealth が管理する
    }

    // ---------- 表示 ----------
    LineRenderer MakeLine()
    {
        var go = new GameObject("BossChargeLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        lr.endColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        lr.widthMultiplier = 0.2f;
        lr.sortingOrder = 5;
        lr.enabled = false;
        return lr;
    }

    void OnGUI()
    {
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // 体力バー
        float w = 600f, h = 22f;
        float x = (Screen.width - w) * 0.5f, y = 96f;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w * hp / maxHp, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 24f, w, 24f), $"フレーム   {hp}/{maxHp}");

        // 前兆の表示
        if (!string.IsNullOrEmpty(label))
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                bigStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            }
            GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, 80f), label, bigStyle);
        }
    }
}
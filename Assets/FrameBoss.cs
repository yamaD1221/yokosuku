using System.Collections;
using UnityEngine;

// ボス「フレーム」。箱の壁(上下左右)のどこかへ移動し、落下攻撃(衝撃波つき)か、遠距離攻撃を行う。
// 攻撃のあとは「攻撃タイミング」(ダウン)になり、プレイヤーの攻撃が大きく効く
[RequireComponent(typeof(Collider2D))]
public class FrameBoss : MonoBehaviour, IDamageable
{
    public static FrameBoss Instance { get; private set; }

    // 壁の番号は、衝撃波の道(床 → 右の壁 → 天井 → 左の壁)の順
    private enum Wall { Floor = 0, Right = 1, Ceiling = 2, Left = 3 }

    [Header("体力と、ダメージの倍率")]
    [SerializeField] private int maxHp = 40;
    [SerializeField] private float vulnerableMultiplier = 2f;     // 攻撃タイミング中の倍率
    [SerializeField] private float normalMultiplier = 0.5f;       // それ以外の倍率

    [Header("移動")]
    [SerializeField] private float idleTime = 0.2f;
    [SerializeField] private float moveTime = 0.4f;               // 別の壁へ移る時間

    [Header("攻撃の選び方")]
    [SerializeField] private float farDistance = 9f;              // プレイヤーがこれより遠いと、遠距離攻撃
    [SerializeField, Range(0f, 1f)] private float rotateChance = 0.25f;   // サイクルの頭で、回転攻撃を行う確率
    [SerializeField] private float rotateCooldown = 8f;           // 回転が終わってから、次の回転を選ばない秒数

    [Header("落下攻撃")]
    [SerializeField] private float slamTelegraph = 0.9f;          // 前兆の長さ
    [SerializeField] private float slamLockTime = 0.3f;           // 着地点が固定される、最後の秒数
    [SerializeField] private float diveTime = 0.35f;              // 落下の時間
    [SerializeField] private int diveDamage = 1;                  // 落下中に、体に触れたときのダメージ
    [SerializeField] private float downTimeAfterSlam = 3f;        // 攻撃タイミング(ダウン)の長さ

    [Header("衝撃波(箱の壁に沿って、両側へ走る)")]
    [SerializeField] private float waveSpeed = 10f;
    [SerializeField] private float waveRadius = 0.9f;
    [SerializeField] private float waveInset = 0.45f;             // 壁から、波の中心までの距離
    [SerializeField] private int waveDamage = 1;

    [Header("遠距離攻撃(弾)")]
    [SerializeField] private float shootTelegraph = 0.5f;
    [SerializeField] private int shotCount = 3;
    [SerializeField] private float shotInterval = 0.12f;
    [SerializeField] private float bulletSpeed = 14f;
    [SerializeField] private int bulletDamage = 1;
    [SerializeField] private float downTimeAfterShoot = 2f;
    [SerializeField] private LayerMask blockLayers;               // 弾が止まる壁・足場のレイヤー(StickWall と NoStickWall)

    [Header("回転攻撃")]
    [SerializeField] private float rotateTelegraph = 1.2f;

    [Header("撃破")]
    [SerializeField] private float defeatDelay = 0.8f;

    [SerializeField] private Material lineMaterial;               // 前兆の線の素材(空でもOK)

    private float hp;
    private bool defeated;
    private bool vulnerable;                 // 攻撃タイミング中か
    private bool contactEnabled;             // 体に触れるとダメージを受けるか(落下中だけ)
    private Wall currentWall = Wall.Floor;
    private Vector3 startPos;
    private Vector2 halfSize = Vector2.one;
    private Collider2D col;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;
    private Color brainColor = Color.white;
    private float flashUntil;
    private Color flashColor = Color.white;
    private string label;
    private LineRenderer slamLine;
    private GameObject marker;
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
        slamLine = MakeLine();
        marker = MakeMarker();
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
        if (marker != null) Destroy(marker);
        if (Instance == this) Instance = null;
    }

    // ---------- 箱の大きさに合わせた、位置の計算 ----------
    Rect Inner()
    {
        var arena = ArenaRotator.Instance;
        return arena != null ? arena.InnerBounds : new Rect(-8f, -8f, 16f, 16f);
    }

    void RefreshLayout()
    {
        Vector3 ext = sr != null ? sr.bounds.extents : col.bounds.extents;
        halfSize = new Vector2(ext.x, ext.y);
        startPos = new Vector3(SpotOn(Wall.Floor, 0.5f).x, SpotOn(Wall.Floor, 0.5f).y, transform.position.z);
        currentWall = Wall.Floor;
    }

    // 壁の上の位置(u: 0?1)に、ボスの中心が来る座標
    Vector2 SpotOn(Wall wall, float u)
    {
        Rect r = Inner();
        float minX = r.xMin + halfSize.x, maxX = r.xMax - halfSize.x;
        float minY = r.yMin + halfSize.y, maxY = r.yMax - halfSize.y;
        switch (wall)
        {
            case Wall.Floor: return new Vector2(Mathf.Lerp(minX, maxX, u), minY);
            case Wall.Ceiling: return new Vector2(Mathf.Lerp(minX, maxX, u), maxY);
            case Wall.Left: return new Vector2(minX, Mathf.Lerp(minY, maxY, u));
            default: return new Vector2(maxX, Mathf.Lerp(minY, maxY, u));
        }
    }

    // プレイヤーの位置を、その壁の上の位置(0?1)に直す
    float ProjectU(Wall wall, Vector2 p)
    {
        Rect r = Inner();
        bool horizontal = wall == Wall.Floor || wall == Wall.Ceiling;
        float v = horizontal ? p.x : p.y;
        float min = horizontal ? r.xMin + halfSize.x : r.yMin + halfSize.y;
        float max = horizontal ? r.xMax - halfSize.x : r.yMax - halfSize.y;
        return Mathf.InverseLerp(min, max, v);
    }

    static Wall Opposite(Wall w) => (Wall)(((int)w + 2) % 4);

    void SetPos(Vector2 p)
    {
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        Physics2D.SyncTransforms();
    }

    // ---------- 毎フレーム ----------
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
        sr.color = Time.time < flashUntil ? flashColor : brainColor;
    }

    // ---------- ダメージを受ける ----------
    public void TakeDamage(int damage, int fromDir)
    {
        if (defeated || LevelEditor.IsEditing) return;

        float mult = vulnerable ? vulnerableMultiplier : normalMultiplier;
        hp = Mathf.Max(0f, hp - damage * mult);

        // 攻撃タイミング中は、明るく光る。それ以外は、鈍く光る
        flashColor = vulnerable ? Color.white : new Color(0.65f, 0.65f, 0.7f);
        flashUntil = Time.time + 0.08f;

        if (hp <= 0f) StartCoroutine(DefeatRoutine());
    }

    IEnumerator DefeatRoutine()
    {
        defeated = true;
        vulnerable = false;
        if (brain != null) StopCoroutine(brain);
        HideTelegraph();
        ClearAttacks();
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
        ClearAttacks();
        HideTelegraph();
        defeated = false;
        vulnerable = false;
        contactEnabled = false;
        hp = maxHp;
        col.enabled = true;
        label = null;
        flashUntil = 0f;
        brainColor = baseColor;
        wasRotating = false;
        nextRotateTime = 0f;

        RefreshLayout();
        transform.position = startPos;
        Physics2D.SyncTransforms();
        brain = StartCoroutine(Brain());
    }

    // 飛んでいる弾と、走っている衝撃波を、すべて消す
    static void ClearAttacks()
    {
        foreach (var b in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach (var w in FindObjectsByType<BossShockwave>(FindObjectsSortMode.None)) Destroy(w.gameObject);
    }

    void HideTelegraph()
    {
        if (slamLine != null) slamLine.enabled = false;
        if (marker != null) marker.SetActive(false);
    }

    // ---------- 行動 ----------
    IEnumerator Brain()
    {
        while (!defeated)
        {
            yield return new WaitForSeconds(idleTime);

            // ときどき、回転攻撃
            var arena = ArenaRotator.Instance;
            if (arena != null && !arena.IsRotating && Time.time >= nextRotateTime && Random.value < rotateChance)
                yield return RotateAttack();

            // 移動: 別の壁へ
            yield return MoveToAnotherWall();

            // 判断: プレイヤーが遠ければ、遠距離。近ければ、落下攻撃
            bool far = player != null && Vector2.Distance(player.position, transform.position) > farDistance;
            if (far)
            {
                yield return ShootAttack();
                yield return DownTime(downTimeAfterShoot);
            }
            else
            {
                yield return SlamAttack();
                yield return DownTime(downTimeAfterSlam);
            }
        }
    }

    // 移動: 今いる壁とは別の壁の、どこかへ滑って移る(移動中は、触れても安全)
    IEnumerator MoveToAnotherWall()
    {
        Wall next;
        do { next = (Wall)Random.Range(0, 4); } while (next == currentWall);

        Vector2 from = transform.position;
        Vector2 to = SpotOn(next, Random.value);

        float t = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveTime));
            SetPos(Vector2.Lerp(from, to, k));
            yield return null;
        }
        SetPos(to);
        currentWall = next;
    }

    // 落下攻撃: 向かいの壁に、前兆(赤く点滅、着地点の印と線)を出して、落下する。着地すると、衝撃波が走る
    IEnumerator SlamAttack()
    {
        if (player == null) yield break;

        Wall target = Opposite(currentWall);
        float lockedU = ProjectU(target, player.position);
        label = "落下!";
        slamLine.enabled = true;
        marker.SetActive(true);

        float t = 0f;
        while (t < slamTelegraph)
        {
            t += Time.deltaTime;
            bool locked = slamTelegraph - t <= slamLockTime;
            if (!locked) lockedU = ProjectU(target, player.position);     // 固定されるまでは、プレイヤーを追う

            Vector2 to = SpotOn(target, lockedU);
            slamLine.SetPosition(0, transform.position);
            slamLine.SetPosition(1, to);
            marker.transform.position = to;
            SetTelegraphAlpha(locked ? 1f : 0.35f);                       // 固定されると、濃くなる
            brainColor = ((int)(Time.time * 14f) % 2 == 0) ? Color.red : baseColor;
            yield return null;
        }
        HideTelegraph();
        brainColor = baseColor;
        label = null;

        // 落下(だんだん速くなる)。落下中は、体に触れるとダメージ
        Vector2 from = transform.position;
        Vector2 dest = SpotOn(target, lockedU);
        contactEnabled = true;
        t = 0f;
        while (t < diveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / diveTime);
            SetPos(Vector2.Lerp(from, dest, k * k));
            yield return null;
        }
        SetPos(dest);
        currentWall = target;
        contactEnabled = false;

        SpawnShockwaves(target, dest);
    }

    // 衝撃波: 着地した場所から、壁に沿って、両側へ走る
    void SpawnShockwaves(Wall wall, Vector2 impact)
    {
        Rect inner = Inner();
        var loop = new Rect(inner.xMin + waveInset, inner.yMin + waveInset,
            inner.width - waveInset * 2f, inner.height - waveInset * 2f);

        float s0 = BossShockwave.ParamOnLoop(loop, (int)wall, impact);
        float half = BossShockwave.Perimeter(loop) * 0.5f;           // 反対側で出会うまで
        var color = new Color(1f, 0.35f, 0.2f, 0.9f);

        BossShockwave.Spawn(loop, s0, +1, waveSpeed, half, waveRadius, waveDamage, playerCol, playerHealth, color);
        BossShockwave.Spawn(loop, s0, -1, waveSpeed, half, waveRadius, waveDamage, playerCol, playerHealth, color);
    }

    // 遠距離攻撃: 前兆(橙に点滅)のあと、プレイヤーを狙って、弾を連射する
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

    // 攻撃タイミング(ダウン): 黄色く点滅。この間は、プレイヤーの攻撃が大きく効く
    IEnumerator DownTime(float duration)
    {
        vulnerable = true;
        label = "チャンス!";

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            brainColor = ((int)(Time.time * 8f) % 2 == 0) ? new Color(1f, 0.9f, 0.2f) : baseColor;
            yield return null;
        }

        vulnerable = false;
        brainColor = baseColor;
        label = null;
    }

    // 回転攻撃: 前兆(紫に点滅 + 方向の表示)のあと、箱を90度回す
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
    }

    // ---------- 接触ダメージ(落下中だけ) ----------
    void CheckContact()
    {
        if (!contactEnabled || playerCol == null || playerHealth == null) return;
        if (!col.bounds.Intersects(playerCol.bounds)) return;

        int fromDir = player.position.x >= transform.position.x ? 1 : -1;
        playerHealth.TakeDamage(diveDamage, fromDir);          // 無敵時間は、PlayerHealth が管理する
    }

    // ---------- 表示 ----------
    LineRenderer MakeLine()
    {
        var go = new GameObject("BossSlamLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.widthMultiplier = 0.2f;
        lr.sortingOrder = 5;
        lr.enabled = false;
        return lr;
    }

    GameObject MakeMarker()
    {
        var go = new GameObject("BossSlamMarker");
        var msr = go.AddComponent<SpriteRenderer>();
        msr.sprite = BossShockwave.SquareSprite;
        msr.sortingOrder = 4;
        go.transform.localScale = Vector3.one * 1.5f;
        go.SetActive(false);
        return go;
    }

    void SetTelegraphAlpha(float a)
    {
        var c = new Color(1f, 0.2f, 0.2f, a);
        slamLine.startColor = c;
        slamLine.endColor = c;
        var msr = marker.GetComponent<SpriteRenderer>();
        msr.color = new Color(1f, 0.2f, 0.2f, a * 0.8f);
    }

    void OnGUI()
    {
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // 体力バー(攻撃タイミング中は、枠が黄色くなる)
        float w = 600f, h = 22f;
        float x = (Screen.width - w) * 0.5f, y = 96f;
        GUI.color = vulnerable ? new Color(1f, 0.9f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Texture2D.whiteTexture);
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w * hp / maxHp, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 24f, w, 24f), $"フレーム   {Mathf.CeilToInt(hp)}/{maxHp}");

        // 前兆と、攻撃タイミングの表示
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
            }
            bigStyle.normal.textColor = vulnerable ? new Color(1f, 0.95f, 0.3f) : new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, 80f), label, bigStyle);
        }
    }
}
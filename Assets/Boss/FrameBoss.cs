using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ボス「フレーム」。箱の壁(上下左右)のどこかへ移動し、落下攻撃(衝撃波つき)か、遠距離攻撃を行う。
// 攻撃のあとは「攻撃タイミング」(ダウン)になり、プレイヤーの攻撃が大きく効く
[RequireComponent(typeof(Collider2D))]
public class FrameBoss : MonoBehaviour, IDamageable
{
    // ボスのインスタンスを管理する
    public static FrameBoss Instance { get; private set; }

    // 壁の番号は、衝撃波の道(床 → 右の壁 → 天井 → 左の壁)の順
    private enum Wall { Floor = 0, Right = 1, Ceiling = 2, Left = 3 }

    // 体力と、ダメージの倍率
    [Header("体力と、ダメージの倍率")]
    [SerializeField] private int maxHp = 40;                       // ボスの最大体力
    [SerializeField] private float vulnerableMultiplier = 2f;    // 攻撃タイミング中に受けるダメージの倍率
    [SerializeField] private float normalMultiplier = 0.5f;      // 通常時に受けるダメージの倍率

    // 移動
    [Header("移動")]
    [SerializeField] private float idleTime = 0.2f;               // 次の行動に移るまでの待ち時間
    private const float moveTime = 1.7f;                       // ワープ前の予告時間（必ず3秒）
    [SerializeField] private float teleportBlinkInterval = 0.5f;  // 点滅開始時の間隔
    [SerializeField] private float teleportBlinkFastInterval = 0.06f; // カウント終了直前の点滅間隔
    [SerializeField] private float markerStartScale = 2.4f;        // マーカー表示直後の大きさ（ここから収束する）
    [SerializeField] private float markerFinalScale = 0.9f;        // マーカーが収束しきったときの大きさ
    [SerializeField] private float destinationPadding = 0.15f;     // ボスの幅に追加する余裕

    // 初期障害物への着地
    [Header("初期障害物への着地")]
    [SerializeField] private float initialObjectSearchDistance = 8f; // 近くの足場を探す距離

    // 攻撃の選び方
    [Header("攻撃の選び方")]
    [SerializeField] private float farDistance = 9f;              // プレイヤーがこれより遠いと遠距離攻撃を選ぶ
    [SerializeField, Range(0f, 1f)] private float rotateChance = 0.25f; // 行動サイクルの最初に回転攻撃を行う確率
    [SerializeField] private float rotateCooldown = 8f;           // 回転攻撃後、次に回転攻撃を選べるまでの時間

    // 落下攻撃
    [Header("落下攻撃")]
    // 落下攻撃の予告時間は移動と同じ moveTime(3秒) を使用する
    [SerializeField] private int diveDamage = 1;                   // 落下中にボス本体へ触れたときのダメージ
    [SerializeField] private float downTimeAfterSlam = 2f;         // 落下攻撃後の攻撃チャンスの時間
    [SerializeField] private float slamDownExtraTime = 1f;      // 落下後ダウンの追加時間（ダウンは落下攻撃の後だけ）
    [SerializeField] private float minFallHeight = 8f;          // 落下開始地点の最低の高さ（着地点からこの分は必ず上から落とす）

    // 雑魚召喚（HPが半分以下になったら1回だけ）
    [SerializeField] private GameObject zakoPrefab;             // 召喚する雑魚（Enemyプレハブを入れる。空なら召喚しない）
    [SerializeField] private int zakoSummonCount = 2;           // 1回で召喚する雑魚の数
    [SerializeField] private float zakoSummonHpRatio = 0.5f;    // HPがこの割合以下になったら召喚する（0.5 = 半分）
    private bool zakoSummoned;                                  // 召喚済みかどうか
    private readonly System.Collections.Generic.List<GameObject> summonedZako = new System.Collections.Generic.List<GameObject>(); // 召喚した雑魚（リセット時に消すため）

    // 衝撃波(箱の壁に沿って、両側へ走る)
    [Header("衝撃波(箱の壁に沿って、両側へ走る)")]
    [SerializeField] private float waveSpeed = 10f;                // 衝撃波が進む速度
    [SerializeField] private float waveRadius = 0.9f;               // 衝撃波の当たり判定の大きさ
    [SerializeField] private float waveInset = 0.12f;               // 障害物の輪郭から衝撃波の中心までの距離
    [SerializeField] private float wavePathSampleSpacing = 0.15f;   // 輪郭を取得する間隔。小さいほど凹凸を細かく再現
    [SerializeField] private int waveDamage = 1;                    // 衝撃波がプレイヤーに与えるダメージ

    // 遠距離攻撃(弾)
    [Header("遠距離攻撃(弾)")]
    [SerializeField] private float shootTelegraph = 0.5f;          // 弾を撃つ前の予兆時間
    [SerializeField] private int shotCount = 3;                     // 一度の攻撃で発射する弾の数
    [SerializeField] private float shotInterval = 0.12f;            // 弾を発射する間隔
    [SerializeField] private float bulletSpeed = 14f;               // 弾の移動速度
    [SerializeField] private int bulletDamage = 1;                  // 弾がプレイヤーに与えるダメージ
    [SerializeField] private float downTimeAfterShoot = 2f;         // 遠距離攻撃後の攻撃チャンスの時間
    [SerializeField] private LayerMask blockLayers;                 // 弾が止まる壁・足場のレイヤー(StickWall と NoStickWall)
    [SerializeField] private float aimRingStartRadius = 2.3f;      // 遠距離予告で収束する赤い円の開始半径
    [SerializeField] private float aimRingEndRadius = 0.45f;       // 遠距離予告で収束する赤い円の最終半径

    // 回転攻撃
    [Header("回転攻撃")]
    [SerializeField] private float rotateTelegraph = 3.0f;          // 箱を回転させる前の予兆時間

    // 撃破
    [Header("撃破")]
    [SerializeField] private float defeatDelay = 0.8f;              // 撃破演出後、結果画面を出すまでの時間

    // 回転予告用の箱の淵の発光
    private GameObject[] rotationEdgeLights = new GameObject[4];

    // 発光させる色
    [SerializeField]
    private Color rotationEdgeColor =
        new Color(0.7f, 0.3f, 1f, 0.9f);

    // 回転予告の光の線
    private GameObject rotationLineObject;
    private LineRenderer rotationLine;

    [SerializeField]
    private Color rotationLineColor =
    new Color(1f, 0.45f, 0.05f, 1f);

    [SerializeField] private float rotationLineWidth = 0.15f;

    // 淵の光の太さ
    [SerializeField] private float rotationEdgeThickness = 0.12f;

    // 現在のボスの体力
    private float hp;

    // ボスが撃破されたかどうか
    private bool defeated;

    // 攻撃タイミング(ダウン)中かどうか
    private bool vulnerable;

    // 落下中に、プレイヤーへ接触ダメージを与えるかどうか
    private bool contactEnabled;

    // 現在ボスがいる壁
    private Wall currentWall = Wall.Floor;

    // ボスの初期位置
    private Vector3 startPos;

    // ボスの大きさ(横幅と高さの半分)
    private Vector2 halfSize = Vector2.one;

    // ボスの当たり判定と物理演算
    private Collider2D col;
    private Rigidbody2D rb;

    // ワープ出現位置を示すマーカー(通常移動は青、落下攻撃は赤)
    private GameObject spawnMarker;
    private GameObject aimLineObject;
    private LineRenderer aimLine;
    private GameObject aimRingObject;
    private LineRenderer aimRing;

    // ボスの画像を表示するコンポーネント
    private SpriteRenderer sr;

    // ----- 見た目（オブジェクト側で組んだ子オブジェクト） -----
    // 枠一式（Frame／Plate／Cable／Boltなど）のRendererと、その元の色
    private readonly List<SpriteRenderer> frameSrs = new List<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, Color> frameBaseColors =
        new Dictionary<SpriteRenderer, Color>();

    // コアと、その元の色・元の位置
    private Transform coreTf;
    private SpriteRenderer coreSr;
    private Color coreBaseColor = Color.white;
    private Vector3 coreHomePos;

    // 瞳と、その元の位置・大きさ、コアの中で動ける最大量
    private Transform pupilTf;
    private Vector3 pupilBaseScale = Vector3.one;

    // 瞳の追従はワールド座標で行う（親がCoreでもBossでも効くように）
    private Vector3 pupilHomeWorldOffset;
    private float pupilMaxWorldOffset = 0.1f;

    // 瞳の動く量の倍率（Inspectorで調整可能。動きが小さく見えたら上げる）
    [SerializeField] private float pupilMoveScale = 1f;

    // 瞼と、その元の大きさ、今どれだけ閉じているか（0=開、1=閉）
    private Transform eyelidTf;
    private Vector3 eyelidFullScale = Vector3.one;
    private float eyelidClosed01;

    // 瞬きのタイミング
    private float nextBlinkTime;
    private float blinkUntil;

    // ボスの通常時の色
    private Color baseColor = Color.white;

    // ボスの現在の表示色
    private Color brainColor = Color.white;

    // ダメージを受けたときの点滅終了時刻
    private float flashUntil;

    // ダメージを受けたときに表示する色
    private Color flashColor = Color.white;

    // ダメージを受けた時間
    private float lastHitTime = -10f;

    // 点滅する時間
    private float hitFlashDuration = 0.4f;


    // 落下攻撃の着地点を表示する赤いマーカー
    private GameObject marker;

    // プレイヤーのTransform
    private Transform player;

    // プレイヤーの当たり判定
    private Collider2D playerCol;

    // プレイヤーの体力を管理するコンポーネント
    private PlayerHealth playerHealth;

    // ボスの行動を管理するコルーチン
    private Coroutine brain;


    // 落下中か、着地したかを管理する
    private bool isFalling;
    private bool hasLanded;

    // 前のフレームで箱が回転していたかどうか
    private bool wasRotating;

    // 次に回転攻撃を選択できる時刻
    private float nextRotateTime;

    // 落下攻撃で最後に着地した足場のCollider（黄色い壁の耐久判定に使う）
    private Collider2D lastLandingCollider;

    // 落下中にすり抜けるStickWall(白ground)のCollider一覧（落下後に元へ戻す）
    private readonly System.Collections.Generic.List<Collider2D> fallIgnoredColliders =
        new System.Collections.Generic.List<Collider2D>();

    // 黄色い壁(NotStickWall)が落下攻撃を受けた回数（1回でヒビ、2回で破壊）
    private readonly System.Collections.Generic.Dictionary<Collider2D, int> noStickSlamHits =
        new System.Collections.Generic.Dictionary<Collider2D, int>();

    // 通常攻撃の種類
    private enum MainAttackType
    {
        Shoot,  // 遠距離攻撃
        Slam    // 落下攻撃
    }

    // 直近の通常攻撃
    private MainAttackType lastMainAttack;

    // 同じ通常攻撃が連続した回数
    private int consecutiveMainAttackCount = 0;

    // 初回はまだ攻撃していない
    private bool hasMainAttackHistory = false;

    void Awake()
    {
        // このボスを、他のスクリプトから参照できるようにする
        Instance = this;

        // ボス自身の当たり判定を取得
        col = GetComponent<Collider2D>();

        // 子オブジェクトも含めて、SpriteRendererを取得
        sr = GetComponentInChildren<SpriteRenderer>();

        // SpriteRendererが存在する場合、初期の色を保存
        if (sr != null) baseColor = sr.color;

        // ボスの表示色を初期色に設定
        brainColor = baseColor;

        // オブジェクト側で組んだ見た目の部品（枠・コア・瞳・瞼）を拾う
        SetupVisualParts();

        // Rigidbody2Dを取得
        rb = GetComponent<Rigidbody2D>();

        // Rigidbody2Dがなければ追加する
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        // 通常時はスクリプトで位置を制御する
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // ボスの初期位置を保存
        startPos = transform.position;

        // ボスの体力を最大体力に設定
        hp = maxHp;

        // 旧着地点マーカーは作成しない。落下攻撃では出現位置マーカーのみを使う。
        marker = null;

        // ワープ後の出現位置を表示するマーカーを作成
        spawnMarker = MakeMarker("BossTeleportSpawnMarker");
    }

    void Start()
    {
        // Playerタグが付いたオブジェクトを検索
        var p = GameObject.FindWithTag("Player");

        // プレイヤーが見つかった場合、必要なコンポーネントを取得
        if (p != null)
        {
            // プレイヤーのTransformを取得
            player = p.transform;

            // プレイヤーの当たり判定を取得
            playerCol = p.GetComponent<Collider2D>();

            // プレイヤーの体力を管理するコンポーネントを取得
            playerHealth = p.GetComponent<PlayerHealth>();
        }

        // 箱の大きさに合わせて、ボスの初期配置を計算
        RefreshLayout();

        // ボスを初期位置に配置
        transform.position = startPos;

        // 位置の変更を物理演算に反映
        Physics2D.SyncTransforms();

        // ボスの行動を管理するコルーチンを開始
        brain = StartCoroutine(Brain());
    }

    void OnDestroy()
    {
        // マーカーが残っていれば削除
        if (marker != null) Destroy(marker);
        if (spawnMarker != null) Destroy(spawnMarker);
        if (aimLineObject != null) Destroy(aimLineObject);
        if (aimRingObject != null) Destroy(aimRingObject);

        // このボスが現在のInstanceなら、参照を解除
        if (Instance == this) Instance = null;

        foreach (GameObject light in rotationEdgeLights)
        {
            if (light != null)
                Destroy(light);
        }

        if (rotationLineObject != null)
        {
            Destroy(rotationLineObject);
        }
    }

    // 箱の内側の範囲を取得する
    Rect Inner()
    {
        // 箱の回転や大きさを管理するArenaRotatorを取得
        var arena = ArenaRotator.Instance;

        // ArenaRotatorが存在すれば、箱の内側の範囲を返す
        // 存在しなければ、中心が(0, 0)、幅と高さが16の範囲を返す
        return arena != null ? arena.InnerBounds : new Rect(-8f, -8f, 16f, 16f);
    }

    void RefreshLayout()
    {
        // SpriteRendererがあれば画像の大きさを取得し、なければ当たり判定の大きさを取得
        Vector3 ext = sr != null ? sr.bounds.extents : col.bounds.extents;

        // ボスの横幅と高さの半分を保存
        halfSize = new Vector2(ext.x, ext.y);

        // 床の中央をボスの初期位置に設定
        // Z座標は現在の位置をそのまま使用する
        startPos = new Vector3(
            SpotOn(Wall.Floor, 0.5f).x,
            SpotOn(Wall.Floor, 0.5f).y,
            transform.position.z
        );

        // ボスが最初にいる壁を床に設定
        currentWall = Wall.Floor;
    }
    // 指定した壁の上で、ボスの中心が来る座標を計算する
    // uは壁の位置を表す値で、0が端、1が反対側の端
    Vector2 SpotOn(Wall wall, float u)
    {
        // 箱の内側の範囲を取得
        Rect r = Inner();

        // ボスが壁からはみ出さないように、ボスの大きさ分だけ範囲を内側にする
        float minX = r.xMin + halfSize.x, maxX = r.xMax - halfSize.x;
        float minY = r.yMin + halfSize.y, maxY = r.yMax - halfSize.y;

        // 指定された壁に応じて、ボスの中心座標を計算
        switch (wall)
        {
            // 床: 左から右へ移動
            case Wall.Floor:
                return new Vector2(Mathf.Lerp(minX, maxX, u), minY);

            // 天井: 左から右へ移動
            case Wall.Ceiling:
                return new Vector2(Mathf.Lerp(minX, maxX, u), maxY);

            // 左の壁: 下から上へ移動
            case Wall.Left:
                return new Vector2(minX, Mathf.Lerp(minY, maxY, u));

            // 右の壁: 下から上へ移動
            default:
                return new Vector2(maxX, Mathf.Lerp(minY, maxY, u));
        }
    }

    // プレイヤーの位置を、指定した壁の上の位置(0～1)に変換する
    float ProjectU(Wall wall, Vector2 p)
    {
        // 箱の内側の範囲を取得
        Rect r = Inner();

        // 床または天井なら横方向、それ以外なら縦方向として扱う
        bool horizontal = wall == Wall.Floor || wall == Wall.Ceiling;

        // 壁の方向に合わせて、プレイヤーの座標を取得
        float v = horizontal ? p.x : p.y;

        // ボスの大きさを考慮して、移動できる範囲を計算
        float min = horizontal ? r.xMin + halfSize.x : r.yMin + halfSize.y;
        float max = horizontal ? r.xMax - halfSize.x : r.yMax - halfSize.y;

        // プレイヤーの位置を0～1の範囲に変換して返す
        return Mathf.InverseLerp(min, max, v);
    }

    // 指定された壁の反対側にある壁を取得する
    static Wall Opposite(Wall w)
    {
        // 壁の番号に2を足して4で割った余りを返す
        // 床なら天井、右なら左、天井なら床、左なら右になる
        return (Wall)(((int)w + 2) % 4);
    }

    // ボスの位置を指定した座標に移動させる
    void SetPos(Vector2 p)
    {
        // X座標とY座標を指定した位置に変更する
        // Z座標は現在の値を維持する
        transform.position = new Vector3(p.x, p.y, transform.position.z);

        // Transformの変更を物理演算に反映させる
        Physics2D.SyncTransforms();
    }

    // 毎フレーム実行する処理
    void Update()
    {
        // 編集モード中、ボス撃破後、またはゲームが一時停止中なら処理を終了
        if (LevelEditor.IsEditing || defeated || Time.timeScale == 0f) return;

        // 箱の回転を管理するArenaRotatorを取得
        var arena = ArenaRotator.Instance;

        // 現在、箱が回転しているかどうかを確認
        bool rotatingNow = arena != null && arena.IsRotating;

        // 前のフレームでは回転中で、現在は回転が終わっている場合
        // 次の回転攻撃を選べるまでの待ち時間を設定
        if (wasRotating && !rotatingNow)
            nextRotateTime = Time.time + rotateCooldown;

        // 現在の回転状態を保存
        wasRotating = rotatingNow;

        // プレイヤーとボスの接触を確認し、必要ならダメージを与える
        CheckContact();

        // ボスとプレイヤーが重なっていたら、プレイヤーを押し出してめり込みを解消する
        ResolvePlayerOverlap();
    }

    // 毎フレームの最後に、ボスの色を更新する
    void LateUpdate()
    {
        // SpriteRendererが存在しなければ処理を終了
        if (sr == null) return;

        // ダメージを受けたときの点滅時間内なら点滅用の色にする
        // それ以外の場合は、通常の表示色にする
        Color tint = Time.time < flashUntil ? flashColor : brainColor;
        sr.color = tint;

        // オブジェクト側で組んだ見た目にも色と動きを反映する
        UpdateVisualParts(tint);

        // 着地の衝撃リングを広げる
        UpdateImpactRings();
    }

    // 子孫全体から名前でTransformを探す（直下でなくても拾える）
    Transform FindDeep(string objName)
    {
        Transform loose = null;
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr.name == objName) return tr;
            // 「Pupil (1)」のような複製名や大文字小文字の違いでも拾えるようにする
            if (loose == null && tr.name.StartsWith(objName, System.StringComparison.OrdinalIgnoreCase))
                loose = tr;
        }
        return loose;
    }

    // オブジェクト側で組んだ見た目の部品を名前で拾う
    // 枠：Bossの子（CoreとEyelid以外）／コア：Core／瞳：Coreの子のPupil／瞼：Eyelid
    void SetupVisualParts()
    {
        frameSrs.Clear();
        frameBaseColors.Clear();

        // 名前での探索は階層全体から行う（直下に無くても拾えるように）
        coreTf = FindDeep("Core");
        if (coreTf != null)
        {
            coreSr = coreTf.GetComponent<SpriteRenderer>();
            if (coreSr != null) coreBaseColor = coreSr.color;
            coreHomePos = coreTf.localPosition;

            pupilTf = FindDeep("Pupil");
            if (pupilTf != null)
            {
                pupilBaseScale = pupilTf.localScale;

                // 瞳のホーム位置（コア中心からのワールド座標のずれ）を覚える
                pupilHomeWorldOffset = pupilTf.position - coreTf.position;

                // 実際の大きさから、コアの中に瞳が収まる最大の移動量（ワールド座標）を出す
                var pupilSr = pupilTf.GetComponent<SpriteRenderer>();
                if (coreSr != null && pupilSr != null)
                {
                    float coreRadius = coreSr.bounds.extents.x;
                    float pupilRadius = pupilSr.bounds.extents.x;
                    // コアに収まる量だと、瞳が大きい場合に動きがほぼ見えなくなるため、
                    // 最低でもコア半径の18%は動くようにする（ほんの少しだけはみ出すのは許容）
                    float fit = coreRadius - pupilRadius - 0.02f;
                    float visible = coreRadius * 0.18f;
                    pupilMaxWorldOffset = Mathf.Max(fit, visible) * pupilMoveScale;
                }
            }
        }

        eyelidTf = FindDeep("Eyelid");
        if (eyelidTf != null)
        {
            eyelidFullScale = eyelidTf.localScale;

            // 最初は開いた状態（縦を0にして隠す）
            var s = eyelidTf.localScale;
            s.y = 0f;
            eyelidTf.localScale = s;
            eyelidClosed01 = 0f;
        }

        // 枠一式：コアと瞼以外の子Rendererを全部拾い、元の色を覚える
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == sr) continue;
            if (coreTf != null && r.transform.IsChildOf(coreTf)) continue;
            if (pupilTf != null && r.transform == pupilTf) continue;
            if (eyelidTf != null && r.transform == eyelidTf) continue;

            frameSrs.Add(r);
            frameBaseColors[r] = r.color;
        }

        nextBlinkTime = Time.time + 2.5f;
    }

    // 見た目の部品に、色（予告の点滅）・瞳の向き・瞼・コアの落下を反映する
    void UpdateVisualParts(Color tint)
    {
        float dt = Time.deltaTime;
        bool flashing = Time.time < flashUntil;

        // 枠一式の色：通常時は元の色、予告や点滅のときはその色へ寄せる
        foreach (var r in frameSrs)
        {
            Color baseCol = frameBaseColors[r];
            if (!flashing && tint == baseColor)
            {
                r.color = baseCol;
            }
            else
            {
                Color c = Color.Lerp(baseCol, tint, flashing ? 0.85f : 0.8f);
                c.a = baseCol.a;
                r.color = c;
            }
        }

        // コア：チャンス中は下へ落ちて暗くなる
        if (coreTf != null)
        {
            Vector3 targetPos = coreHomePos + (vulnerable ? new Vector3(0f, -0.13f, 0f) : Vector3.zero);
            float k = 1f - Mathf.Exp(-10f * dt);
            coreTf.localPosition = Vector3.Lerp(coreTf.localPosition, targetPos, k);

            if (coreSr != null)
            {
                if (flashing) coreSr.color = flashColor;
                else if (vulnerable)
                {
                    Color c = coreBaseColor * 0.55f;
                    c.a = coreBaseColor.a;
                    coreSr.color = c;
                }
                else coreSr.color = coreBaseColor;
            }
        }

        // 瞼：チャンス中だけ閉じる
        if (eyelidTf != null)
        {
            float target = vulnerable ? 1f : 0f;
            eyelidClosed01 = Mathf.MoveTowards(eyelidClosed01, target, dt / 0.15f);

            var s = eyelidFullScale;
            s.y = eyelidFullScale.y * eyelidClosed01;
            eyelidTf.localScale = s;
            eyelidTf.gameObject.SetActive(eyelidClosed01 > 0.01f);
        }

        // 瞳：マーカーが出ていればマーカーを、なければプレイヤーを見る
        if (pupilTf != null && coreTf != null)
        {
            Vector3 targetWorld = player != null ? player.position : coreTf.position;
            if (spawnMarker != null && spawnMarker.activeSelf)
                targetWorld = spawnMarker.transform.position;

            Vector2 dir = (Vector2)targetWorld - (Vector2)coreTf.position;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();

            // ワールド座標で瞳を動かす（親の違いや大きさ設定の影響を受けない）
            Vector3 desiredWorld = coreTf.position + pupilHomeWorldOffset
                + (Vector3)(dir * pupilMaxWorldOffset);
            float k = 1f - Mathf.Exp(-14f * dt);
            pupilTf.position = Vector3.Lerp(pupilTf.position, desiredWorld, k);

            // たまに瞬きする（チャンス中は瞼が閉じるのでしない）
            if (!vulnerable && Time.time >= nextBlinkTime)
            {
                blinkUntil = Time.time + 0.12f;
                nextBlinkTime = Time.time + Random.Range(2.2f, 4.2f);
            }

            // チャンス中は瞳を隠す（瞼が閉じるのに合わせて縮んで消える）
            // 瞼が無い場合は、チャンスに入った瞬間に消える
            float hide01 = eyelidTf != null ? eyelidClosed01 : (vulnerable ? 1f : 0f);
            var ps = pupilBaseScale * (1f - hide01);
            if (Time.time < blinkUntil) ps.y *= 0.12f;
            pupilTf.localScale = ps;
        }
    }

    // ボスがダメージを受けたときの処理
    public void TakeDamage(int damage, int fromDir)
    {
        // 撃破済み、または編集モード中ならダメージを受け付けない
        if (defeated || LevelEditor.IsEditing) return;

        // 攻撃タイミング中なら大きなダメージ倍率、それ以外なら通常の倍率を設定
        float mult = vulnerable ? vulnerableMultiplier : normalMultiplier;

        // ダメージを倍率に応じて計算し、体力が0未満にならないようにする
        hp = Mathf.Max(0f, hp - damage * mult);
        lastHitTime = Time.time;

        // ダメージを受けたときの点滅色を設定
        // 攻撃タイミング中は白、それ以外は少し暗い灰色にする
        flashColor = vulnerable ? Color.white : new Color(0.65f, 0.65f, 0.7f);

        // 0.08秒間、ダメージを受けたときの色を表示する
        flashUntil = Time.time + 0.08f;

        // HPが半分以下になったら、1回だけ雑魚を召喚する（倒された場合は召喚しない）
        if (!zakoSummoned && hp > 0f && hp <= maxHp * zakoSummonHpRatio)
        {
            zakoSummoned = true;
            SummonZako();
        }

        // 体力が0以下になったら、撃破処理を開始する
        if (hp <= 0f) StartCoroutine(DefeatRoutine());
    }

    // 雑魚を召喚する（出現位置はランダム。障害物にめり込まない位置を探して出す）
    void SummonZako()
    {
        if (zakoPrefab == null) return;

        Rect inner = Inner();

        // めり込み判定に使う大きさ（プレハブの当たり判定から取る）
        Vector2 checkSize = Vector2.one;
        var prefabCol = zakoPrefab.GetComponent<Collider2D>();
        if (prefabCol != null) checkSize = (Vector2)prefabCol.bounds.size + new Vector2(0.15f, 0.15f);

        // 障害物として避けるレイヤー（配置ブロック・初期物・ボス）
        int blockMask = LayerMask.GetMask("StickWall", "NotStickWall", "Boss");

        var usedPositions = new System.Collections.Generic.List<Vector3>();

        for (int i = 0; i < zakoSummonCount; i++)
        {
            Vector3 pos = FindZakoSpawnPosition(inner, checkSize, blockMask, usedPositions);
            usedPositions.Add(pos);

            var zako = Instantiate(zakoPrefab, pos, Quaternion.identity);
            summonedZako.Add(zako);

            // Enemyプレハブは配置用にRigidbody2DのY方向が固定されていて、
            // そのままだと召喚しても落ちてこないので、召喚分だけ固定を外す
            // （配置してある雑魚には影響しない）
            var zakoRb = zako.GetComponent<Rigidbody2D>();
            if (zakoRb != null)
                zakoRb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 出現したことが分かるよう、その場に火花を出す
            BossFx.SparkBurst(pos, new Color(1f, 0.45f, 0.35f), 10, 2.8f);
        }
    }

    // 障害物にめり込まず、他の召喚位置やプレイヤーとも近すぎない出現位置を探す
    Vector3 FindZakoSpawnPosition(Rect inner, Vector2 checkSize, int blockMask,
        System.Collections.Generic.List<Vector3> usedPositions)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            // 位置も高さもランダム（落ちた先の床や足場で動き出す）
            Vector3 candidate = new Vector3(
                Random.Range(inner.xMin + 0.6f, inner.xMax - 0.6f),
                Random.Range(inner.yMin + 0.6f, inner.yMax - 0.6f),
                0f);

            // 障害物と重なっていたらやり直し
            if (Physics2D.OverlapBox(candidate, checkSize, 0f, blockMask) != null)
                continue;

            // プレイヤーの真上・真下すぎる位置は避ける
            if (player != null && Vector2.Distance(candidate, player.position) < 1.6f)
                continue;

            // 他の召喚位置と近すぎたらやり直し
            bool tooClose = false;
            foreach (var used in usedPositions)
            {
                if (Vector2.Distance(candidate, used) < 1.4f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            return candidate;
        }

        // どうしても見つからなければ、ボスの位置（ここは空いていることが保証される）
        return transform.position;
    }

    // 召喚した雑魚を全部消す（リトライ時などに残らないように）
    void ClearSummonedZako()
    {
        foreach (var zako in summonedZako)
        {
            if (zako != null) Destroy(zako);
        }
        summonedZako.Clear();
    }

    // ボスが撃破されたときの処理
    IEnumerator DefeatRoutine()
    {
        // 撃破状態にする
        defeated = true;

        // 攻撃タイミング状態を解除
        vulnerable = false;

        // ボスの行動を管理するコルーチンを停止
        if (brain != null) StopCoroutine(brain);

        // 攻撃の前兆表示を消す
        HideTelegraph();

        // 発射済みの弾と衝撃波をすべて削除
        ClearAttacks();

        // ボスの当たり判定を無効にする
        col.enabled = false;

        // ボスの色を灰色に変更
        brainColor = Color.gray;


        // 撃破演出のため、指定した時間だけ待つ
        yield return new WaitForSeconds(defeatDelay);

        // RunControllerが存在する場合、クリア結果を表示する
        if (RunController.Instance != null)
            RunController.Instance.EndRun(true);
    }

    // 編集モードに戻るときや、ゲームをやり直すときにボスの状態を初期状態に戻す
    public void ResetState()
    {
        // 実行中のすべてのコルーチンを停止
        StopAllCoroutines();

        // 発射済みの弾と衝撃波をすべて削除
        ClearAttacks();

        // 攻撃の前兆表示を消す
        HideTelegraph();

        // 撃破状態を解除
        defeated = false;

        // 攻撃タイミング状態を解除
        vulnerable = false;

        // 接触によるダメージ判定と落下状態を解除する
        contactEnabled = false;
        isFalling = false;
        hasLanded = false;

        // 体力を最大値に戻す
        hp = maxHp;

        // 雑魚召喚を未使用に戻し、前回の召喚分が残っていれば消す
        zakoSummoned = false;
        ClearSummonedZako();

        // ボスの当たり判定を有効にする
        col.enabled = true;

        // Rigidbody2Dを通常の制御状態に戻す
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;


        // ダメージによる点滅状態を解除
        flashUntil = 0f;

        // ボスの色を初期色に戻す
        brainColor = baseColor;

        // 回転中だったかどうかの記録をリセット
        wasRotating = false;

        // 回転攻撃の再使用待ち時間をリセット
        nextRotateTime = 0f;

        // 箱の大きさに合わせて、ボスの初期位置を再計算
        RefreshLayout();

        // ボスを初期位置に戻す
        transform.position = startPos;

        // 位置の変更を物理演算に反映
        Physics2D.SyncTransforms();

        // ボスの行動を管理するコルーチンを再開
        brain = StartCoroutine(Brain());

        // 黄色い壁の耐久記録をリセット
        noStickSlamHits.Clear();
        lastLandingCollider = null;

        // 通常攻撃の履歴をリセット
        hasMainAttackHistory = false;
        consecutiveMainAttackCount = 0;
        lastMainAttack = MainAttackType.Shoot;
    }

    // 飛んでいる弾と、走っている衝撃波をすべて削除する
    static void ClearAttacks()
    {
        // シーン内に存在するBossProjectileをすべて取得し、削除する
        foreach (var b in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
            Destroy(b.gameObject);

        // シーン内に存在するBossShockwaveをすべて取得し、削除する
        foreach (var w in FindObjectsByType<BossShockwave>(FindObjectsSortMode.None))
            Destroy(w.gameObject);
    }

    // 落下攻撃のマーカーを消す（旧仕様の落下線は使用しない）
    void HideTelegraph()
    {
        if (marker != null) marker.SetActive(false);
        if (spawnMarker != null) spawnMarker.SetActive(false);
        HideAimVisuals();

        // 衝撃リングも消す
        foreach (var ring in impactRings)
        {
            if (ring.go != null) Destroy(ring.go);
        }
        impactRings.Clear();
    }

    // ボスの行動を繰り返し実行する
    // ボスの行動を繰り返し実行する
    IEnumerator Brain()
    {
        while (!defeated)
        {
            yield return new WaitForSeconds(idleTime);

            // 回転攻撃の抽選
            var arena = ArenaRotator.Instance;

            if (arena != null &&
                !arena.IsRotating &&
                Time.time >= nextRotateTime &&
                Random.value < rotateChance)
            {
                yield return RotateAttack();
            }

            // 別の場所へ移動
            yield return MoveToAnotherWall();

            // プレイヤーとの距離を確認
            bool far = player != null &&
                Vector2.Distance(player.position, transform.position) > farDistance;

            // 今回行う通常攻撃を決定
            MainAttackType nextAttack;

            if (hasMainAttackHistory &&
                consecutiveMainAttackCount >= 2)
            {
                // 同じ攻撃が2回連続していたら、必ず反対の攻撃にする
                nextAttack = lastMainAttack == MainAttackType.Shoot
                    ? MainAttackType.Slam
                    : MainAttackType.Shoot;
            }
            else
            {
                // 通常時はプレイヤーとの距離で攻撃を決める
                nextAttack = far
                    ? MainAttackType.Shoot
                    : MainAttackType.Slam;
            }

            // 選んだ攻撃を実行
            // ダウン（チャンス）は落下攻撃の後だけ入る。遠距離の後はダウンしない
            if (nextAttack == MainAttackType.Shoot)
            {
                yield return ShootAttack();
            }
            else
            {
                yield return SlamAttack();

                // 落下後のダウンは、今の設定値より少し長めにする
                yield return DownTime(downTimeAfterSlam + slamDownExtraTime);
            }

            // 攻撃履歴を更新
            if (hasMainAttackHistory && lastMainAttack == nextAttack)
            {
                consecutiveMainAttackCount++;
            }
            else
            {
                consecutiveMainAttackCount = 1;
            }

            lastMainAttack = nextAttack;
            hasMainAttackHistory = true;
        }
    }

    // 初期オブジェクトまたはプレイヤー設置オブジェクトの上へワープする
    IEnumerator MoveToAnotherWall()
    {
        Vector2 destination;

        // 条件を満たす足場をランダムに選ぶ
        if (!TryChooseRandomPlatformPosition(out destination))
        {
            // 足場が見つからない場合は、床の空いている位置を使う
            destination = FindClearFloorPosition();
        }

        // ワープ先に青いひし形マーカーを表示
        ShowMarker(spawnMarker, destination, new Color(0.15f, 0.65f, 1f, 0.9f));

        // ワープ前の予告（枠は光らせず、マーカーの収束で示す）
        yield return ConvergeMarkerBeforeTeleport();

        // 3秒経過した瞬間にマーカーを消し、同じ位置へボスを移動させる
        if (spawnMarker != null) spawnMarker.SetActive(false);
        SetPos(destination);

        // 通常移動後は床側にいるものとして扱う
        currentWall = Wall.Floor;
    }

    // InitialObjタグの物と、LevelObjectが付いたプレイヤー設置物を集める
    void GetPlatformColliders(System.Collections.Generic.List<Collider2D> result)
    {
        result.Clear();

        var added = new System.Collections.Generic.HashSet<Collider2D>();

        // 初期配置オブジェクト
        GameObject[] initialObjects = GameObject.FindGameObjectsWithTag("InitialObj");
        foreach (GameObject obj in initialObjects)
        {
            if (obj == null) continue;

            Collider2D[] childColliders = obj.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D c in childColliders)
            {
                if (c != null && c.enabled && !c.isTrigger && added.Add(c))
                    result.Add(c);
            }
        }

        // LevelEditorで配置されたオブジェクト
        LevelObject[] placedObjects = FindObjectsByType<LevelObject>(FindObjectsSortMode.None);
        foreach (LevelObject lo in placedObjects)
        {
            if (lo == null || (player != null && lo.gameObject == player.gameObject)) continue;

            Collider2D[] childColliders = lo.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D c in childColliders)
            {
                if (c != null && c.enabled && !c.isTrigger && added.Add(c))
                    result.Add(c);
            }
        }
    }

    // ボスが乗れる幅を持ち、上側に空間がある足場かを確認する
    bool IsValidPlatformPosition(Collider2D platform, Vector2 position)
    {
        if (platform == null) return false;

        Bounds b = platform.bounds;
        float requiredWidth = halfSize.x * 2f + destinationPadding * 2f;

        // ボスの幅より足場が狭い場合は使用しない
        if (b.size.x < requiredWidth) return false;

        // 箱の内側からはみ出す位置は使用しない
        Rect inner = Inner();
        if (position.x - halfSize.x < inner.xMin ||
            position.x + halfSize.x > inner.xMax ||
            position.y - halfSize.y < inner.yMin ||
            position.y + halfSize.y > inner.yMax)
            return false;

        // ボスの占有範囲に別の物体が重ならないか確認する
        Vector2 checkSize = new Vector2(
            halfSize.x * 2f - 0.04f,
            halfSize.y * 2f - 0.04f
        );

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(position, checkSize, 0f);
        foreach (Collider2D hit in overlaps)
        {
            if (hit == null || hit == col || hit == platform) continue;
            if (hit.isTrigger) continue;

            // 足場以外の物体と重なる場所には出現しない
            return false;
        }

        return true;
    }

    // 足場の上からランダムにワープ先を選ぶ
    bool TryChooseRandomPlatformPosition(out Vector2 destination)
    {
        destination = Vector2.zero;

        var platforms = new System.Collections.Generic.List<Collider2D>();
        GetPlatformColliders(platforms);

        var candidates = new System.Collections.Generic.List<Vector2>();

        foreach (Collider2D platform in platforms)
        {
            if (platform == null) continue;

            Bounds b = platform.bounds;
            float minX = b.min.x + halfSize.x + destinationPadding;
            float maxX = b.max.x - halfSize.x - destinationPadding;

            // ボスが十分に乗れない幅なら候補から外す
            if (maxX <= minX) continue;

            // 足場の上面にボスの底面が接する位置
            float x = Random.Range(minX, maxX);
            Vector2 candidate = new Vector2(x, b.max.y + halfSize.y);

            if (IsValidPlatformPosition(platform, candidate))
                candidates.Add(candidate);
        }

        if (candidates.Count == 0) return false;

        destination = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    // 足場が見つからない場合に使う、床上の空いている位置
    Vector2 FindClearFloorPosition()
    {
        Rect inner = Inner();

        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(
                inner.xMin + halfSize.x,
                inner.xMax - halfSize.x
            );

            Vector2 candidate = new Vector2(x, inner.yMin + halfSize.y);

            Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                candidate,
                halfSize * 2f * 0.9f,
                0f
            );

            bool blocked = false;
            foreach (Collider2D hit in overlaps)
            {
                if (hit == null || hit == col || hit.isTrigger) continue;
                blocked = true;
                break;
            }

            if (!blocked) return candidate;
        }

        return SpotOn(Wall.Floor, Random.value);
    }

    // 指定位置にマーカーを表示する
    void ShowMarker(GameObject targetMarker, Vector2 position, Color color)
    {
        if (targetMarker == null) return;

        targetMarker.transform.position = position;
        targetMarker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        targetMarker.transform.localScale = Vector3.one * markerStartScale;
        targetMarker.SetActive(true);

        SpriteRenderer markerRenderer = targetMarker.GetComponent<SpriteRenderer>();
        if (markerRenderer != null) markerRenderer.color = color;
    }

    // ワープ前の予告: 枠は光らせず、マーカーが収束していくことで残り時間を示す
    // マーカーは表示したまま、moveTime秒かけて開始サイズから最終サイズへ縮む
    IEnumerator ConvergeMarkerBeforeTeleport()
    {
        float startTime = Time.time;
        float elapsed = 0f;

        while (elapsed < moveTime)
        {
            elapsed = Time.time - startTime;

            float progress = Mathf.Clamp01(elapsed / Mathf.Max(moveTime, 0.01f));

            if (spawnMarker != null)
                spawnMarker.transform.localScale = Vector3.one
                    * Mathf.Lerp(markerStartScale, markerFinalScale, progress);

            yield return null;
        }

        if (spawnMarker != null)
            spawnMarker.transform.localScale = Vector3.one * markerFinalScale;
    }

    // 落下攻撃:
    // プレイヤーの現在位置の真上へワープし、真下へ落下する。
    // 予告中は赤いマーカーを表示し、3秒後にマーカーを消して落下を開始する。
    IEnumerator SlamAttack()
    {
        if (player == null) yield break;

        Rect inner = Inner();
        Vector2 landing = FindLandingPosition(player.position.x);
        Vector2 spawnPosition = GetFallSpawnPosition(landing, inner);

        // 落下攻撃では着地点マーカーを使わず、ボスの出現位置だけを表示する。
        if (marker != null) marker.SetActive(false);
        ShowMarker(spawnMarker, spawnPosition, new Color(1f, 0.12f, 0.12f, 0.95f));

        float warningStart = Time.time;

        // 予告中はプレイヤーの横位置に追従する。出現高度は最も高い初期障害物の上。
        // 枠は光らせず、マーカーが収束していくことで残り時間を示す
        while (Time.time - warningStart < moveTime)
        {
            if (player != null)
            {
                landing = FindLandingPosition(player.position.x);
                spawnPosition = GetFallSpawnPosition(landing, inner);
                if (spawnMarker != null) spawnMarker.transform.position = spawnPosition;
            }

            float elapsed = Time.time - warningStart;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(moveTime, 0.01f));

            if (spawnMarker != null)
                spawnMarker.transform.localScale = Vector3.one
                    * Mathf.Lerp(markerStartScale, markerFinalScale, progress);

            yield return null;
        }

        if (spawnMarker != null) spawnMarker.SetActive(false);

        // マーカーが示していた位置に出現し、下方向へ落下する。
        SetPos(spawnPosition);

        // 落下中だけ、白ground(StickWall)との衝突を無効にしてすり抜ける
        BeginFallThroughStickWalls();

        // 落下中はプレイヤーとの衝突も無効にする
        // （頭の上に乗って止まり、時間切れでワープするのを防ぐ。
        //   接触ダメージは範囲判定で別に出るので影響しない）
        if (playerCol != null) Physics2D.IgnoreCollision(col, playerCol, true);

        isFalling = true;
        hasLanded = false;
        contactEnabled = true;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(0f, -22f);
        rb.angularVelocity = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.WakeUp();
        Physics2D.SyncTransforms();

        float fallElapsed = 0f;
        const float maxFallTime = 5f;
        while (!hasLanded && fallElapsed < maxFallTime)
        {
            yield return new WaitForFixedUpdate();
            fallElapsed += Time.fixedDeltaTime;
            if (transform.position.y < inner.yMin - halfSize.y) break;
        }

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        isFalling = false;
        contactEnabled = false;

        // 白groundとの衝突を元に戻す
        EndFallThroughStickWalls();

        // プレイヤーとの衝突を元に戻す
        if (playerCol != null) Physics2D.IgnoreCollision(col, playerCol, false);

        // 黄色い壁に着地した場合は耐久処理（1回でヒビ、2回で破壊）
        bool landedWallDestroyed = RegisterNoStickSlamHit(lastLandingCollider);

        if (!hasLanded)
            SetPos(landing);

        // 着地した黄色い壁がこの落下で壊れた場合、
        // 空中に浮いたままにならないよう、その下の足場（なければ床）へ落とす
        if (landedWallDestroyed)
            SetPos(FindLandingPosition(transform.position.x));

        Physics2D.SyncTransforms();
        Vector2 impact = transform.position;
        currentWall = Wall.Floor;

        // 実際に着地した位置から、初期障害物の内側輪郭に沿って衝撃波を流す。
        SpawnShockwaves(impact);
    }

    // プレイヤーのX座標から、真下にある足場の上面を着地点として取得する。
    // 足場がなければ箱の床を着地点にする。
    Vector2 FindLandingPosition(float targetX)
    {
        Rect inner = Inner();

        float x = Mathf.Clamp(
            targetX,
            inner.xMin + halfSize.x,
            inner.xMax - halfSize.x
        );

        Vector2 rayStart = new Vector2(x, inner.yMax - 0.05f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            rayStart,
            Vector2.down,
            inner.height
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // 着地候補がなければ床に着地するものとして記録を消しておく
        lastLandingCollider = null;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                hit.collider == col ||
                hit.collider == playerCol ||
                hit.collider.isTrigger)
                continue;

            // 白ground(StickWall)は落下攻撃を通すので、着地候補にしない
            // ただし初期配置物(InitialObj)は今まで通り着地する
            if (IsFallThroughGround(hit.collider))
                continue;

            // 壁に刺さったナイフも着地候補にしない（落下で砕いて通り抜ける）
            if (IsStuckKnife(hit.collider))
                continue;

            // ボスが乗れないほど狭い足場は着地点にしない。
            Bounds bounds = hit.collider.bounds;
            float requiredWidth = halfSize.x * 2f + destinationPadding * 2f;

            if (bounds.size.x < requiredWidth)
                continue;

            // 着地する足場を記録する（黄色い壁なら耐久処理に使う）
            lastLandingCollider = hit.collider;

            return new Vector2(x, hit.point.y + halfSize.y);
        }

        return new Vector2(x, inner.yMin + halfSize.y);
    }

    // プレイヤーの真上に、最も高い初期障害物より少し上の出現位置を設定する。
    // ボスの頭が障害物や天井にめり込まないよう、ボス自身の半分の高さも加味する。
    Vector2 GetFallSpawnPosition(Vector2 landing, Rect inner)
    {
        float x = Mathf.Clamp(landing.x, inner.xMin + halfSize.x, inner.xMax - halfSize.x);
        float highestObstacleTop = inner.yMin;

        // 出現する縦の列にかかる天井側障害物の下端（これより上には出現しない）
        float ceilingLimit = inner.yMax;

        ArenaObstacleGenerator generator = FindObjectOfType<ArenaObstacleGenerator>();
        if (generator != null)
        {
            Collider2D[] obstacles = generator.GetGeneratedColliders();
            foreach (Collider2D obstacle in obstacles)
            {
                if (obstacle == null || obstacle.isTrigger) continue;
                // 天井側の障害物は落下開始位置の基準にしない。
                // 箱の中央より下にある初期物のうち、最も高い上面を使う。
                if (obstacle.bounds.center.y <= inner.center.y)
                {
                    highestObstacleTop = Mathf.Max(highestObstacleTop, obstacle.bounds.max.y);
                }
                else if (obstacle.bounds.min.x < x + halfSize.x &&
                         obstacle.bounds.max.x > x - halfSize.x)
                {
                    // 出現位置の真上にある天井側障害物にはめり込まないよう、上限をその下端にする
                    ceilingLimit = Mathf.Min(ceilingLimit, obstacle.bounds.min.y);
                }
            }
        }

        const float headClearance = 0.2f;
        float y = highestObstacleTop + halfSize.y + headClearance;

        // 床側の初期物が無い・低いと開始地点が極端に低くなるため、
        // 着地点から最低でも minFallHeight の高さは確保する
        y = Mathf.Max(y, landing.y + minFallHeight);

        float maxY = Mathf.Min(inner.yMax - halfSize.y - 0.05f,
                               ceilingLimit - halfSize.y - 0.05f);
        maxY = Mathf.Max(maxY, inner.yMin + halfSize.y);
        y = Mathf.Clamp(y, inner.yMin + halfSize.y, maxY);
        return new Vector2(x, y);
    }

    // 落下中に床・足場へ着地したかを物理衝突から判定する。
    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckFallLanding(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        CheckFallLanding(collision);
    }

    void CheckFallLanding(Collision2D collision)
    {
        if (!isFalling || collision == null)
            return;

        // 落下中に壁に刺さったナイフへ触れたら、ナイフを砕いて落ち続ける
        // （着地にはしない。足場にしていたナイフは消える）
        if (collision.collider != null && IsStuckKnife(collision.collider))
        {
            Destroy(collision.collider.gameObject);
            return;
        }

        // 白ground(StickWall)には着地しない（落下攻撃は通す）
        // 初期配置物(InitialObj)には着地する
        if (collision.collider != null && IsFallThroughGround(collision.collider))
            return;

        // プレイヤーとの接触は着地として扱わない。
        if (playerCol != null && collision.collider == playerCol)
            return;

        // 上向きの接触法線がある場合、ボスの底面が足場に乗ったと判断する。
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                hasLanded = true;
                return;
            }
        }
    }

    // 落下攻撃が通り抜ける対象かどうか
    // 白ground(StickWall)のうち、初期配置物(InitialObj)ではない物だけを通す
    bool IsFallThroughGround(Collider2D c)
    {
        if (c == null) return false;
        if (c.gameObject.layer != LayerMask.NameToLayer("StickWall")) return false;
        if (c.CompareTag("InitialObj")) return false;
        return true;
    }

    // 壁に刺さったナイフ（足場状態）かどうか
    // 刺さったナイフは PlatformEffector2D を有効化（usedByEffector = true）しているので、それで見分ける
    // 飛行中のナイフはトリガーなので対象外
    bool IsStuckKnife(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (!c.usedByEffector) return false;
        return c.GetComponent<PlatformEffector2D>() != null;
    }

    // 落下中だけ、白ground(StickWall)との衝突を無効にする
    void BeginFallThroughStickWalls()
    {
        fallIgnoredColliders.Clear();

        if (col == null) return;

        foreach (Collider2D c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (c == null || c == col || c.isTrigger) continue;
            if (!IsFallThroughGround(c)) continue;

            Physics2D.IgnoreCollision(col, c, true);
            fallIgnoredColliders.Add(c);
        }
    }

    // BeginFallThroughStickWallsで無効にした衝突を元に戻す
    void EndFallThroughStickWalls()
    {
        if (col != null)
        {
            foreach (Collider2D c in fallIgnoredColliders)
            {
                if (c != null)
                    Physics2D.IgnoreCollision(col, c, false);
            }
        }

        fallIgnoredColliders.Clear();
    }

    // 黄色い壁(NotStickWall)が落下攻撃を受けたときの処理
    // 1回目: ヒビの代わりに色を暗くする / 2回目: その壁を壊す
    // 戻り値: この落下で壁が壊れたら true
    bool RegisterNoStickSlamHit(Collider2D platform)
    {
        if (platform == null) return false;
        if (platform.gameObject.layer != LayerMask.NameToLayer("NotStickWall")) return false;

        int count = 0;
        noStickSlamHits.TryGetValue(platform, out count);
        count++;
        noStickSlamHits[platform] = count;

        if (count >= 2)
        {
            noStickSlamHits.Remove(platform);
            Destroy(platform.gameObject);
            return true;
        }

        // 1回目: ヒビが入ったことが分かるように暗くする
        SpriteRenderer wallSr = platform.GetComponentInChildren<SpriteRenderer>();
        if (wallSr != null)
            wallSr.color = new Color(0.65f, 0.5f, 0.1f);

        return false;
    }

    // 着地の衝撃リング（広がって消える輪）
    private class ImpactRing
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float start;
    }

    private readonly List<ImpactRing> impactRings = new List<ImpactRing>();

    // 着地点に衝撃リングを出す
    void SpawnImpactRing(Vector2 pos)
    {
        var go = new GameObject("ImpactRing");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BossFx.RingSprite;
        sr.color = new Color(1f, 0.6f, 0.25f, 0.85f);
        sr.sortingOrder = 8;

        impactRings.Add(new ImpactRing { go = go, sr = sr, start = Time.time });
    }

    // 衝撃リングを広げて薄くし、終わったら消す
    void UpdateImpactRings()
    {
        const float duration = 0.38f;
        const float maxDiameter = 5.2f;

        for (int i = impactRings.Count - 1; i >= 0; i--)
        {
            var ring = impactRings[i];
            if (ring.go == null) { impactRings.RemoveAt(i); continue; }

            float progress = (Time.time - ring.start) / duration;
            if (progress >= 1f)
            {
                Destroy(ring.go);
                impactRings.RemoveAt(i);
                continue;
            }

            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            ring.go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxDiameter, eased);

            var c = ring.sr.color;
            c.a = 0.85f * (1f - progress);
            ring.sr.color = c;
        }
    }

    // 衝撃波：初期障害物の内側にできた凹凸の輪郭を取得し、着地点から両方向へ走らせる。
    void SpawnShockwaves(Vector2 impact)
    {
        List<Vector2> path = BuildInitialObstacleContour();
        if (path == null || path.Count < 4)
        {
            Debug.LogWarning("衝撃波の輪郭を作れなかったため、衝撃波を生成できませんでした。");
            return;
        }

        float startDistance = BossShockwave.FindNearestDistance(path, impact);
        float halfLength = BossShockwave.PathLength(path) * 0.5f;
        Color color = new Color(1f, 0.35f, 0.2f, 0.9f);

        BossShockwave.Spawn(path, startDistance, +1, waveSpeed, halfLength, waveRadius,
            waveDamage, playerCol, playerHealth, color);
        BossShockwave.Spawn(path, startDistance, -1, waveSpeed, halfLength, waveRadius,
            waveDamage, playerCol, playerHealth, color);
    }


    // 初期障害物の内側輪郭を作る。
    // 障害物の角と枠の内側をつないで、連続した凹凸経路にする。
    List<Vector2> BuildInitialObstacleContour()
    {
        Rect inner = Inner();

        ArenaObstacleGenerator generator =
            FindObjectOfType<ArenaObstacleGenerator>();

        Collider2D[] obstacles = generator != null
            ? generator.GetGeneratedColliders()
            : new Collider2D[0];

        // 0=床、1=右、2=天井、3=左
        List<Collider2D>[] sides = new List<Collider2D>[4];

        for (int i = 0; i < 4; i++)
            sides[i] = new List<Collider2D>();

        // 障害物を、それぞれ最も近い壁に分類する
        foreach (Collider2D obstacle in obstacles)
        {
            if (obstacle == null || !obstacle.enabled || obstacle.isTrigger)
                continue;

            Bounds b = obstacle.bounds;

            float[] distances =
            {
            Mathf.Abs(b.min.y - inner.yMin),
            Mathf.Abs(inner.xMax - b.max.x),
            Mathf.Abs(inner.yMax - b.max.y),
            Mathf.Abs(b.min.x - inner.xMin)
        };

            int nearestSide = 0;

            for (int i = 1; i < 4; i++)
            {
                if (distances[i] < distances[nearestSide])
                    nearestSide = i;
            }

            sides[nearestSide].Add(obstacle);
        }

        List<Vector2> points = new List<Vector2>();

        float inset = waveInset;

        float left = inner.xMin + inset;
        float right = inner.xMax - inset;
        float bottom = inner.yMin + inset;
        float top = inner.yMax - inset;

        // 床：左から右
        AddContourPoint(points, new Vector2(left, bottom));

        sides[0].Sort((a, b) =>
            a.bounds.min.x.CompareTo(b.bounds.min.x));

        foreach (Collider2D obstacle in sides[0])
        {
            Bounds b = obstacle.bounds;

            float x1 = Mathf.Clamp(b.min.x, left, right);
            float x2 = Mathf.Clamp(b.max.x, left, right);
            float y = Mathf.Min(b.max.y + inset, top);

            AddContourPoint(points, new Vector2(x1, bottom));
            AddContourPoint(points, new Vector2(x1, y));
            AddContourPoint(points, new Vector2(x2, y));
            AddContourPoint(points, new Vector2(x2, bottom));
        }

        AddContourPoint(points, new Vector2(right, bottom));

        // 右壁：下から上
        sides[1].Sort((a, b) =>
            a.bounds.min.y.CompareTo(b.bounds.min.y));

        foreach (Collider2D obstacle in sides[1])
        {
            Bounds b = obstacle.bounds;

            float y1 = Mathf.Clamp(b.min.y, bottom, top);
            float y2 = Mathf.Clamp(b.max.y, bottom, top);
            float x = Mathf.Max(b.min.x - inset, left);

            AddContourPoint(points, new Vector2(right, y1));
            AddContourPoint(points, new Vector2(x, y1));
            AddContourPoint(points, new Vector2(x, y2));
            AddContourPoint(points, new Vector2(right, y2));
        }

        AddContourPoint(points, new Vector2(right, top));

        // 天井：右から左
        sides[2].Sort((a, b) =>
            b.bounds.max.x.CompareTo(a.bounds.max.x));

        foreach (Collider2D obstacle in sides[2])
        {
            Bounds b = obstacle.bounds;

            float x1 = Mathf.Clamp(b.max.x, left, right);
            float x2 = Mathf.Clamp(b.min.x, left, right);
            float y = Mathf.Max(b.min.y - inset, bottom);

            AddContourPoint(points, new Vector2(x1, top));
            AddContourPoint(points, new Vector2(x1, y));
            AddContourPoint(points, new Vector2(x2, y));
            AddContourPoint(points, new Vector2(x2, top));
        }

        AddContourPoint(points, new Vector2(left, top));

        // 左壁：上から下
        sides[3].Sort((a, b) =>
            b.bounds.max.y.CompareTo(a.bounds.max.y));

        foreach (Collider2D obstacle in sides[3])
        {
            Bounds b = obstacle.bounds;

            float y1 = Mathf.Clamp(b.max.y, bottom, top);
            float y2 = Mathf.Clamp(b.min.y, bottom, top);
            float x = Mathf.Min(b.max.x + inset, right);

            AddContourPoint(points, new Vector2(left, y1));
            AddContourPoint(points, new Vector2(x, y1));
            AddContourPoint(points, new Vector2(x, y2));
            AddContourPoint(points, new Vector2(left, y2));
        }

        return points;
    }

    // 同じ位置の点を重複登録しない
    void AddContourPoint(List<Vector2> points, Vector2 point)
    {
        if (points.Count == 0 ||
            Vector2.Distance(points[points.Count - 1], point) > 0.02f)
        {
            points.Add(point);
        }
    }


    // 遠距離攻撃:
    // 前兆としてボスをオレンジ色に点滅させたあと、プレイヤーの方向へ弾を連射する。
    IEnumerator ShootAttack()
    {
        // プレイヤーが存在しない場合は、攻撃を行わず終了する
        if (player == null) yield break;


        //  攻撃前の予兆：赤い円がボスへ収束し、同時にプレイヤーへの照準線を描く
        // 枠は光らせない
        EnsureAimVisuals();
        if (aimLineObject != null) aimLineObject.SetActive(true);
        if (aimRingObject != null) aimRingObject.SetActive(true);

        float t = 0f;

        // 設定された予兆時間が経過するまで繰り返す
        while (t < shootTelegraph)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            float progress = Mathf.Clamp01(t / Mathf.Max(shootTelegraph, 0.01f));

            // 赤い円がボスを中心に収束していく
            UpdateAimRing(Mathf.Lerp(aimRingStartRadius, aimRingEndRadius, progress));

            // 照準線はプレイヤーを追い続ける
            if (aimLine != null && player != null)
            {
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, player.position);
            }

            // 次のフレームまで待つ
            yield return null;
        }

        // 予兆が終わったら円と照準線を消して、発射に入る
        HideAimVisuals();

        //  弾の連射 
        // 設定された弾数だけ、繰り返し弾を発射する
        for (int i = 0; i < shotCount; i++)
        {
            // ボスの現在位置を弾の発射位置にする
            Vector2 origin = transform.position;

            // ボスからプレイヤーに向かう方向を計算して、正規化する
            // 正規化することで、方向だけを表す長さ1のベクトルになる
            Vector2 dir = ((Vector2)player.position - origin).normalized;

            // プレイヤーの方向へ弾を生成する
            // bulletSpeedで弾の速度、bulletDamageでダメージを設定する
            // blockLayersに含まれる壁や足場に当たると弾が止まる
            BossProjectile.Spawn(origin, dir * bulletSpeed, bulletDamage, blockLayers,
                playerCol, playerHealth, new Color(1f, 0.55f, 0.1f));

            // 次の弾を発射するまで、設定された時間だけ待つ
            yield return new WaitForSeconds(shotInterval);
        }
    }

    // 攻撃タイミング(ダウン):
    // ボスが黄色く点滅する。
    // この間はプレイヤーの攻撃によるダメージが大きくなる。
    IEnumerator DownTime(float duration)
    {
        // 攻撃タイミング中であることを記録する
        // TakeDamage()で、この状態かどうかによってダメージ倍率が変わる
        vulnerable = true;


        // 経過時間を初期化する
        float t = 0f;

        // 指定された時間が経過するまで繰り返す
        while (t < duration)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            // ボスを黄色と通常色で交互に点滅させる
            // Time.timeを使って、一定間隔で色を切り替える
            brainColor = ((int)(Time.time * 8f) % 2 == 0)
                ? new Color(1f, 0.9f, 0.2f)
                : baseColor;

            // 次のフレームまで待つ
            yield return null;
        }

        // 攻撃タイミングが終了したので、通常状態に戻す
        vulnerable = false;

        // ボスの色を元に戻す
        brainColor = baseColor;

    }


    IEnumerator PlayRotationLine(ArenaRotator.Direction dir)
    {
        // 二重枠のちょうど中間にチャージ線を配置する
        Rect inner = Inner();
        float frameHalfSize = 15.75f;

        Rect r = new Rect(
            inner.center.x - frameHalfSize,
            inner.center.y - frameHalfSize,
            frameHalfSize * 2f,
            frameHalfSize * 2f
        );

        // 上辺中央をスタート地点にする
        Vector3 topCenter = new Vector3(r.center.x, r.yMax, 0f);
        Vector3 topRight = new Vector3(r.xMax, r.yMax, 0f);
        Vector3 bottomRight = new Vector3(r.xMax, r.yMin, 0f);
        Vector3 bottomLeft = new Vector3(r.xMin, r.yMin, 0f);
        Vector3 topLeft = new Vector3(r.xMin, r.yMax, 0f);

        // 回転方向によって線を伸ばす順番を変更
        Vector3[] path;

        if (dir == ArenaRotator.Direction.Clockwise)
        {
            path = new Vector3[]
            {
            topCenter,
            topRight,
            bottomRight,
            bottomLeft,
            topLeft,
            topCenter
            };
        }
        else
        {
            path = new Vector3[]
            {
            topCenter,
            topLeft,
            bottomLeft,
            bottomRight,
            topRight,
            topCenter
            };
        }

        // 線を作成
        rotationLineObject = new GameObject("RotationLine");
        rotationLine = rotationLineObject.AddComponent<LineRenderer>();

        rotationLine.useWorldSpace = true;
        rotationLine.startWidth = rotationLineWidth;
        rotationLine.endWidth = rotationLineWidth;
        rotationLine.startColor = rotationLineColor;
        rotationLine.endColor = rotationLineColor;

        rotationLine.material = new Material(Shader.Find("Sprites/Default"));
        rotationLine.sortingOrder = 20;

        rotationLine.positionCount = 1;
        rotationLine.SetPosition(0, topCenter);

        // 線全体の長さを計算
        float totalLength = 0f;

        for (int i = 0; i < path.Length - 1; i++)
        {
            totalLength += Vector3.Distance(path[i], path[i + 1]);
        }

        float elapsed = 0f;

        // 予告時間を使って線を伸ばす
        while (elapsed < rotateTelegraph)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / rotateTelegraph);
            float currentLength = totalLength * progress;

            float remaining = currentLength;

            List<Vector3> points = new List<Vector3>();
            points.Add(path[0]);

            for (int i = 0; i < path.Length - 1; i++)
            {
                float segmentLength =
                    Vector3.Distance(path[i], path[i + 1]);

                if (remaining >= segmentLength)
                {
                    points.Add(path[i + 1]);
                    remaining -= segmentLength;
                }
                else
                {
                    Vector3 currentPoint = Vector3.Lerp(
                        path[i],
                        path[i + 1],
                        remaining / segmentLength
                    );

                    points.Add(currentPoint);
                    break;
                }
            }

            rotationLine.positionCount = points.Count;
            rotationLine.SetPositions(points.ToArray());

            yield return null;
        }

        // 最後に一周分の線を確実に完成させる
        rotationLine.positionCount = path.Length;
        rotationLine.SetPositions(path);
    }

    // 回転攻撃:
    // 前兆が終わると、箱全体を90度回転させる。
    IEnumerator RotateAttack()
    {
        var arena = ArenaRotator.Instance;

        if (arena == null || arena.IsRotating)
            yield break;

        // 回転方向をランダムに決定
        var dir = Random.value < 0.5f
            ? ArenaRotator.Direction.Clockwise
            : ArenaRotator.Direction.CounterClockwise;


        // ボスの予告演出
        Coroutine edgeEffect = StartCoroutine(
            PlayRotationLine(dir)
        );

        // 予告中は枠を光らせない（回転方向は線の描画だけで示す）
        float t = 0f;

        while (t < rotateTelegraph)
        {
            t += Time.deltaTime;

            yield return null;
        }

        // 線の描画が完了するまで待つ
        if (edgeEffect != null)
            yield return edgeEffect;

        // 箱を回転させる
        arena.TryRotate(dir);

        // 箱の回転が終わるまで線を残す
        while (arena.IsRotating)
        {
            yield return null;
        }

        // 回転終了後に線を消す
        if (rotationLineObject != null)
        {
            Destroy(rotationLineObject);
            rotationLineObject = null;
            rotationLine = null;
        }
    }

    // ボスとプレイヤーのめり込み解消
    // ボスはKinematic＋座標の直接指定で動くため、ワープや着地の行き先が
    // プレイヤーと重なると物理演算では押し戻されず、重なったままになる。
    // 落下中以外で重なっていたら、めり込んだ分だけプレイヤーを外へ押し出す。
    void ResolvePlayerOverlap()
    {
        // 落下中は接触ダメージを優先するので押し出さない
        if (isFalling) return;
        if (col == null || playerCol == null) return;

        // 2つのColliderの距離情報（重なっている場合は isOverlapped が true になる）
        ColliderDistance2D d = Physics2D.Distance(col, playerCol);
        if (!d.isOverlapped) return;

        // めり込んだ深さ（-d.distance）＋少しの余白だけ、ボスから離れる方向へ押し出す
        Vector2 push = d.normal * (-d.distance + 0.02f);

        var prb = playerCol.attachedRigidbody;
        if (prb != null)
            prb.position += push;
        else if (player != null)
            player.position += (Vector3)push;

        Physics2D.SyncTransforms();
    }

    //  接触ダメージ(落下中だけ)
    // ボスとプレイヤーが接触しているかを確認し、
    // 落下中に接触していた場合だけプレイヤーにダメージを与える。
    void CheckContact()
    {
        // 接触ダメージが無効、またはプレイヤーのColliderや体力情報がない場合は処理を終了する
        if (!contactEnabled || playerCol == null || playerHealth == null) return;

        // ボスとプレイヤーの当たり判定が重なっていなければ処理を終了する
        if (!col.bounds.Intersects(playerCol.bounds)) return;

        // プレイヤーがボスより右側にいる場合は1、左側にいる場合は-1にする
        // ダメージを受けたときの吹き飛ぶ方向を決めるために使用する
        int fromDir = player.position.x >= transform.position.x ? 1 : -1;

        // プレイヤーに落下攻撃のダメージを与える
        // 無敵時間の管理はPlayerHealth側で行う
        playerHealth.TakeDamage(diveDamage, fromDir);
    }

    // 落下攻撃の着地点を示すマーカーを作成する
    // 遠距離攻撃の予告用（照準線と、収束する赤い円）を用意する
    void EnsureAimVisuals()
    {
        if (aimLineObject == null)
        {
            aimLineObject = new GameObject("AimLine");
            aimLine = aimLineObject.AddComponent<LineRenderer>();
            aimLine.useWorldSpace = true;
            aimLine.startWidth = 0.05f;
            aimLine.endWidth = 0.05f;
            aimLine.startColor = new Color(1f, 0.2f, 0.2f, 0.9f);
            aimLine.endColor = new Color(1f, 0.2f, 0.2f, 0.9f);
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.sortingOrder = 20;
            aimLine.positionCount = 2;
            aimLineObject.SetActive(false);
        }

        if (aimRingObject == null)
        {
            aimRingObject = new GameObject("AimRing");
            aimRing = aimRingObject.AddComponent<LineRenderer>();
            aimRing.useWorldSpace = true;
            aimRing.startWidth = 0.06f;
            aimRing.endWidth = 0.06f;
            aimRing.startColor = new Color(1f, 0.15f, 0.15f, 0.95f);
            aimRing.endColor = new Color(1f, 0.15f, 0.15f, 0.95f);
            aimRing.material = new Material(Shader.Find("Sprites/Default"));
            aimRing.sortingOrder = 20;
            aimRing.loop = true;
            aimRing.positionCount = 48;
            aimRingObject.SetActive(false);
        }
    }

    // 収束する赤い円を、ボスを中心に描く
    void UpdateAimRing(float radius)
    {
        if (aimRing == null) return;

        Vector3 center = transform.position;
        for (int i = 0; i < 48; i++)
        {
            float a = (i / 48f) * Mathf.PI * 2f;
            aimRing.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
    }

    // 照準線と赤い円を消す
    void HideAimVisuals()
    {
        if (aimLineObject != null) aimLineObject.SetActive(false);
        if (aimRingObject != null) aimRingObject.SetActive(false);
    }

    GameObject MakeMarker(string objectName)
    {
        // マーカー用のGameObjectを作成する
        var go = new GameObject(objectName);

        // マーカーにSpriteRendererを追加する
        var msr = go.AddComponent<SpriteRenderer>();

        // BossShockwaveで使用している四角形のスプライトを設定する
        msr.sprite = BossShockwave.SquareSprite;

        // 描画順を設定する(数字が大きいほど手前に表示される)
        msr.sortingOrder = 4;

        // 正方形を45度回転させ、ひし形にする（大きさは収束の開始サイズ）
        go.transform.localScale = Vector3.one * markerStartScale;
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

        // 作成直後はマーカーを非表示にしておく
        go.SetActive(false);

        // 作成したマーカーのGameObjectを返す
        return go;
    }

    //  画面にボスの情報を表示する 
    void OnGUI()
    {
        // レベル編集画面、またはリザルト画面では表示しない
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // ボスの体力バー
        float w = 20f;       // 横幅
        float h = 250f;      // 高さ
        float x = 30f;       // 画面左からの位置
        float y = 150f;      // 画面上からの位置

        // 現在のHP割合
        float hpRate = Mathf.Clamp01(hp / maxHp);

        // ダメージを受けた直後かどうか
        bool isFlashing = Time.time - lastHitTime < hitFlashDuration;

        // 点滅の切り替え
        bool flashOn = isFlashing &&
            Mathf.FloorToInt(Time.time * 12f) % 2 == 0;

        // 枠の色
        GUI.color = flashOn ? Color.white : new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(
            new Rect(x - 3f, y - 3f, w + 6f, h + 6f),
            Texture2D.whiteTexture
        );

        // 背景
        GUI.color = Color.black;
        GUI.DrawTexture(
            new Rect(x, y, w, h),
            Texture2D.whiteTexture
        );

        // HPの残量（下から上に表示）
        GUI.color = flashOn ? Color.white : new Color(0.85f, 0.2f, 0.25f);

        float fillHeight = h * hpRate;

        GUI.DrawTexture(
            new Rect(x, y + h - fillHeight, w, fillHeight),
            Texture2D.whiteTexture
        );

        // GUIの色を白に戻す
        GUI.color = Color.white;

        // ボスの名前と現在の体力
        GUI.Label(
            new Rect(x - 10f, y - 30f, 100f, 24f),
            $"フレーム {Mathf.CeilToInt(hp)}/{maxHp}"
        );

    }
}
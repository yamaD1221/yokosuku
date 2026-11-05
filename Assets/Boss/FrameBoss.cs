using System.Collections;
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
    [SerializeField] private float moveTime = 0.4f;                // 別の壁へ移動する時間

    // 初期障害物への着地
    [Header("初期障害物への着地")]
    [SerializeField] private float initialObjectSearchDistance = 4f; // 着地できる初期障害物を探す距離

    // 攻撃の選び方
    [Header("攻撃の選び方")]
    [SerializeField] private float farDistance = 9f;              // プレイヤーがこれより遠いと遠距離攻撃を選ぶ
    [SerializeField, Range(0f, 1f)] private float rotateChance = 0.25f; // 行動サイクルの最初に回転攻撃を行う確率
    [SerializeField] private float rotateCooldown = 8f;           // 回転攻撃後、次に回転攻撃を選べるまでの時間

    // 落下攻撃
    [Header("落下攻撃")]
    [SerializeField] private float slamTelegraph = 0.9f;          // 落下攻撃の前兆時間
    [SerializeField] private float slamLockTime = 0.3f;            // 着地点を固定する最後の時間
    [SerializeField] private float diveTime = 0.35f;               // ボスが落下する時間
    [SerializeField] private int diveDamage = 1;                   // 落下中にボス本体へ触れたときのダメージ
    [SerializeField] private float downTimeAfterSlam = 3f;         // 落下攻撃後の攻撃チャンスの時間

    // 衝撃波(箱の壁に沿って、両側へ走る)
    [Header("衝撃波(箱の壁に沿って、両側へ走る)")]
    [SerializeField] private float waveSpeed = 10f;                // 衝撃波が進む速度
    [SerializeField] private float waveRadius = 0.9f;               // 衝撃波の当たり判定の大きさ
    [SerializeField] private float waveInset = 0.45f;               // 壁から衝撃波の中心までの距離
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

    // 回転攻撃
    [Header("回転攻撃")]
    [SerializeField] private float rotateTelegraph = 1.2f;          // 箱を回転させる前の予兆時間

    // 撃破
    [Header("撃破")]
    [SerializeField] private float defeatDelay = 0.8f;              // 撃破演出後、結果画面を出すまでの時間

    [SerializeField] private Material lineMaterial;                 // 落下攻撃の前兆に使う線の素材

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

    // ボスの当たり判定
    private Collider2D col;

    // ボスの画像を表示するコンポーネント
    private SpriteRenderer sr;

    // ボスの通常時の色
    private Color baseColor = Color.white;

    // ボスの現在の表示色
    private Color brainColor = Color.white;

    // ダメージを受けたときの点滅終了時刻
    private float flashUntil;

    // ダメージを受けたときに表示する色
    private Color flashColor = Color.white;

    // 現在画面に表示する攻撃の説明
    private string label;

    // 落下攻撃の前兆を表示する線
    private LineRenderer slamLine;

    // 落下攻撃の着地点を表示するマーカー
    private GameObject marker;

    // プレイヤーのTransform
    private Transform player;

    // プレイヤーの当たり判定
    private Collider2D playerCol;

    // プレイヤーの体力を管理するコンポーネント
    private PlayerHealth playerHealth;

    // ボスの行動を管理するコルーチン
    private Coroutine brain;

    // 画面に攻撃名などを表示するための文字設定
    private GUIStyle bigStyle;

    // 前のフレームで箱が回転していたかどうか
    private bool wasRotating;

    // 次に回転攻撃を選択できる時刻
    private float nextRotateTime;

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

        // Rigidbody2Dを取得
        var rb = GetComponent<Rigidbody2D>();

        // Rigidbody2Dがなければ追加する
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        // 物理演算による移動を行わず、スクリプトで位置を制御する
        rb.bodyType = RigidbodyType2D.Kinematic;

        // ボスの初期位置を保存
        startPos = transform.position;

        // ボスの体力を最大体力に設定
        hp = maxHp;

        // 落下攻撃の前兆を表示する線を作成
        slamLine = MakeLine();

        // 落下攻撃の着地点を表示するマーカーを作成
        marker = MakeMarker();
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
        // 着地点を表示するマーカーが残っていれば削除
        if (marker != null) Destroy(marker);

        // このボスが現在のInstanceなら、参照を解除
        if (Instance == this) Instance = null;
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
    }

    // 毎フレームの最後に、ボスの色を更新する
    void LateUpdate()
    {
        // SpriteRendererが存在しなければ処理を終了
        if (sr == null) return;

        // ダメージを受けたときの点滅時間内なら点滅用の色にする
        // それ以外の場合は、通常の表示色にする
        sr.color = Time.time < flashUntil ? flashColor : brainColor;
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

        // ダメージを受けたときの点滅色を設定
        // 攻撃タイミング中は白、それ以外は少し暗い灰色にする
        flashColor = vulnerable ? Color.white : new Color(0.65f, 0.65f, 0.7f);

        // 0.08秒間、ダメージを受けたときの色を表示する
        flashUntil = Time.time + 0.08f;

        // 体力が0以下になったら、撃破処理を開始する
        if (hp <= 0f) StartCoroutine(DefeatRoutine());
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

        // 画面に撃破メッセージを表示
        label = "撃破!";

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

        // 接触によるダメージ判定を無効にする
        contactEnabled = false;

        // 体力を最大値に戻す
        hp = maxHp;

        // ボスの当たり判定を有効にする
        col.enabled = true;

        // 画面に表示する攻撃メッセージを消す
        label = null;

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

    // 落下攻撃の前兆表示を消す
    void HideTelegraph()
    {
        // 前兆を表示する線が存在すれば、非表示にする
        if (slamLine != null) slamLine.enabled = false;

        // 着地点を示すマーカーが存在すれば、非表示にする
        if (marker != null) marker.SetActive(false);
    }

    // ボスの行動を繰り返し実行する
    IEnumerator Brain()
    {
        // ボスが撃破されるまで行動を繰り返す
        while (!defeated)
        {
            // 次の行動を開始するまで指定した時間だけ待つ
            yield return new WaitForSeconds(idleTime);

            // ときどき回転攻撃を行う
            var arena = ArenaRotator.Instance;

            // 箱が回転中でなく、回転攻撃の待ち時間が終わっていて、
            // 設定した確率に当たった場合、回転攻撃を行う
            if (arena != null && !arena.IsRotating && Time.time >= nextRotateTime && Random.value < rotateChance)
                yield return RotateAttack();

            // 現在いる壁とは別の壁へ移動する
            yield return MoveToAnotherWall();

            // プレイヤーとの距離を調べ、遠いかどうかを判定する
            bool far = player != null && Vector2.Distance(player.position, transform.position) > farDistance;

            // プレイヤーが遠い場合
            if (far)
            {
                // 遠距離攻撃を行う
                yield return ShootAttack();

                // 攻撃後、指定した時間だけ攻撃タイミングにする
                yield return DownTime(downTimeAfterShoot);
            }
            // プレイヤーが近い場合、またはプレイヤーが見つからない場合
            else
            {
                // 落下攻撃を行う
                yield return SlamAttack();

                // 攻撃後、指定した時間だけ攻撃タイミングにする
                yield return DownTime(downTimeAfterSlam);
            }
        }
    }

    // 今いる壁とは別の壁を選び、そこまで滑らかに移動する
    // 移動中は接触してもプレイヤーにダメージを与えない
    IEnumerator MoveToAnotherWall()
    {
        // 現在いる壁とは違う壁をランダムに選ぶ
        Wall next;
        do
        {
            next = (Wall)Random.Range(0, 4);
        }
        while (next == currentWall);

        // 移動開始時のボスの位置を取得
        Vector2 from = transform.position;

        // 移動先の壁のランダムな位置を取得
        Vector2 to = SpotOn(next, Random.value);

        // 移動先に初期障害物がある場合、障害物に乗れる位置に変更
        if (TryGetInitialObjectLandingPosition(next, to, out Vector2 landing))
        {
            to = landing;
        }

        // 移動にかかった時間を初期化
        float t = 0f;

        // 指定した移動時間が経過するまで移動を続ける
        while (t < moveTime)
        {
            // 経過時間を加算
            t += Time.deltaTime;

            // 移動の進み具合を0～1の範囲で計算
            // SmoothStepによって、移動開始と終了を滑らかにする
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveTime));

            // 開始位置から移動先まで、滑らかに移動させる
            SetPos(Vector2.Lerp(from, to, k));

            // 次のフレームまで待つ
            yield return null;
        }

        // 最後に移動先へ正確に配置する
        SetPos(to);

        // 現在いる壁を移動先の壁に更新
        currentWall = next;
    }

    // 初期障害物の上にボスが着地できる位置を探す
    // targetWall : ボスが移動する先の壁
    // intendedPosition : 通常の着地予定位置
    // landingPosition : 実際に着地する位置
    // 戻り値 : 着地できる障害物が見つかったかどうか
    private bool TryGetInitialObjectLandingPosition(
        Wall targetWall,
        Vector2 intendedPosition,
        out Vector2 landingPosition)
    {
        // 最初は通常の着地予定位置を設定しておく
        landingPosition = intendedPosition;

        // InitialObjタグが付いている障害物をすべて取得する
        GameObject[] objects = GameObject.FindGameObjectsWithTag("InitialObj");

        // 箱の内側の範囲を取得する
        Rect inner = Inner();

        // 探索距離の初期値を設定する
        float nearestDistance = initialObjectSearchDistance;

        // 条件に合う障害物が見つかったかどうか
        bool found = false;

        // 取得した障害物を1つずつ調べる
        foreach (GameObject obj in objects)
        {
            // オブジェクトが存在しない場合は次へ進む
            if (obj == null)
                continue;

            // 障害物のCollider2Dを取得する
            Collider2D col = obj.GetComponent<Collider2D>();

            // Collider2Dが付いていない場合は対象外にする
            if (col == null)
                continue;

            // 障害物の位置と大きさを取得する
            Bounds bounds = col.bounds;

            // 着地候補の位置を通常の着地予定位置にする
            Vector2 candidate = intendedPosition;

            // 移動先の壁によって、障害物の着地位置を計算する
            switch (targetWall)
            {
                case Wall.Floor:
                    // 床側にある障害物だけを対象にする
                    if (bounds.center.y >= inner.center.y)
                        continue;

                    // ボスが乗れるだけの横幅がない障害物は対象外
                    if (bounds.size.x < halfSize.x * 2f)
                        continue;

                    // ボスの中心が障害物の横幅からはみ出さないようにする
                    candidate.x = Mathf.Clamp(
                        intendedPosition.x,
                        bounds.min.x + halfSize.x,
                        bounds.max.x - halfSize.x
                    );

                    // 障害物の上面にボスの底面が接する位置にする
                    candidate.y = bounds.max.y + halfSize.y;
                    break;

                case Wall.Ceiling:
                    // 天井側にある障害物だけを対象にする
                    if (bounds.center.y <= inner.center.y)
                        continue;

                    // ボスが乗れるだけの横幅がない障害物は対象外
                    if (bounds.size.x < halfSize.x * 2f)
                        continue;

                    // ボスの中心が障害物の横幅からはみ出さないようにする
                    candidate.x = Mathf.Clamp(
                        intendedPosition.x,
                        bounds.min.x + halfSize.x,
                        bounds.max.x - halfSize.x
                    );

                    // 障害物の下面にボスの上面が接する位置にする
                    candidate.y = bounds.min.y - halfSize.y;
                    break;

                case Wall.Left:
                    // 左側にある障害物だけを対象にする
                    if (bounds.center.x >= inner.center.x)
                        continue;

                    // ボスが乗れるだけの高さがない障害物は対象外
                    if (bounds.size.y < halfSize.y * 2f)
                        continue;

                    // 障害物の右側にボスが接する位置にする
                    candidate.x = bounds.max.x + halfSize.x;

                    // ボスの中心が障害物の高さからはみ出さないようにする
                    candidate.y = Mathf.Clamp(
                        intendedPosition.y,
                        bounds.min.y + halfSize.y,
                        bounds.max.y - halfSize.y
                    );
                    break;

                case Wall.Right:
                    // 右側にある障害物だけを対象にする
                    if (bounds.center.x <= inner.center.x)
                        continue;

                    // ボスが乗れるだけの高さがない障害物は対象外
                    if (bounds.size.y < halfSize.y * 2f)
                        continue;

                    // 障害物の左側にボスが接する位置にする
                    candidate.x = bounds.min.x - halfSize.x;

                    // ボスの中心が障害物の高さからはみ出さないようにする
                    candidate.y = Mathf.Clamp(
                        intendedPosition.y,
                        bounds.min.y + halfSize.y,
                        bounds.max.y - halfSize.y
                    );
                    break;
            }

            // 通常の着地予定位置から候補位置までの距離を計算する
            float distance = Vector2.Distance(
                intendedPosition,
                candidate
            );

            // 探索範囲内で、これまでより近い障害物が見つかった場合
            if (distance <= nearestDistance)
            {
                // 最も近い障害物までの距離を更新する
                nearestDistance = distance;

                // 着地位置を今回の候補位置に更新する
                landingPosition = candidate;

                // 条件に合う障害物が見つかったことを記録する
                found = true;
            }
        }

        // 条件に合う障害物が見つかったかどうかを返す
        return found;
    }

    // 落下攻撃:
    // 向かい側の壁に向かって、前兆(赤く点滅・着地点の印・線)を表示する。
    // 前兆が終わるとボスが落下し、着地した場所から衝撃波を発生させる。
    IEnumerator SlamAttack()
    {
        // プレイヤーが存在しない場合は、落下攻撃を行わず終了する
        if (player == null) yield break;

        // 現在いる壁の反対側を、落下先の壁にする
        Wall target = Opposite(currentWall);

        // プレイヤーの位置を、落下先の壁に沿った位置(0～1)に変換する
        // この値を使って、プレイヤーのいる位置を狙う
        float lockedU = ProjectU(target, player.position);

        // ボスの状態表示を「落下!」にする
        label = "落下!";

        // 着地点を示す線とマーカーを表示する
        slamLine.enabled = true;
        marker.SetActive(true);

        //  落下前の予兆 
        float t = 0f;

        // 設定された予兆時間が経過するまで繰り返す
        while (t < slamTelegraph)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            // 着地点が固定される時間に入ったかを判定する
            bool locked = slamTelegraph - t <= slamLockTime;

            // 着地点が固定されるまでは、プレイヤーの現在位置を追い続ける
            if (!locked)
                lockedU = ProjectU(target, player.position);

            // 狙っている位置を、実際のワールド座標に変換する
            Vector2 to = SpotOn(target, lockedU);

            // 着地点付近に初期障害物があれば、その上に着地する位置に変更する
            if (TryGetInitialObjectLandingPosition(target, to, out Vector2 previewLanding))
            {
                to = previewLanding;
            }

            // ボスの現在位置から着地点までの予兆線を表示する
            slamLine.SetPosition(0, transform.position);
            slamLine.SetPosition(1, to);

            // 着地点を示すマーカーを移動させる
            marker.transform.position = to;

            // 着地点が固定される前は薄く、固定された後は濃く表示する
            SetTelegraphAlpha(locked ? 1f : 0.35f);

            // ボスを赤く点滅させて、落下攻撃が来ることを知らせる
            brainColor = ((int)(Time.time * 14f) % 2 == 0)
                ? Color.red
                : baseColor;

            // 次のフレームまで待つ
            yield return null;
        }

        // 予兆の線とマーカーを非表示にする
        HideTelegraph();

        // ボスの色を元に戻す
        brainColor = baseColor;

        // 状態表示を消す
        label = null;

        // ---------- 落下処理 ----------
        // 落下開始時のボスの位置を取得する
        Vector2 from = transform.position;

        // 固定された着地点の座標を取得する
        Vector2 dest = SpotOn(target, lockedU);

        // 着地点に初期障害物があれば、障害物に接する位置に変更する
        if (TryGetInitialObjectLandingPosition(target, dest, out Vector2 landing))
        {
            dest = landing;
        }

        // 落下中だけ、ボスとの接触によるダメージを有効にする
        contactEnabled = true;

        // 落下時間の計測を開始する
        t = 0f;

        // 設定された落下時間が経過するまで移動する
        while (t < diveTime)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            // 落下の進行度を0～1の範囲に収める
            float k = Mathf.Clamp01(t / diveTime);

            // kの2乗を使い、最初はゆっくり、後半は速くなるように移動させる
            SetPos(Vector2.Lerp(from, dest, k * k));

            // 次のフレームまで待つ
            yield return null;
        }

        // 最終的な位置を着地点に合わせる
        SetPos(dest);

        // ボスが現在いる壁を、落下先の壁に更新する
        currentWall = target;

        // 落下が終了したので、接触ダメージを無効にする
        contactEnabled = false;

        // 着地した位置から、壁に沿って左右両方向へ衝撃波を発生させる
        SpawnShockwaves(target, dest);
    }

    // 衝撃波:
    // ボスが着地した場所から、箱の壁に沿って左右両方向へ衝撃波を走らせる。
    void SpawnShockwaves(Wall wall, Vector2 impact)
    {
        // 箱の内側の範囲を取得する
        Rect inner = Inner();

        // 衝撃波が走る経路を作る
        // waveInsetの分だけ箱の壁から内側にずらしている
        var loop = new Rect(inner.xMin + waveInset, inner.yMin + waveInset,
            inner.width - waveInset * 2f, inner.height - waveInset * 2f);

        // ボスの着地位置を、衝撃波の経路上の位置(距離パラメータ)に変換する
        // wallは着地した壁の番号、impactは実際の着地座標
        float s0 = BossShockwave.ParamOnLoop(loop, (int)wall, impact);

        // 経路全体の長さの半分を計算する
        // 左右から進む衝撃波が、経路の反対側で出会うまでの距離になる
        float half = BossShockwave.Perimeter(loop) * 0.5f;

        // 衝撃波の色を設定する(赤みのあるオレンジ色)
        var color = new Color(1f, 0.35f, 0.2f, 0.9f);

        // 着地点から正方向(+1)へ進む衝撃波を発生させる
        BossShockwave.Spawn(loop, s0, +1, waveSpeed, half, waveRadius, waveDamage, playerCol, playerHealth, color);

        // 着地点から逆方向(-1)へ進む衝撃波を発生させる
        BossShockwave.Spawn(loop, s0, -1, waveSpeed, half, waveRadius, waveDamage, playerCol, playerHealth, color);
    }

    // 遠距離攻撃:
    // 前兆としてボスをオレンジ色に点滅させたあと、プレイヤーの方向へ弾を連射する。
    IEnumerator ShootAttack()
    {
        // プレイヤーが存在しない場合は、攻撃を行わず終了する
        if (player == null) yield break;

        // ボスの状態表示を「遠距離!」にする
        label = "遠距離!";

        //  攻撃前の予兆 
        float t = 0f;

        // 設定された予兆時間が経過するまで繰り返す
        while (t < shootTelegraph)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            // ボスをオレンジ色と通常色で交互に点滅させる
            // Time.timeを使うことで、時間に応じて点滅を切り替える
            brainColor = ((int)(Time.time * 14f) % 2 == 0)
                ? new Color(1f, 0.55f, 0.1f)
                : baseColor;

            // 次のフレームまで待つ
            yield return null;
        }

        // 予兆が終わったので、ボスの色を元に戻す
        brainColor = baseColor;

        // 状態表示を消す
        label = null;

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

        // ボスの状態表示を「チャンス!」にする
        label = "チャンス!";

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

        // 状態表示を消す
        label = null;
    }

    // 回転攻撃:
    // 前兆としてボスを紫色に点滅させ、回転する方向を表示する。
    // 前兆が終わると、箱全体を90度回転させる。
    IEnumerator RotateAttack()
    {
        // 箱を回転させるArenaRotatorを取得する
        var arena = ArenaRotator.Instance;

        // ArenaRotatorが存在しない場合、または既に回転中の場合は終了する
        if (arena == null || arena.IsRotating) yield break;

        // 時計回りか反時計回りかをランダムに決める
        var dir = Random.value < 0.5f
            ? ArenaRotator.Direction.Clockwise
            : ArenaRotator.Direction.CounterClockwise;

        // 選ばれた回転方向を画面に表示する
        label = dir == ArenaRotator.Direction.Clockwise
            ? "回転!  時計回り"
            : "回転!  反時計回り";

        //回転前の予兆
        // 経過時間を初期化する
        float t = 0f;

        // 設定された予兆時間が経過するまで繰り返す
        while (t < rotateTelegraph)
        {
            // 経過時間を加算する
            t += Time.deltaTime;

            // ボスを紫色と通常色で交互に点滅させる
            // Time.timeを使って、一定間隔で色を切り替える
            brainColor = ((int)(Time.time * 10f) % 2 == 0)
                ? new Color(0.7f, 0.3f, 1f)
                : baseColor;

            // 次のフレームまで待つ
            yield return null;
        }

        // 予兆が終わったので、ボスの色を元に戻す
        brainColor = baseColor;

        // 回転方向の表示を消す
        label = null;

        // 箱の回転
        // 決定した方向に箱を回転させる
        // ArenaRotator側で90度の回転処理を行う
        arena.TryRotate(dir);
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

    //  表示 
    // 落下攻撃の予兆として表示する線(LineRenderer)を作成する
    LineRenderer MakeLine()
    {
        // 線を表示するためのGameObjectを作成する
        var go = new GameObject("BossSlamLine");

        // ボスの子オブジェクトにして、ボスと一緒に管理できるようにする
        go.transform.SetParent(transform, false);

        // LineRendererコンポーネントを追加する
        var lr = go.AddComponent<LineRenderer>();

        // 線の座標をワールド座標で指定する
        lr.useWorldSpace = true;

        // 線の始点と終点の2点を設定できるようにする
        lr.positionCount = 2;

        // Inspectorで設定されたマテリアルを使用する
        // 設定されていない場合は、標準のSprites/Defaultを使用する
        lr.material = lineMaterial != null
            ? lineMaterial
            : new Material(Shader.Find("Sprites/Default"));

        // 線の太さを設定する
        lr.widthMultiplier = 0.2f;

        // 描画順を設定する(数字が大きいほど手前に表示される)
        lr.sortingOrder = 5;

        // 最初は線を非表示にしておく
        lr.enabled = false;

        // 作成したLineRendererを返す
        return lr;
    }

    // 落下攻撃の着地点を示すマーカーを作成する
    GameObject MakeMarker()
    {
        // マーカー用のGameObjectを作成する
        var go = new GameObject("BossSlamMarker");

        // マーカーにSpriteRendererを追加する
        var msr = go.AddComponent<SpriteRenderer>();

        // BossShockwaveで使用している四角形のスプライトを設定する
        msr.sprite = BossShockwave.SquareSprite;

        // 描画順を設定する(数字が大きいほど手前に表示される)
        msr.sortingOrder = 4;

        // マーカーの大きさを通常の1.5倍にする
        go.transform.localScale = Vector3.one * 1.5f;

        // 作成直後はマーカーを非表示にしておく
        go.SetActive(false);

        // 作成したマーカーのGameObjectを返す
        return go;
    }

    // 落下攻撃の予兆表示の透明度を変更する
    // a : 透明度(0に近いほど透明、1に近いほど不透明)
    void SetTelegraphAlpha(float a)
    {
        // 赤色をベースに、引数aで透明度を設定する
        var c = new Color(1f, 0.2f, 0.2f, a);

        // 予兆線の始点の色と透明度を変更する
        slamLine.startColor = c;

        // 予兆線の終点の色と透明度を変更する
        slamLine.endColor = c;

        // 着地点マーカーのSpriteRendererを取得する
        var msr = marker.GetComponent<SpriteRenderer>();

        // マーカーの色を赤色にし、透明度をaの80%に設定する
        // 線より少し薄く表示することで、見た目を調整する
        msr.color = new Color(1f, 0.2f, 0.2f, a * 0.8f);
    }

    //  画面にボスの情報を表示する 
    void OnGUI()
    {
        // レベル編集画面、またはリザルト画面では表示しない
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        //  ボスの体力バー 
        float w = 300f, h = 18f;  // 体力バーの幅と高さ
        float x = 30f, y = 100f;  // 画面左上からの表示位置

        // 攻撃タイミング中は黄色、それ以外は黒っぽい色で枠を表示
        GUI.color = vulnerable ? new Color(1f, 0.9f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Texture2D.whiteTexture);

        // 体力バーの背景を黒色で表示
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 現在の体力に応じて赤色のバーを表示
        // hp / maxHp によって、残り体力に合わせて横幅を変える
        GUI.color = new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w * hp / maxHp, h), Texture2D.whiteTexture);

        // GUIの色を白色に戻す
        GUI.color = Color.white;

        // ボスの名前と現在の体力を表示
        GUI.Label(new Rect(x, y - 24f, w, 24f), $"フレーム   {Mathf.CeilToInt(hp)}/{maxHp}");

        //  前兆と攻撃タイミングの表示 
        // labelに文字が設定されている場合だけ表示する
        if (!string.IsNullOrEmpty(label))
        {
            // GUIStyleがまだ作られていない場合、初回だけ作成する
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,                       // 文字の大きさ
                    alignment = TextAnchor.MiddleCenter, // 画面中央に配置
                    fontStyle = FontStyle.Bold           // 太字にする
                };
            }

            // 攻撃タイミング中は黄色、それ以外はオレンジ色で文字を表示
            bigStyle.normal.textColor = vulnerable
                ? new Color(1f, 0.95f, 0.3f)
                : new Color(1f, 0.85f, 0.3f);

            // 画面中央付近に、現在の行動を表示する
            GUI.Label(
                new Rect(0f, Screen.height * 0.2f, Screen.width, 80f),
                label,
                bigStyle
            );
        }
    }
}
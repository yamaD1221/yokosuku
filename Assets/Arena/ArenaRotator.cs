using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 箱の見た目の向きを90度ずつ回転させる
// 物理の世界（箱・プレイヤー・ボス）は回さず、カメラなどの見た目だけを回転させる
// プレイヤーは床に付いたまま、入力方向を回転に合わせて読み替える
public class ArenaRotator : MonoBehaviour
{
    // 回転する方向
    public enum Direction { Clockwise, CounterClockwise }

    // シングルトン：ArenaRotatorのインスタンスをどこからでも取得できるようにする
    public static ArenaRotator Instance { get; private set; }

    [Header("箱の大きさ")]
    [SerializeField] private float innerSize = 16f;           // 内側の空間の一辺（戦う場所の大きさ）
    [SerializeField] private float wallThickness = 0.5f;      // 壁の厚さ

    [Header("壁(空なら、子の Floor / Ceiling / LeftWall / RightWall を探す)")]
    [SerializeField] private Transform floorWall;             // 下の壁
    [SerializeField] private Transform ceilingWall;           // 上の壁
    [SerializeField] private Transform leftWall;              // 左の壁
    [SerializeField] private Transform rightWall;             // 右の壁

    [Header("回転")]
    [SerializeField] private float rotateDuration = 4f;       // 回転にかける秒数
    [SerializeField] private bool debugKeys = true;           // 確認用のキー入力を有効にするか

    // 回転中かどうか
    public bool IsRotating { get; private set; }

    // 時計回りに何回90度回ったか（0〜3）
    public int QuarterTurns { get; private set; }

    // 回転の進み具合（0〜1）
    public float Progress { get; private set; }

    // 箱が時計回りに回って見える角度
    public float ViewAngle { get; private set; }

    // 箱の内側と外側の大きさを取得する
    public float InnerSize => innerSize;
    public float OuterSize => innerSize + wallThickness * 2f;

    // InnerBounds：箱の内側の範囲をワールド座標で取得する
    public Rect InnerBounds
    {
        get
        {
            // 箱の中心位置と、中心から壁までの距離を取得
            Vector2 c = transform.position;
            float h = innerSize * 0.5f;

            // 箱の左下を基準に、内側の範囲をRectで返す
            return new Rect(c.x - h, c.y - h, innerSize, innerSize);
        }
    }

    // 回転開始時と回転終了時に呼び出されるイベント
    public event Action<Direction> RotationStarted;
    public event Action<Direction> RotationFinished;

    // 回転の管理に使う変数
    private int totalSteps;                                   // 累計の回転数（時計回りが+1）
    private float startAngle, endAngle, elapsed;              // 回転開始角度・終了角度・経過時間
    private Direction currentDirection;                      // 現在回転している方向

    // Awake：ゲーム開始時に1回だけ実行。インスタンスを登録して壁を配置する
    void Awake()
    {
        Instance = this;
        ApplyLayout();
    }

    // OnDestroy：オブジェクトが破棄されたとき、登録したインスタンスを解除する
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ApplyLayout：箱の大きさに合わせて4つの壁を配置する
    // [ContextMenu]：インスペクターのコンポーネントメニューから実行できる
    [ContextMenu("壁を配置する")]
    public void ApplyLayout()
    {
        // 壁が設定されていない場合は、子オブジェクトから名前で探す
        if (floorWall == null) floorWall = transform.Find("Floor");
        if (ceilingWall == null) ceilingWall = transform.Find("Ceiling");
        if (leftWall == null) leftWall = transform.Find("LeftWall");
        if (rightWall == null) rightWall = transform.Find("RightWall");

        // 箱の内側の半分の大きさ、壁の厚さ、外側の大きさを取得
        float h = innerSize * 0.5f;
        float t = wallThickness;
        float outer = innerSize + t * 2f;

        // それぞれの壁の位置と大きさを設定する
        PlaceWall(floorWall, new Vector2(0f, -(h + t * 0.5f)), new Vector2(outer, t));
        PlaceWall(ceilingWall, new Vector2(0f, h + t * 0.5f), new Vector2(outer, t));
        PlaceWall(leftWall, new Vector2(-(h + t * 0.5f), 0f), new Vector2(t, outer));
        PlaceWall(rightWall, new Vector2(h + t * 0.5f, 0f), new Vector2(t, outer));
    }

    // PlaceWall：指定した壁の位置と大きさを設定する
    static void PlaceWall(Transform wall, Vector2 localPos, Vector2 size)
    {
        // 壁が存在しない場合は処理を終了
        if (wall == null) return;

        // 壁の位置と大きさを設定（ローカル座標）
        wall.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        wall.localScale = new Vector3(size.x, size.y, 1f);
    }

    // Update：毎フレーム実行。キー入力の確認と箱の回転処理を行う
    void Update()
    {
        // デバッグ用キー入力が有効で、ゲームが停止していない場合に実行
        if (debugKeys && Time.timeScale > 0f)
        {
            // キーボードの入力情報を取得
            var kb = Keyboard.current;

            if (kb != null)
            {
                // ]キーで時計回り、[キーで反時計回りに回転する
                if (kb.rightBracketKey.wasPressedThisFrame) TryRotate(Direction.Clockwise);
                if (kb.leftBracketKey.wasPressedThisFrame) TryRotate(Direction.CounterClockwise);
            }
        }

        // 回転中でなければ処理を終了
        if (!IsRotating) return;

        // 回転開始からの経過時間を加算
        elapsed += Time.deltaTime;

        // 回転の進み具合を0〜1の範囲で計算
        // rotateDurationが0以下の場合は、すぐに回転を完了させる
        float t = rotateDuration > 0f ? Mathf.Clamp01(elapsed / rotateDuration) : 1f;
        Progress = t;

        // SmoothStepで回転速度を滑らかに変化させながら角度を計算
        ViewAngle = Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, t));

        // 回転が完了した場合
        if (t >= 1f)
        {
            // 回転数から正確な角度を設定する
            ViewAngle = endAngle;

            // 累計回転数を0〜3の範囲に変換する
            // マイナスの回転数にも対応するため、余りを調整する
            QuarterTurns = ((totalSteps % 4) + 4) % 4;

            // 回転状態を終了にする
            IsRotating = false;

            // 回転終了イベントを呼び出す
            RotationFinished?.Invoke(currentDirection);
        }
    }

    // TryRotate：指定した方向への回転を開始する。回転中ならfalseを返す
    public bool TryRotate(Direction direction)
    {
        // すでに回転中なら新しい回転を受け付けない
        if (IsRotating) return false;

        // 現在の回転角度を開始角度として設定
        startAngle = 90f * totalSteps;

        // 回転方向に応じて累計回転数を変更する
        // 時計回りなら+1、反時計回りなら-1
        totalSteps += direction == Direction.Clockwise ? 1 : -1;

        // 次の回転角度を設定
        endAngle = 90f * totalSteps;

        // 回転方向と経過時間を初期化
        currentDirection = direction;
        elapsed = 0f;
        Progress = 0f;

        // 回転状態を開始にする
        IsRotating = true;

        // 回転開始イベントを呼び出す
        RotationStarted?.Invoke(direction);

        return true;
    }

    // ResetRotation：回転状態をリセットして、向きを0度に戻す
    public void ResetRotation()
    {
        // 回転状態と回転の進み具合をリセット
        IsRotating = false;
        Progress = 0f;

        // 回転数と角度を初期状態に戻す
        totalSteps = 0;
        QuarterTurns = 0;
        ViewAngle = 0f;
    }

    // ScreenToLocal：画面上の方向を箱の中の方向に変換する
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        // 現在の回転角度をラジアンに変換
        // Mathf.SinとMathf.Cosはラジアンで計算するため
        float rad = ViewAngle * Mathf.Deg2Rad;

        // 回転角度のCosとSinを取得
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        // 回転角度に合わせて入力方向を変換して返す
        return new Vector2(screen.x * c - screen.y * s, screen.x * s + screen.y * c);
    }

    // OnGUI：画面上にデバッグ情報を表示する（現在はコメントアウト）
    //void OnGUI()
    //{
    //    if (!debugKeys) return;
    //    GUI.Label(new Rect(10, Screen.height - 30, 700, 30),
    //        $"箱の回転 [ ]:反時計/時計   向き: 時計回りに{QuarterTurns}回   {(IsRotating ? $"回転中 {Progress * 100f:F0}%" : "停止中")}");
    //}

    // OnDrawGizmosSelected：オブジェクト選択時に箱の内側の範囲をSceneビューに表示する
    void OnDrawGizmosSelected()
    {
        // ギズモの色を黄色に設定
        Gizmos.color = Color.yellow;

        // 箱の内側の範囲をワイヤーフレームの四角形で表示
        Gizmos.DrawWireCube(transform.position, new Vector3(innerSize, innerSize, 0f));
    }
}
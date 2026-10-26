using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 箱の「見た目の向き」を、90度ずつ回す。
// 物理の世界(箱・プレイヤー・ボス)は回さず、カメラを傾けて、箱が回って見えるようにする。
// プレイヤーは箱の床に付いたままで(落ちない)、入力は、回転に合わせて読み替える
public class ArenaRotator : MonoBehaviour
{
    public enum Direction { Clockwise, CounterClockwise }

    public static ArenaRotator Instance { get; private set; }

    [Header("箱の大きさ")]
    [SerializeField] private float innerSize = 16f;           // 内側の空間の一辺(置ける範囲、戦う場所)
    [SerializeField] private float wallThickness = 0.5f;      // 壁の厚さ

    [Header("壁(空なら、子の Floor / Ceiling / LeftWall / RightWall を探す)")]
    [SerializeField] private Transform floorWall;
    [SerializeField] private Transform ceilingWall;
    [SerializeField] private Transform leftWall;
    [SerializeField] private Transform rightWall;

    [Header("回転")]
    [SerializeField] private float rotateDuration = 4f;       // 回転にかける秒数
    [SerializeField] private bool debugKeys = true;           // 確認用: [ で反時計回り、] で時計回り

    public bool IsRotating { get; private set; }
    public int QuarterTurns { get; private set; }             // 時計回りに、何回90度回ったか(0〜3)
    public float Progress { get; private set; }               // 回転の進み具合(0〜1)
    public float ViewAngle { get; private set; }              // 箱が時計回りに回って見える角度(度)

    public float InnerSize => innerSize;
    public float OuterSize => innerSize + wallThickness * 2f;

    // 内側の空間(ワールド座標)
    public Rect InnerBounds
    {
        get
        {
            Vector2 c = transform.position;
            float h = innerSize * 0.5f;
            return new Rect(c.x - h, c.y - h, innerSize, innerSize);
        }
    }

    public event Action<Direction> RotationStarted;
    public event Action<Direction> RotationFinished;

    private int totalSteps;                                   // 累計の回転数(時計回りが +1)
    private float startAngle, endAngle, elapsed;
    private Direction currentDirection;

    void Awake()
    {
        Instance = this;
        ApplyLayout();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // 箱の大きさに合わせて、4つの壁を配置する(エディタでは、コンポーネントの「︙」から実行できる)
    [ContextMenu("壁を配置する")]
    public void ApplyLayout()
    {
        if (floorWall == null) floorWall = transform.Find("Floor");
        if (ceilingWall == null) ceilingWall = transform.Find("Ceiling");
        if (leftWall == null) leftWall = transform.Find("LeftWall");
        if (rightWall == null) rightWall = transform.Find("RightWall");

        float h = innerSize * 0.5f;
        float t = wallThickness;
        float outer = innerSize + t * 2f;

        PlaceWall(floorWall, new Vector2(0f, -(h + t * 0.5f)), new Vector2(outer, t));
        PlaceWall(ceilingWall, new Vector2(0f, h + t * 0.5f), new Vector2(outer, t));
        PlaceWall(leftWall, new Vector2(-(h + t * 0.5f), 0f), new Vector2(t, outer));
        PlaceWall(rightWall, new Vector2(h + t * 0.5f, 0f), new Vector2(t, outer));
    }

    static void PlaceWall(Transform wall, Vector2 localPos, Vector2 size)
    {
        if (wall == null) return;
        wall.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        wall.localScale = new Vector3(size.x, size.y, 1f);
    }

    void Update()
    {
        if (debugKeys && Time.timeScale > 0f)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightBracketKey.wasPressedThisFrame) TryRotate(Direction.Clockwise);
                if (kb.leftBracketKey.wasPressedThisFrame) TryRotate(Direction.CounterClockwise);
            }
        }

        if (!IsRotating) return;

        elapsed += Time.deltaTime;
        float t = rotateDuration > 0f ? Mathf.Clamp01(elapsed / rotateDuration) : 1f;
        Progress = t;
        ViewAngle = Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f)
        {
            ViewAngle = endAngle;                             // 回転数から計算するので、正確に90度の倍数
            QuarterTurns = ((totalSteps % 4) + 4) % 4;
            IsRotating = false;
            RotationFinished?.Invoke(currentDirection);
        }
    }

    // 回転を始める。回転中なら、受け付けずに false を返す
    public bool TryRotate(Direction direction)
    {
        if (IsRotating) return false;

        startAngle = 90f * totalSteps;
        totalSteps += direction == Direction.Clockwise ? 1 : -1;
        endAngle = 90f * totalSteps;

        currentDirection = direction;
        elapsed = 0f;
        Progress = 0f;
        IsRotating = true;
        RotationStarted?.Invoke(direction);
        return true;
    }

    // 向きを、即座に0度へ戻す(編集に戻るとき、やり直すとき)
    public void ResetRotation()
    {
        IsRotating = false;
        Progress = 0f;
        totalSteps = 0;
        QuarterTurns = 0;
        ViewAngle = 0f;
    }

    // 画面上の方向(上が +y、右が +x)を、箱の中の方向に変換する
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        float rad = ViewAngle * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(screen.x * c - screen.y * s, screen.x * s + screen.y * c);
    }

    void OnGUI()
    {
        if (!debugKeys) return;
        GUI.Label(new Rect(10, 140, 700, 30),
            $"箱の回転 [ ]:反時計/時計   向き: 時計回りに{QuarterTurns}回   {(IsRotating ? $"回転中 {Progress * 100f:F0}%" : "停止中")}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(innerSize, innerSize, 0f));
    }
}
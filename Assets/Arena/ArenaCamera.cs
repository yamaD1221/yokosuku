using UnityEngine;

// RequireComponent：このスクリプトを付けたオブジェクトにCameraが無い場合、自動で追加する
[RequireComponent(typeof(Camera))]

// MonoBehaviourを継承：Unityのオブジェクトにアタッチして使うための基本クラス
// AwakeやLateUpdateなど、Unityが自動で呼び出す関数が使える
public class ArenaCamera : MonoBehaviour
{
    // [SerializeField]：privateのままインスペクターに表示して数値を調整できる

    [SerializeField] private Transform arenaCenter;               // 箱の中心。空ならArenaRotatorの位置を使う
    [SerializeField] private float arenaSize = 10f;               // 箱がまだ無いとき用の仮の大きさ
    [SerializeField] private float margin = 0.5f;                 // 箱のまわりの余白

    // [Range]：インスペクターで指定した範囲のスライダーになる
    // 画面の高さに対する割合で、UIが画面を隠す部分を表す
    [SerializeField, Range(0f, 0.4f)] private float topUiFraction = 0.10f;            // 上のHUDの高さ
    [SerializeField, Range(0f, 0.4f)] private float bottomUiFractionEdit = 0.14f;     // 編集中の下のパレットの高さ
    [SerializeField, Range(0f, 0.4f)] private float bottomUiFractionPlay = 0.04f;     // プレイ中の下の余白

    // このオブジェクトに付いているCameraを保存する変数
    // 毎フレーム取得しなくていいように、最初に1回だけ取得する
    private Camera cam;

    // Awake：ゲーム開始時に1回だけ実行。Cameraを取得して保存する
    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    // LateUpdate：すべてのUpdateが終わった後に毎フレーム実行
    // 箱の回転が終わった後の角度に合わせてカメラを調整するため、LateUpdateを使う
    void LateUpdate()
    {
        // 2Dゲームなので平行投影にする（奥行きによって大きさが変わらない）
        cam.orthographic = true;

        // ArenaRotatorのインスタンスを取得
        var arena = ArenaRotator.Instance;

        // 箱の中心位置を取得
        // 優先順位：arenaCenter → ArenaRotatorの位置 → 原点
        // 条件 ? A : B は、条件が正しければA、違えばBを使う（三項演算子）
        Vector3 c = arenaCenter != null ? arenaCenter.position
                  : (arena != null ? arena.transform.position : Vector3.zero);

        // 箱の大きさと回転角度を取得。箱が無い場合は仮の値を使う
        float size = arena != null ? arena.OuterSize : arenaSize;
        float angle = arena != null ? arena.ViewAngle : 0f;

        // 下のUIの高さを取得。編集中かプレイ中かで切り替える
        // LevelEditor.IsEditingはstaticなので、インスタンスなしで取得できる
        float bottom = LevelEditor.IsEditing ? bottomUiFractionEdit : bottomUiFractionPlay;

        // 画面全体から上と下のUIを引いて、実際に使える画面の割合を求める
        // Mathf.Maxで最低0.3は残す
        float usable = Mathf.Max(0.3f, 1f - topUiFraction - bottom);

        // 回転した箱が画面内に収まるために必要な大きさを計算する
        // 回転した正方形の幅 = 一辺 × (|cosθ| + |sinθ|)
        // 45度のときに最も大きくなるため、この式でどの角度でも収まるようにする
        float rad = angle * Mathf.Deg2Rad;      // 度をラジアンに変換
        float half = size * 0.5f * (Mathf.Abs(Mathf.Cos(rad)) + Mathf.Abs(Mathf.Sin(rad)));
        float need = half + margin;             // 余白を足した必要な大きさ

        // カメラに映す範囲を計算する
        // 縦方向と横方向の両方に箱が収まるように、大きい方を採用する
        // orthographicSizeは画面の縦の半分の長さ
        float ortho = Mathf.Max(need / usable, need / cam.aspect);

        // カメラに映す範囲を設定する
        cam.orthographicSize = ortho;

        // UIを避けて、箱が画面の使える範囲の中央に表示されるようにする
        // 画面の下から数えた、使える範囲の中心位置を求める
        float centerY = bottom + usable * 0.5f;

        // 箱と同じ角度だけZ軸まわりにカメラを回転させる
        // 箱が常にまっすぐ見えるようにする
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        // 回転後のカメラの上方向を取得する
        // カメラを移動させる方向を求めるために使う
        Vector3 up = rot * Vector3.up;

        // 使える画面範囲の中心に箱が表示されるように、カメラの位置を調整する
        // 画面の中心からのずれをワールド座標の長さに変換する
        // 箱を上に表示したい場合、カメラは下に移動するため引き算になる
        Vector3 pos = c - up * ((centerY - 0.5f) * 2f * ortho);

        // カメラの回転を設定する
        transform.rotation = rot;

        // カメラの位置を設定する
        // Zは-10に固定して、2Dの箱より手前にカメラを配置する
        transform.position = new Vector3(pos.x, pos.y, -10f);
    }
}
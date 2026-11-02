
using UnityEngine;

// 箱を映す固定カメラ。箱の向きに合わせてカメラを傾け、箱全体が、画面のUIに隠れずに収まるようにする
[RequireComponent(typeof(Camera))]
public class ArenaCamera : MonoBehaviour
{
    [SerializeField] private Transform arenaCenter;               // 空なら、ArenaRotator の位置
    [SerializeField] private float arenaSize = 10f;               // 箱がないときの仮の大きさ
    [SerializeField] private float margin = 0.5f;                 // 余白
    [SerializeField, Range(0f, 0.4f)] private float topUiFraction = 0.10f;            // 上のHUDの高さ(画面に対する割合)
    [SerializeField, Range(0f, 0.4f)] private float bottomUiFractionEdit = 0.14f;     // 編集中の、下のパレットの高さ
    [SerializeField, Range(0f, 0.4f)] private float bottomUiFractionPlay = 0.04f;     // プレイ中の、下の余白

    private Camera cam;

    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        cam.orthographic = true;

        var arena = ArenaRotator.Instance;
        Vector3 c = arenaCenter != null ? arenaCenter.position
                  : (arena != null ? arena.transform.position : Vector3.zero);
        float size = arena != null ? arena.OuterSize : arenaSize;
        float angle = arena != null ? arena.ViewAngle : 0f;

        float bottom = LevelEditor.IsEditing ? bottomUiFractionEdit : bottomUiFractionPlay;
        float usable = Mathf.Max(0.3f, 1f - topUiFraction - bottom);

        // 傾けた箱の外形が、どの角度でも、使える範囲に収まる大きさ
        float rad = angle * Mathf.Deg2Rad;
        float half = size * 0.5f * (Mathf.Abs(Mathf.Cos(rad)) + Mathf.Abs(Mathf.Sin(rad)));
        float need = half + margin;
        float ortho = Mathf.Max(need / usable, need / cam.aspect);
        cam.orthographicSize = ortho;

        // 箱の中心が、使える範囲の真ん中に見えるように、カメラをずらす
        float centerY = bottom + usable * 0.5f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);
        Vector3 up = rot * Vector3.up;
        Vector3 pos = c - up * ((centerY - 0.5f) * 2f * ortho);

        transform.rotation = rot;
        transform.position = new Vector3(pos.x, pos.y, -10f);
    }
}

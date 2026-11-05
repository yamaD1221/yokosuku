using UnityEngine;

// 衝撃波。箱の壁に沿って床 → 右の壁 → 天井 → 左の壁 → 床の順番で移動する
// 壁から離れた場所にいるプレイヤーには当たらない
public class BossShockwave : MonoBehaviour
{
    // 衝撃波の見た目に使う四角形のSprite
    private static Sprite square;

    // SquareSprite：衝撃波用のSpriteを取得する
    // まだ作られていない場合は作成して、次回以降は使い回す
    public static Sprite SquareSprite
    {
        get
        {
            if (square == null)
            {
                // 白いテクスチャから四角形のSpriteを作成する
                var tex = Texture2D.whiteTexture;
                square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return square;
        }
    }

    // 衝撃波の中心が移動する四角形の道
    private Rect loop;

    // 道の上での現在位置（スタート地点からの距離）
    private float s;

    // 移動する方向（+1：順方向、-1：逆方向）
    private int dir;

    // 移動速度・残りの移動距離・衝撃波の半径
    private float speed, remaining, radius;

    // プレイヤーに与えるダメージ量
    private int damage;

    // プレイヤーのCollider
    private Collider2D playerCol;

    // プレイヤーの体力を管理するスクリプト
    private PlayerHealth playerHealth;

    // Spawn：衝撃波を生成して、移動経路や速度などを設定する
    public static void Spawn(Rect loop, float startS, int dir, float speed, float distance, float radius,
        int damage, Collider2D playerCol, PlayerHealth playerHealth, Color color)
    {
        // 衝撃波のGameObjectを作成する
        var go = new GameObject("BossShockwave");

        // SpriteRendererを追加して、衝撃波の見た目を設定する
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.sortingOrder = 7;

        // 衝撃波の大きさを設定する
        go.transform.localScale = Vector3.one * radius * 2f;

        // BossShockwaveを追加して、衝撃波の情報を保存する
        var w = go.AddComponent<BossShockwave>();
        w.loop = loop;
        w.s = startS;
        w.dir = dir;
        w.speed = speed;
        w.remaining = distance;
        w.radius = radius;
        w.damage = damage;
        w.playerCol = playerCol;
        w.playerHealth = playerHealth;

        // 道の上の開始位置から、実際の座標を求めて配置する
        go.transform.position = PointOnLoop(loop, startS);
    }

    // Update：毎フレーム実行。衝撃波の移動とプレイヤーとの当たり判定を行う
    void Update()
    {
        // レベル編集中なら衝撃波を削除する
        if (LevelEditor.IsEditing) { Destroy(gameObject); return; }

        // 1フレームに進む時間を取得
        float dt = Time.deltaTime;

        // 時間が進んでいない場合は処理を終了（一時停止中）
        if (dt <= 0f) return;

        // 1フレームで移動する距離を計算
        float step = speed * dt;

        // 移動方向に合わせて道の上の位置を更新する
        s += dir * step;

        // 残りの移動距離を減らす
        remaining -= step;

        // 移動距離がなくなったら衝撃波を削除する
        if (remaining <= 0f) { Destroy(gameObject); return; }

        // 道の上の位置から実際のワールド座標を求める
        Vector2 pos = PointOnLoop(loop, s);

        // 衝撃波の位置を更新する
        transform.position = pos;

        // プレイヤーに当たったらダメージを与える
        // 無敵時間の管理はPlayerHealth側で行う
        if (playerCol != null && playerHealth != null)
        {
            // ClosestPoint：プレイヤーのCollider上で、衝撃波に最も近い位置を取得する
            Vector2 closest = playerCol.ClosestPoint(pos);

            // 衝撃波とプレイヤーの最短距離が半径以内なら当たったと判定する
            if ((closest - pos).sqrMagnitude <= radius * radius)
            {
                // プレイヤーが衝撃波の右側にいるか左側にいるかで、吹き飛ばす方向を決める
                int fromDir = playerCol.bounds.center.x >= pos.x ? 1 : -1;

                // プレイヤーにダメージを与える
                playerHealth.TakeDamage(damage, fromDir);
            }
        }
    }

    // Perimeter：道となる四角形の一周分の長さを求める
    public static float Perimeter(Rect r) => 2f * (r.width + r.height);

    // PointOnLoop：道の上の位置（距離）から、実際の座標を求める
    // 移動する順番：床（左→右）→ 右の壁（下→上）→ 天井（右→左）→ 左の壁（上→下）
    public static Vector2 PointOnLoop(Rect r, float s)
    {
        // 四角形の幅・高さ・一周分の長さを取得
        float w = r.width, h = r.height, p = Perimeter(r);

        // 位置が一周分の長さを超えても、0?一周分の範囲に収める
        // マイナスの位置にも対応する
        s = ((s % p) + p) % p;

        // 床：左から右へ移動
        if (s < w) return new Vector2(r.xMin + s, r.yMin);

        // 床の長さを引いて、次の壁での位置に変換
        s -= w;

        // 右の壁：下から上へ移動
        if (s < h) return new Vector2(r.xMax, r.yMin + s);

        // 右の壁の長さを引く
        s -= h;

        // 天井：右から左へ移動
        if (s < w) return new Vector2(r.xMax - s, r.yMax);

        // 天井の長さを引く
        s -= w;

        // 左の壁：上から下へ移動
        return new Vector2(r.xMin, r.yMax - s);
    }

    // ParamOnLoop：壁の番号と壁の上の座標から、道の上の位置（距離）を求める
    // 壁の番号：0=床、1=右、2=天井、3=左
    public static float ParamOnLoop(Rect r, int wall, Vector2 p)
    {
        // 四角形の幅と高さを取得
        float w = r.width, h = r.height;

        // 壁の方向に応じて、道の上での位置を計算する
        switch (wall)
        {
            // 床：左端から右方向への距離
            case 0: return Mathf.Clamp(p.x - r.xMin, 0f, w);

            // 右の壁：床の長さに、下端から上方向への距離を足す
            case 1: return w + Mathf.Clamp(p.y - r.yMin, 0f, h);

            // 天井：床と右の壁の長さに、右端から左方向への距離を足す
            case 2: return w + h + Mathf.Clamp(r.xMax - p.x, 0f, w);

            // 左の壁：床・右の壁・天井の長さに、上端から下方向への距離を足す
            default: return 2f * w + h + Mathf.Clamp(r.yMax - p.y, 0f, h);
        }
    }
}
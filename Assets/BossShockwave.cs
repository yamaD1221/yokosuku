using UnityEngine;

// 衝撃波。箱の壁に沿って(床 → 右の壁 → 天井 → 左の壁 → 床…の順に)走る。壁から離れていれば、当たらない
public class BossShockwave : MonoBehaviour
{
    private static Sprite square;
    public static Sprite SquareSprite
    {
        get
        {
            if (square == null)
            {
                var tex = Texture2D.whiteTexture;
                square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return square;
        }
    }

    private Rect loop;              // 波の中心が通る道(箱の内側から、少し内側に入った四角)
    private float s;                // 道の上の位置(長さ)
    private int dir;                // +1 / -1
    private float speed, remaining, radius;
    private int damage;
    private Collider2D playerCol;
    private PlayerHealth playerHealth;

    public static void Spawn(Rect loop, float startS, int dir, float speed, float distance, float radius,
        int damage, Collider2D playerCol, PlayerHealth playerHealth, Color color)
    {
        var go = new GameObject("BossShockwave");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.sortingOrder = 7;
        go.transform.localScale = Vector3.one * radius * 2f;

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
        go.transform.position = PointOnLoop(loop, startS);
    }

    void Update()
    {
        if (LevelEditor.IsEditing) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;                          // 一時停止中

        float step = speed * dt;
        s += dir * step;
        remaining -= step;
        if (remaining <= 0f) { Destroy(gameObject); return; }

        Vector2 pos = PointOnLoop(loop, s);
        transform.position = pos;

        // プレイヤーに当たったら、ダメージ(無敵時間は、PlayerHealth が管理する)
        if (playerCol != null && playerHealth != null)
        {
            Vector2 closest = playerCol.ClosestPoint(pos);
            if ((closest - pos).sqrMagnitude <= radius * radius)
            {
                int fromDir = playerCol.bounds.center.x >= pos.x ? 1 : -1;
                playerHealth.TakeDamage(damage, fromDir);
            }
        }
    }

    // 道の長さ
    public static float Perimeter(Rect r) => 2f * (r.width + r.height);

    // 道の上の位置(長さ)から、座標を求める。順番: 床(左→右) → 右の壁(下→上) → 天井(右→左) → 左の壁(上→下)
    public static Vector2 PointOnLoop(Rect r, float s)
    {
        float w = r.width, h = r.height, p = Perimeter(r);
        s = ((s % p) + p) % p;

        if (s < w) return new Vector2(r.xMin + s, r.yMin);          // 床
        s -= w;
        if (s < h) return new Vector2(r.xMax, r.yMin + s);          // 右の壁
        s -= h;
        if (s < w) return new Vector2(r.xMax - s, r.yMax);          // 天井
        s -= w;
        return new Vector2(r.xMin, r.yMax - s);                     // 左の壁
    }

    // 壁の番号(0=床、1=右、2=天井、3=左)と、その壁の上の座標から、道の上の位置を求める
    public static float ParamOnLoop(Rect r, int wall, Vector2 p)
    {
        float w = r.width, h = r.height;
        switch (wall)
        {
            case 0: return Mathf.Clamp(p.x - r.xMin, 0f, w);
            case 1: return w + Mathf.Clamp(p.y - r.yMin, 0f, h);
            case 2: return w + h + Mathf.Clamp(r.xMax - p.x, 0f, w);
            default: return 2f * w + h + Mathf.Clamp(r.yMax - p.y, 0f, h);
        }
    }
}
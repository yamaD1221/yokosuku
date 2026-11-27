using System.Collections.Generic;
using UnityEngine;

// 初期障害物の内側輪郭をつないだ経路に沿って移動する衝撃波。
public class BossShockwave : MonoBehaviour
{
    private static Sprite square;

    public static Sprite SquareSprite
    {
        get
        {
            if (square == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
            }
            return square;
        }
    }

    // 一周する折れ線経路と、各点までの累積距離。
    private Vector2[] path;
    private float[] cumulative;
    private float totalLength;
    private float distanceAlongPath;
    private int direction;
    private float speed;
    private float remaining;
    private float radius;
    private int damage;
    private Collider2D playerCol;
    private PlayerHealth playerHealth;

    // 見た目用：元の色と、火花を出す間隔の管理
    private Color baseColor;
    private float sparkTimer;

    // ギザギザ形の見た目（本体と芯）と、ちらつき・脈動の管理
    private SpriteRenderer bodySr;
    private SpriteRenderer coreSr;
    private Vector3 baseScale = Vector3.one;
    private float flickerTimer;
    private int flickerIndex;

    // 経路を指定して衝撃波を生成する。
    public static void Spawn(IList<Vector2> contour, float startDistance, int direction,
        float speed, float distance, float radius, int damage,
        Collider2D playerCol, PlayerHealth playerHealth, Color color)
    {
        if (contour == null || contour.Count < 3) return;

        GameObject go = new GameObject("BossShockwave");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BossFx.JaggedSprite(0);
        sr.color = color;
        sr.sortingOrder = 7;
        go.transform.localScale = new Vector3(radius * 1.5f, radius * 2.2f, 1f);

        // 中心に明るい芯を重ねる（炎の芯のイメージ）
        var core = new GameObject("WaveCore");
        core.transform.SetParent(go.transform, false);
        core.transform.localScale = Vector3.one * 0.55f;
        var coreSr = core.AddComponent<SpriteRenderer>();
        coreSr.sprite = BossFx.JaggedSprite(1);
        coreSr.color = new Color(1f, 0.85f, 0.4f, 0.95f);
        coreSr.sortingOrder = 8;

        // 後ろに色の残像（軌跡）を残す
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.startWidth = radius * 1.5f;
        trail.endWidth = 0f;
        trail.startColor = new Color(color.r, color.g, color.b, 0.45f);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.sortingOrder = 6;

        BossShockwave wave = go.AddComponent<BossShockwave>();
        wave.path = new Vector2[contour.Count];
        for (int i = 0; i < contour.Count; i++) wave.path[i] = contour[i];
        wave.BuildCumulativeLengths();
        wave.distanceAlongPath = Mathf.Repeat(startDistance, wave.totalLength);
        wave.direction = direction >= 0 ? 1 : -1;
        wave.speed = speed;
        wave.remaining = distance;
        wave.radius = radius;
        wave.damage = damage;
        wave.playerCol = playerCol;
        wave.playerHealth = playerHealth;
        wave.baseColor = color;
        wave.bodySr = sr;
        wave.coreSr = coreSr;
        wave.baseScale = go.transform.localScale;
        go.transform.position = wave.PointAtDistance(wave.distanceAlongPath);
    }

    private void BuildCumulativeLengths()
    {
        cumulative = new float[path.Length + 1];
        cumulative[0] = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[(i + 1) % path.Length];
            cumulative[i + 1] = cumulative[i] + Vector2.Distance(a, b);
        }
        totalLength = cumulative[path.Length];
    }

    private void Update()
    {
        if (LevelEditor.IsEditing) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        if (dt <= 0f || totalLength <= 0.001f) return;

        float step = speed * dt;
        distanceAlongPath = Mathf.Repeat(distanceAlongPath + direction * step, totalLength);
        remaining -= step;
        if (remaining <= 0f)
        {
            // 消える場所で小さく火花を散らす
            BossFx.SparkBurst(transform.position, baseColor, 6, 2.5f, 0.11f, 0.4f);
            Destroy(gameObject); return;
        }

        Vector2 pos = PointAtDistance(distanceAlongPath);
        transform.position = pos;

        // 進行方向を向く（経路の少し先の点との角度）
        Vector2 ahead = PointAtDistance(distanceAlongPath + direction * 0.15f);
        Vector2 moveDir = ahead - pos;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // 大きさをわずかに脈動させる
        float pulse = 1f + 0.13f * Mathf.Sin(Time.time * 22f);
        transform.localScale = baseScale * pulse;

        // ギザギザの形を切り替えて、火花がちらつくように見せる
        flickerTimer -= dt;
        if (flickerTimer <= 0f)
        {
            flickerTimer = 0.06f;
            flickerIndex = (flickerIndex + 1) % 3;
            if (bodySr != null) bodySr.sprite = BossFx.JaggedSprite(flickerIndex);
            if (coreSr != null) coreSr.sprite = BossFx.JaggedSprite(flickerIndex + 1);
        }

        // 進みながら火花をまき散らす
        sparkTimer -= dt;
        if (sparkTimer <= 0f)
        {
            sparkTimer = 0.07f;
            BossFx.SparkBurst(pos, new Color(1f, 0.5f, 0.18f), 1, 1.2f, 0.1f, 0.35f);
        }

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

    private Vector2 PointAtDistance(float distance)
    {
        if (path == null || path.Length == 0) return Vector2.zero;
        distance = Mathf.Repeat(distance, totalLength);

        int segment = 0;
        while (segment < path.Length - 1 && cumulative[segment + 1] < distance)
            segment++;

        float segmentLength = cumulative[segment + 1] - cumulative[segment];
        float t = segmentLength > 0.0001f
            ? (distance - cumulative[segment]) / segmentLength
            : 0f;
        Vector2 a = path[segment];
        Vector2 b = path[(segment + 1) % path.Length];
        return Vector2.Lerp(a, b, t);
    }

    // 経路全体の長さ。
    public static float PathLength(IList<Vector2> contour)
    {
        if (contour == null || contour.Count < 2) return 0f;
        float length = 0f;
        for (int i = 0; i < contour.Count; i++)
            length += Vector2.Distance(contour[i], contour[(i + 1) % contour.Count]);
        return length;
    }

    // 着地点から最も近い経路上の位置を、スタート距離として返す。
    public static float FindNearestDistance(IList<Vector2> contour, Vector2 position)
    {
        if (contour == null || contour.Count < 2) return 0f;

        float bestDistanceSqr = float.MaxValue;
        float bestAlong = 0f;
        float traversed = 0f;

        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 a = contour[i];
            Vector2 b = contour[(i + 1) % contour.Count];
            Vector2 segment = b - a;
            float lengthSqr = segment.sqrMagnitude;
            float t = lengthSqr > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(position - a, segment) / lengthSqr)
                : 0f;
            Vector2 nearest = a + segment * t;
            float sqr = (position - nearest).sqrMagnitude;
            float length = Mathf.Sqrt(lengthSqr);

            if (sqr < bestDistanceSqr)
            {
                bestDistanceSqr = sqr;
                bestAlong = traversed + length * t;
            }
            traversed += length;
        }
        return bestAlong;
    }
}

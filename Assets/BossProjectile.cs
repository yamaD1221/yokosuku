using UnityEngine;

// ボスの弾。壁や置いた足場に当たると消える(遮蔽物になる)。プレイヤーに当たるとダメージ
public class BossProjectile : MonoBehaviour
{
    private static Sprite square;

    private Vector2 velocity;
    private float radius = 0.3f;
    private int damage;
    private LayerMask blockMask;
    private Collider2D playerCol;
    private PlayerHealth playerHealth;
    private float life = 5f;

    public static BossProjectile Spawn(Vector2 pos, Vector2 vel, int damage, LayerMask blockMask,
        Collider2D playerCol, PlayerHealth playerHealth, Color color)
    {
        if (square == null)
        {
            var tex = Texture2D.whiteTexture;
            square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }

        var go = new GameObject("BossBullet");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.6f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = 6;

        var b = go.AddComponent<BossProjectile>();
        b.velocity = vel;
        b.damage = damage;
        b.blockMask = blockMask;
        b.playerCol = playerCol;
        b.playerHealth = playerHealth;
        return b;
    }

    void Update()
    {
        if (LevelEditor.IsEditing) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;                          // 一時停止中

        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector2 pos = transform.position;
        Vector2 step = velocity * dt;
        float dist = step.magnitude;

        // 壁や置いた足場に当たったら、消える
        if (dist > 0f)
        {
            RaycastHit2D hit = Physics2D.CircleCast(pos, radius, step / dist, dist, blockMask);
            if (hit.collider != null) { Destroy(gameObject); return; }
        }

        pos += step;
        transform.position = pos;

        // プレイヤーに当たったら、ダメージ
        if (playerCol != null && playerHealth != null)
        {
            Vector2 closest = playerCol.ClosestPoint(pos);
            if ((closest - pos).sqrMagnitude <= radius * radius)
            {
                playerHealth.TakeDamage(damage, velocity.x >= 0f ? 1 : -1);
                Destroy(gameObject);
            }
        }
    }
}
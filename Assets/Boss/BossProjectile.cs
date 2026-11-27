using UnityEngine;

// ボスの弾。壁や置いた足場に当たると消え、プレイヤーに当たるとダメージを与える
public class BossProjectile : MonoBehaviour
{
    // 弾の見た目に使う四角形のSprite
    // staticなので、複数の弾で同じSpriteを使い回す
    private static Sprite square;

    // 弾の移動速度
    private Vector2 velocity;

    // 弾の当たり判定の半径
    private float radius = 0.3f;

    // プレイヤーに与えるダメージ量
    private int damage;

    // 弾を消す対象のレイヤー
    private LayerMask blockMask;

    // プレイヤーのCollider
    private Collider2D playerCol;

    // プレイヤーの体力を管理するスクリプト
    private PlayerHealth playerHealth;

    // 弾が存在できる時間
    private float life = 5f;

    // 黄色い壁(NotStickWall)で跳ね返った弾かどうか（跳ね返しは1回だけ）
    private bool reflected;

    // 弾の見た目（跳ね返ったときに色を変えるために保持する）
    private SpriteRenderer sr;

    // 弾の後ろに残る光の軌跡
    private TrailRenderer trail;

    // 弾の周りの光（ハロー）。跳ね返ったときに色を変えるために保持する
    private SpriteRenderer glowSr;

    // Spawn：弾を生成して、位置・速度・ダメージなどを設定する
    public static BossProjectile Spawn(Vector2 pos, Vector2 vel, int damage, LayerMask blockMask,
        Collider2D playerCol, PlayerHealth playerHealth, Color color)
    {
        // Spriteがまだ作られていない場合は作成する
        if (square == null)
        {
            // 白いテクスチャを取得して、四角形のSpriteを作る
            var tex = Texture2D.whiteTexture;
            square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }

        // 弾のGameObjectを作成して、位置と大きさを設定する
        var go = new GameObject("BossBullet");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.6f;

        // SpriteRendererを追加して、弾の見た目を設定する
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = 6;       // 描画する順番を設定

        // 弾の周りに柔らかい光（ハロー）を付ける
        var glow = new GameObject("Glow");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localScale = Vector3.one * 2.4f;
        var glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = BossFx.GlowSprite;
        glowSr.color = new Color(color.r, color.g, color.b, 0.35f);
        glowSr.sortingOrder = 5;

        // 弾の後ろに光の軌跡を残す
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = 0.34f;
        trail.endWidth = 0f;
        trail.startColor = new Color(color.r, color.g, color.b, 0.5f);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.sortingOrder = 5;

        // 発射の瞬間に小さな火花を出す
        BossFx.SparkBurst(pos, color, 4, 2f, 0.1f, 0.3f);

        // BossProjectileを追加して、弾の情報を保存する
        var b = go.AddComponent<BossProjectile>();
        b.sr = sr;
        b.trail = trail;
        b.glowSr = glowSr;
        b.velocity = vel;
        b.damage = damage;
        b.blockMask = blockMask;
        b.playerCol = playerCol;
        b.playerHealth = playerHealth;

        return b;
    }

    // Update：毎フレーム実行。弾の移動・当たり判定・消滅処理を行う
    void Update()
    {
        // レベル編集中なら弾を削除する
        if (LevelEditor.IsEditing) { Destroy(gameObject); return; }

        // 1フレームに進む時間を取得
        float dt = Time.deltaTime;

        // 時間が進んでいない場合は処理を終了（一時停止中）
        if (dt <= 0f) return;

        // 残りの生存時間を減らす
        life -= dt;

        // 生存時間がなくなったら弾を削除する
        if (life <= 0f) { Destroy(gameObject); return; }

        // 見た目：弾を回転させ、大きさをわずかに脈動させる
        transform.Rotate(0f, 0f, 540f * dt);
        float pulse = 0.6f * (1f + 0.13f * Mathf.Sin(Time.time * 28f));
        transform.localScale = Vector3.one * pulse;

        // 現在の位置と、1フレームで移動する距離を計算
        Vector2 pos = transform.position;
        Vector2 step = velocity * dt;
        float dist = step.magnitude;       // 移動距離の長さ

        // 壁や置いた足場に当たったら弾を消す
        if (dist > 0f)
        {
            // CircleCast：弾の半径を使って移動先までの当たり判定を調べる
            // 高速で移動しても壁をすり抜けないようにする
            RaycastHit2D hit = Physics2D.CircleCast(pos, radius, step / dist, dist, blockMask);

            // 壁や足場に当たった場合の処理
            if (hit.collider != null)
            {
                // 黄色い壁(NotStickWall)なら、1回だけ反射角そのままに跳ね返す
                // 狙ってボスへ向かわせることはせず、置いた角度次第で当たるだけにする
                bool isNoStick = hit.collider.gameObject.layer ==
                    LayerMask.NameToLayer("NotStickWall");

                if (isNoStick && !reflected)
                {
                    reflected = true;
                    velocity = Vector2.Reflect(velocity, hit.normal);
                    transform.position = hit.point + hit.normal * (radius + 0.02f);

                    // 跳ね返ったことが分かるように色を変える
                    if (sr != null) sr.color = new Color(1f, 0.9f, 0.2f);
                    if (glowSr != null) glowSr.color = new Color(1f, 0.9f, 0.2f, 0.35f);
                    if (trail != null)
                    {
                        // 移動先が飛ぶので、軌跡がいったん切れるようにする
                        trail.Clear();
                        trail.startColor = new Color(1f, 0.9f, 0.2f, 0.5f);
                        trail.endColor = new Color(1f, 0.9f, 0.2f, 0f);
                    }

                    // 跳ね返った瞬間にも火花を出す
                    BossFx.SparkBurst(hit.point + hit.normal * 0.1f,
                        new Color(1f, 0.9f, 0.2f), 6, 2.5f, 0.1f, 0.35f);

                    return;
                }

                // それ以外の壁・足場に当たった場合と、2回目の反射は弾を削除する
                // 消える前に火花を散らす
                BossFx.SparkBurst(hit.point + hit.normal * 0.1f,
                    reflected ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.55f, 0.15f),
                    8, 3f, 0.11f, 0.4f);
                Destroy(gameObject); return;
            }
        }

        // 当たり判定に問題がなければ弾を移動させる
        pos += step;
        transform.position = pos;

        // 跳ね返った弾はプレイヤーではなくボスに当たる
        if (reflected)
        {
            var boss = FrameBoss.Instance;
            if (boss != null)
            {
                var bossCol = boss.GetComponent<Collider2D>();
                if (bossCol != null)
                {
                    Vector2 bossClosest = bossCol.ClosestPoint(pos);
                    if ((bossClosest - pos).sqrMagnitude <= radius * radius)
                    {
                        boss.TakeDamage(damage, velocity.x >= 0f ? 1 : -1);
                        BossFx.SparkBurst(pos, new Color(1f, 0.9f, 0.2f), 10, 3.5f, 0.12f, 0.45f);
                        Destroy(gameObject);
                    }
                }
            }
            return;
        }

        // プレイヤーに当たったらダメージを与える
        if (playerCol != null && playerHealth != null)
        {
            // ClosestPoint：プレイヤーのCollider上で、弾に最も近い位置を取得する
            Vector2 closest = playerCol.ClosestPoint(pos);

            // 弾とプレイヤーの最短距離が弾の半径以内なら当たったと判定する
            // sqrMagnitudeは平方根を使わずに距離の2乗を計算する
            if ((closest - pos).sqrMagnitude <= radius * radius)
            {
                // プレイヤーにダメージを与える
                // 弾の進行方向に応じて、吹き飛ばす方向を決める
                playerHealth.TakeDamage(damage, velocity.x >= 0f ? 1 : -1);

                // 当たった瞬間に火花を散らす
                BossFx.SparkBurst(pos, new Color(1f, 0.4f, 0.2f), 10, 3.5f, 0.12f, 0.45f);

                // プレイヤーに当たったら弾を削除する
                Destroy(gameObject);
            }
        }
    }
}
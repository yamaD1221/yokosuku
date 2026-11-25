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

        // BossProjectileを追加して、弾の情報を保存する
        var b = go.AddComponent<BossProjectile>();
        b.sr = sr;
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

                    return;
                }

                // それ以外の壁・足場に当たった場合と、2回目の反射は弾を削除する
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

                // プレイヤーに当たったら弾を削除する
                Destroy(gameObject);
            }
        }
    }
}
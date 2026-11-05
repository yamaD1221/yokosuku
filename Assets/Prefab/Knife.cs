using UnityEngine;

// 投げられたナイフの移動・衝突・敵への攻撃・足場としての機能を管理するクラス
[RequireComponent(typeof(BoxCollider2D))]
public class Knife : MonoBehaviour
{
    // ナイフが飛ぶ速度
    [SerializeField] private float speed = 20f;

    // ナイフが飛び続ける最大時間
    [SerializeField] private float maxFlyTime = 0.6f;

    // 壁に食い込ませる深さ
    [SerializeField] private float embedDepth = 0.25f;

    // ナイフが刺さる対象のレイヤー
    [SerializeField] private LayerMask stickLayer;

    // ダメージを与える対象のレイヤー
    [SerializeField] private LayerMask enemyLayer;

    // 切断できるロープのレイヤー
    [SerializeField] private LayerMask ropeLayer;

    // ナイフが与えるダメージ量
    [SerializeField] private int damage = 1;

    // ナイフが刺さらずに跳ね返る対象のレイヤー
    [SerializeField] private LayerMask blockLayer;

    // 刺さってから消えるまでの時間
    [SerializeField] private float stuckLifetime = 5f;

    // 消える直前に点滅する時間
    [SerializeField] private float blinkDuration = 1f;

    // ナイフの物理挙動を管理するRigidbody2D
    private Rigidbody2D rb;

    // ナイフの当たり判定
    private Collider2D col;

    // ナイフが何かに刺さっているか
    private bool stuck;

    // ナイフが刺さった時刻
    private float stuckAt;

    // ナイフの見た目を管理するSpriteRenderer
    private SpriteRenderer sr;

    // 敵やロープに当たって役目を終えたか
    private bool finished;

    // ナイフが飛んでいる方向
    private Vector2 dir = Vector2.right;

    // ナイフが飛び終わる時刻
    private float deadline;

    // ナイフの長さの半分
    private float halfLength;

    // 衝突判定に使用するレイヤーをまとめたもの
    private int hitMask;

    // ナイフを足場にするためのPlatformEffector2D
    private PlatformEffector2D platformEffector;

    // オブジェクト生成時の初期化
    void Awake()
    {
        // 子オブジェクトも含めてSpriteRendererを取得する
        sr = GetComponentInChildren<SpriteRenderer>();

        // Rigidbody2Dを取得する
        rb = GetComponent<Rigidbody2D>();

        // BoxCollider2Dを取得する
        var box = GetComponent<BoxCollider2D>();
        col = box;

        // ナイフの長さの半分を計算する
        // オブジェクトの拡大率も考慮する
        halfLength = box.size.x * Mathf.Abs(transform.localScale.x) * 0.5f;

        // 衝突判定に使用するレイヤーをまとめる
        hitMask = stickLayer.value | enemyLayer.value | ropeLayer.value | blockLayer.value;

        // PlatformEffector2Dを取得する
        platformEffector = GetComponent<PlatformEffector2D>();
    }

    // ナイフを指定した方向に飛ばす
    // direction：飛ばす方向
    // castOrigin：最初の衝突判定を行う位置
    public void Launch(Vector2 direction, Vector2 castOrigin)
    {
        // 飛ばす方向を正規化する
        dir = direction.normalized;

        // 方向がほぼゼロの場合は右向きにする
        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.right;

        // ナイフの向きを飛ぶ方向に合わせる
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // プレイヤーからナイフの先端までの距離を計算する
        // プレイヤーに密着した状態で投げても、すぐに衝突判定できるようにする
        float reach = Vector2.Distance(castOrigin, rb.position) + halfLength;

        // プレイヤーの位置からナイフの進行方向にRaycastを飛ばす
        RaycastHit2D hit = Physics2D.Raycast(castOrigin, dir, reach, hitMask);

        // 最初から何かに当たっている場合は衝突処理を行う
        if (hit.collider != null)
        {
            Resolve(hit);
            return;
        }

        // ナイフに重力がかからないようにする
        rb.gravityScale = 0f;

        // 指定した方向と速度でナイフを飛ばす
        rb.linearVelocity = dir * speed;

        // ナイフが飛び終わる時刻を設定する
        deadline = Time.time + maxFlyTime;
    }

    // 毎フレーム実行される
    void Update()
    {
        // 飛行時間を超えた場合は飛行終了とする
        if (!stuck && !finished && Time.time > deadline)
        {
            //Destroy(gameObject);
            return;
        }

        // 刺さっていない場合は、ここで処理を終了する
        if (!stuck) return;

        // 刺さってから現在までの経過時間を引き、残り時間を計算する
        float remaining = stuckLifetime - (Time.time - stuckAt);

        // 残り時間がなくなったらナイフを削除する
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 消える直前はナイフを点滅させる
        // 点滅中も足場として使用できる
        if (sr != null && remaining <= blinkDuration)
            sr.enabled = ((int)(Time.time * 12f)) % 2 == 0;
    }

    // 一定間隔で実行される物理処理
    void FixedUpdate()
    {
        // 刺さっている、または役目を終えている場合は処理しない
        if (stuck || finished) return;

        // 1回の物理更新でナイフが進む距離を計算する
        float dist = speed * Time.fixedDeltaTime;

        // ナイフの先端位置を計算する
        Vector2 tip = rb.position + dir * halfLength;

        // ナイフの先端から進行方向にRaycastを飛ばす
        RaycastHit2D hit = Physics2D.Raycast(
            tip,
            dir,
            dist,
            hitMask
        );

        // 何かに当たった場合は衝突処理を行う
        if (hit.collider != null)
            Resolve(hit);
    }

    // 衝突した対象に応じて処理を分ける
    void Resolve(RaycastHit2D hit)
    {
        // 衝突したオブジェクトのレイヤーをビット形式で取得する
        int bit = 1 << hit.collider.gameObject.layer;

        // ロープに当たった場合
        if ((ropeLayer.value & bit) != 0)
        {
            // ナイフの役目を終了する
            finished = true;

            // 親オブジェクトも含めてRopeを取得する
            var rope = hit.collider.GetComponentInParent<Rope>();

            // ロープが存在する場合は切断する
            if (rope != null) rope.Cut();

            // ナイフを削除する
            Destroy(gameObject);
        }
        // 敵に当たった場合
        else if ((enemyLayer.value & bit) != 0)
        {
            // ナイフの役目を終了する
            finished = true;

            // 親オブジェクトも含めてEnemyを取得する
            var enemy = hit.collider.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                // 敵にダメージを与える
                // ナイフの飛んだ方向によって攻撃方向を決める
                enemy.TakeDamage(damage, dir.x >= 0 ? 1 : -1);
            }
            else
            {
                // Enemyがない場合はIDamageableを取得する
                // ボスなど、別のダメージ対象にも対応する
                var target = hit.collider.GetComponentInParent<IDamageable>();

                if (target != null)
                {
                    // 対象にダメージを与える
                    target.TakeDamage(damage, dir.x >= 0 ? 1 : -1);
                }
            }

            // 敵に当たったナイフを削除する
            Destroy(gameObject);
        }
        // ナイフが刺さらない壁に当たった場合
        else if ((blockLayer.value & bit) != 0)
        {
            // ナイフの役目を終了する
            finished = true;

            // 重力を有効にする
            rb.gravityScale = 3f;

            // 反対方向に跳ね返し、上方向にも飛ばす
            rb.linearVelocity = -dir * 4f + Vector2.up * 5f;

            // 0.5秒後にナイフを削除する
            Destroy(gameObject, 0.5f);
        }
        // それ以外の対象に当たった場合
        else
        {
            // 衝突位置にナイフを埋め込む
            EmbedAt(hit.point);
        }
    }

    // 衝突した位置にナイフを埋め込む
    void EmbedAt(Vector2 surface)
    {
        // 衝突面からナイフの長さを考慮して生成位置を計算する
        // embedDepthの分だけ壁に食い込ませる
        Vector2 pos = surface - dir * (halfLength - embedDepth);

        // ナイフの位置を変更する
        transform.position = new Vector3(
            pos.x,
            pos.y,
            transform.position.z
        );

        // Rigidbody2Dの位置も更新する
        rb.position = pos;

        // ナイフを固定する
        Stick();
    }

    // ナイフを足場として固定する
    void Stick()
    {
        // 刺さっている状態にする
        stuck = true;

        // 刺さった時刻を記録する
        stuckAt = Time.time;

        // ナイフの移動と回転を停止する
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // 当たり判定を通常の衝突判定に変更する
        col.isTrigger = false;

        // PlatformEffector2Dを使用する設定にする
        col.usedByEffector = true;

        // 足場の判定方向を画面上方向に固定する
        if (platformEffector != null)
        {
            // 一方向からのみ乗れる足場にする
            platformEffector.useOneWay = true;

            // ナイフの回転角度に合わせて足場の向きを調整する
            platformEffector.rotationalOffset = -transform.eulerAngles.z;
        }

        // 回転する箱の管理オブジェクトを取得する
        var arena = ArenaRotator.Instance;

        if (arena != null)
        {
            // 箱と一緒に動くようにナイフを箱の子オブジェクトにする
            transform.SetParent(arena.transform, true);

            // 物理的な移動と回転を停止する
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // Rigidbody2DをKinematicにして物理演算による移動を防ぐ
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            // 箱がない場合はStaticにして完全に固定する
            rb.bodyType = RigidbodyType2D.Static;
        }
    }
}
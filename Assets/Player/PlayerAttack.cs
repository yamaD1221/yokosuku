using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーの近接攻撃を管理するクラス
// 敵へのダメージ処理と、攻撃範囲内にあるロープの切断を行う
public class PlayerAttack : MonoBehaviour
{
    // プレイヤーの移動や向きを管理するPlayerControllerへの参照
    [SerializeField] private PlayerController player;

    // プレイヤーの位置から攻撃範囲の中心までのオフセット
    // x：前方への距離、y：上下方向への距離
    [SerializeField] private Vector2 attackOffset = new Vector2(0.8f, 0f);

    // 近接攻撃が当たる範囲の幅と高さ
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 0.8f);

    // 攻撃対象となる敵のLayer
    [SerializeField] private LayerMask enemyLayer;

    // 次の攻撃が可能になるまでの待ち時間（秒）
    [SerializeField] private float cooldown = 0.3f;

    // 近接攻撃で与えるダメージ量
    [SerializeField] private int damage = 1;

    // 攻撃によって切断するロープのLayer
    [SerializeField] private LayerMask ropeLayer;

    // 次に攻撃できるようになるゲーム内時刻
    private float nextAttackTime;

    // オブジェクト生成時に1回実行される
    void Awake()
    {
        // InspectorでPlayerが設定されていない場合、
        // 同じGameObjectに付いているPlayerControllerを取得する
        if (player == null)
            player = GetComponent<PlayerController>();
    }

    // 毎フレーム実行される
    void Update()
    {
        // 編集モード中、または結果画面表示中は攻撃できないようにする
        if (LevelEditor.IsEditing || RunController.IsResultShowing)
            return;

        // マウスの左クリックが押された瞬間、
        // かつクールダウンが終了している場合に攻撃する
        if (Mouse.current.leftButton.wasPressedThisFrame &&
            Time.time >= nextAttackTime)
        {
            // 次に攻撃できる時刻を設定する
            nextAttackTime = Time.time + cooldown;

            // 近接攻撃を実行する
            Attack();
        }
    }

    // 近接攻撃の処理
    void Attack()
    {
        // 近接攻撃を使用した回数をRunRecorderに記録する
        // 星3の「近接攻撃を使わない」という条件の判定に使用する
        RunRecorder.MeleeUsed();

        // PlayerControllerから現在の攻撃方向を取得する
        Vector2 direction = player.attackDirection;

        // ---------- 攻撃範囲の位置を計算 ----------

        // プレイヤーの位置を基準に、攻撃方向へ攻撃範囲の中心を移動させる
        // attackOffset.xの値だけ前方に攻撃範囲を配置する
        Vector2 center = (Vector2)transform.position
            + direction * attackOffset.x;

        // 攻撃方向から角度を計算する
        // Atan2で方向をラジアンから取得し、Rad2Degで度数法に変換する
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // ---------- 敵への攻撃判定 ----------

        // 指定した位置・大きさ・角度の四角形で当たり判定を行う
        // enemyLayerに設定されたLayerのオブジェクトだけを取得する
        var hits = Physics2D.OverlapBoxAll(
            center,
            attackSize,
            angle,
            enemyLayer
        );

        // 攻撃範囲内で検出したオブジェクトを1つずつ確認する
        foreach (var h in hits)
        {
            // 検出したオブジェクト自身にEnemyが付いているか確認する
            var enemy = h.GetComponent<Enemy>();

            if (enemy != null)
            {
                // Enemyが見つかった場合、ダメージを与える
                // player.Facingでプレイヤーの向きも渡す
                enemy.TakeDamage(damage, player.Facing);

                // 攻撃が当たった場所に火花を発生させる
                HitSparkEffect.Play(h.ClosestPoint(center), direction);

                // このオブジェクトの処理を終了し、次の判定へ進む
                continue;
            }

            // Enemyが付いていない場合、
            // 親オブジェクトも含めてIDamageableを実装したコンポーネントを探す
            var target = h.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                // IDamageableを実装した対象にダメージを与える
                target.TakeDamage(damage, player.Facing);
                // 攻撃が当たった場所に火花を発生させる
                HitSparkEffect.Play(h.ClosestPoint(center), direction);
            }
        }
       
    }

    // 選択中のオブジェクトの攻撃範囲をSceneビューに表示する
    // ゲーム実行中の攻撃範囲の調整や確認に使用する
    void OnDrawGizmosSelected()
    {
        // プレイヤーの向きを取得する
        // PlayerControllerがない場合は右向き（1）とする
        int f = player != null ? player.Facing : 1;

        // Gizmoの色を赤色に設定する
        Gizmos.color = Color.red;

        // プレイヤーの位置とオフセットから攻撃範囲を計算し、
        // 指定した大きさのワイヤーフレームの四角形を表示する
        Gizmos.DrawWireCube(
            (Vector2)transform.position +
            new Vector2(attackOffset.x * f, attackOffset.y),
            attackSize
        );
    }
}
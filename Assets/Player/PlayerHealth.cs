using UnityEngine;
using UnityEngine.SceneManagement;

// プレイヤーの体力・ダメージ・無敵時間・死亡処理を管理するクラス
public class PlayerHealth : MonoBehaviour
{
    // プレイヤーの最大HP
    [SerializeField] private int maxHp = 5;

    // ダメージを受けた後の無敵時間（秒）
    [SerializeField] private float invincibleTime = 1f;

    // ダメージを受けた後のスタン時間（秒）
    [SerializeField] private float stunTime = 0.25f;

    // ダメージを受けたときのノックバックの強さ
    // x：横方向、y：上方向
    [SerializeField] private Vector2 knockback = new Vector2(6f, 5f);

    // 現在のHP
    private int hp;

    // 無敵状態が終了するゲーム内時刻
    private float invincibleUntil;

    // スタン状態が終了するゲーム内時刻
    private float stunUntil;

    // プレイヤーが死亡しているかどうか
    private bool dead;

    // プレイヤーの物理挙動を管理するRigidbody2D
    private Rigidbody2D rb;

    // プレイヤーの見た目を管理するSpriteRenderer
    private SpriteRenderer sr;

    // 現在スタン状態かどうかを取得する
    // 現在時刻がスタン終了時刻より前ならtrue
    public bool IsStunned => Time.time < stunUntil;

    // 現在のHPを外部から取得する
    public int Hp => hp;

    // 最大HPを外部から取得する
    public int MaxHp => maxHp;

    // HPが変更されたときに呼び出されるイベント
    // 引数は現在のHPと最大HP
    public event System.Action<int, int> OnHealthChanged;

    // オブジェクト生成時の初期化
    void Awake()
    {
        // HPを最大HPに設定する
        hp = maxHp;

        // Rigidbody2Dを取得する
        rb = GetComponent<Rigidbody2D>();

        // 子オブジェクトも含めてSpriteRendererを取得する
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    // 毎フレーム実行される
    void Update()
    {
        // 無敵中はプレイヤーを点滅させる
        if (sr == null) return;

        // 現在無敵状態かどうかを判定する
        bool invincible = Time.time < invincibleUntil;

        // 現在のスプライトの色を取得する
        var c = sr.color;

        // 無敵中は一定間隔で透明度を下げて点滅させる
        // それ以外のときは通常の透明度に戻す
        c.a = (invincible && ((int)(Time.time * 15f) % 2 == 0))
            ? 0.3f
            : 1f;

        // 変更した色をスプライトに反映する
        sr.color = c;
    }

    // ダメージを受けたときの処理
    // damage：受けるダメージ量
    // fromDir：攻撃してきた方向
    public void TakeDamage(int damage, int fromDir)
    {
        // 死亡中、または無敵時間中はダメージを受けない
        if (dead || Time.time < invincibleUntil) return;

        // HPを減らす
        hp -= damage;

        // HP変更イベントを呼び出す
        // HPが0未満の場合は0として通知する
        OnHealthChanged?.Invoke(Mathf.Max(hp, 0), maxHp);

        // 現在のHPをConsoleに表示する
        Debug.Log($"Player HP: {hp}/{maxHp}");

        // 次にダメージを受けるまでの無敵時間を設定する
        invincibleUntil = Time.time + invincibleTime;

        // スタン状態が終了する時刻を設定する
        stunUntil = Time.time + stunTime;

        // 攻撃してきた方向と反対方向にノックバックさせる
        // 横方向の速度と上方向の速度を設定する
        rb.linearVelocity = new Vector2(
            fromDir * knockback.x,
            knockback.y
        );

        // HPが0以下になった場合は死亡処理を行う
        if (hp <= 0)
        {
            // 死亡状態にする
            dead = true;

            // スタン終了時刻を最大値にして操作できない状態にする
            stunUntil = float.MaxValue;

            // プレイヤーの死亡回数をRunRecorderに記録する
            RunRecorder.PlayerDied();

            // 1秒後にRestartメソッドを実行する
            Invoke(nameof(Restart), 1f);
        }
    }

    // プレイヤーの状態を初期状態に戻す
    public void ResetState()
    {
        // 登録されているInvokeをすべてキャンセルする
        CancelInvoke();

        // 死亡状態を解除する
        dead = false;

        // HPを最大値に戻す
        hp = maxHp;

        // 無敵時間とスタン時間をリセットする
        invincibleUntil = 0f;
        stunUntil = 0f;

        // HP変更イベントを呼び出して最新のHPを通知する
        OnHealthChanged?.Invoke(hp, maxHp);
    }

    // 死亡後に結果画面を表示する
    void Restart()
    {
        // RunControllerが存在する場合は、死亡による挑戦終了処理を行う
        if (RunController.Instance != null)
            RunController.Instance.EndRun(false);

        // RunControllerがない場合は、プレイを再スタートする
        else if (LevelEditor.Instance != null)
            LevelEditor.Instance.RestartPlay();

        // どちらも存在しない場合は、現在のシーンを再読み込みする
        else
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
    }
}
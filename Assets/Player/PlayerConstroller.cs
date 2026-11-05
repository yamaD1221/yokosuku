using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーの移動・ジャンプ・向きを管理するクラス
// マウスの位置から攻撃方向も計算し、PlayerAttackに渡す
public class PlayerController : MonoBehaviour
{
    // プレイヤーのHPやスタン状態を管理するPlayerHealthへの参照
    PlayerHealth health;

    // プレイヤーの物理的な移動を管理するRigidbody2Dへの参照
    Rigidbody2D rb;

    // プレイヤーの通常移動速度
    public float moveSpeed = 3.5f;

    // ジャンプしたときに与える上方向の速度
    public float jumpPower = 15f;

    // プレイヤーが現在向いている方向
    // 1 = 右向き、-1 = 左向き
    // 外部から読み取り可能だが、変更できるのはこのクラス内だけ
    public int Facing { get; private set; } = 1;

    // プレイヤーの攻撃方向
    // マウスの方向を基準に更新する
    // 初期状態では右方向を向いている
    public Vector2 attackDirection { get; private set; } = Vector2.right;

    // 現在のジャンプ回数
    int jumpCount = 0;

    // 最大ジャンプ回数
    // 初期値が2なので、空中でもう1回ジャンプできる
    int maxJumpCount = 2;

    // オブジェクト生成後の初期化処理
    void Start()
    {
        // 同じGameObjectに付いているRigidbody2Dを取得する
        rb = GetComponent<Rigidbody2D>();

        // 同じGameObjectに付いているPlayerHealthを取得する
        health = GetComponent<PlayerHealth>();
    }

    // 毎フレーム実行される
    void Update()
    {
        // 編集モード中、または結果画面表示中は操作を受け付けない
        if (LevelEditor.IsEditing || RunController.IsResultShowing)
            return;

        // 移動入力の取得

        // キーボード入力から画面上の移動方向を取得する
        // Dキー：右方向、Aキー：左方向
        // Wキー：上方向、Sキー：下方向
        // 押されていない方向は0になる
        Vector2 screenInput = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1f : 0f) -
            (Keyboard.current.aKey.isPressed ? 1f : 0f),

            (Keyboard.current.wKey.isPressed ? 1f : 0f) -
            (Keyboard.current.sKey.isPressed ? 1f : 0f)
        );

        //  移動方向の変換 

        // 回転する箱が存在する場合は、
        // 箱の向きに合わせて画面上の入力方向をローカル方向に変換する
        // 変換後のX方向の値を-1～1の範囲に制限する
        // 箱が存在しない場合は、そのまま画面上のX方向を使用する
        float x = ArenaRotator.Instance != null
            ? Mathf.Clamp(
                ArenaRotator.Instance.ScreenToLocal(screenInput).x,
                -1f,
                1f
            )
            : screenInput.x;

        // キャラクターの向きの更新

        // 左右の移動入力が一定以上ある場合に向きを変更する
        // 右方向に移動すると右向き、左方向に移動すると左向きになる
        // 0.1f未満の入力では向きを変更しない
        if (Mathf.Abs(x) > 0.1f)
        {
            Facing = x > 0f ? 1 : -1;
        }

        // マウス位置の取得

        // マウスのスクリーン座標を取得する
        Vector3 mousePos = Mouse.current.position.ReadValue();

        // カメラから見た奥行きを設定する
        // プレイヤーがいるワールド座標の平面に合わせる
        mousePos.z = -Camera.main.transform.position.z;

        // マウスのスクリーン座標をワールド座標に変換する
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // プレイヤーの位置からマウスの位置までの方向ベクトルを計算する
        Vector2 mouseDiff =
            (Vector2)worldPos - (Vector2)transform.position;

        // 攻撃方向の更新

        // マウスとプレイヤーの位置がほぼ同じでなければ、
        // マウス方向のベクトルを正規化して攻撃方向として保存する
        // 正規化することで、方向の長さが1になる
        if (mouseDiff.sqrMagnitude > 0.001f)
        {
            attackDirection = mouseDiff.normalized;
        }

        // スタン状態の確認

        // PlayerHealthが存在し、スタン状態であればtrueになる
        // PlayerHealthが存在しない場合はfalseになる
        bool stunned = health != null && health.IsStunned;

        // プレイヤーの移動

        // スタン状態でなければ通常移動を行う
        if (!stunned)
        {
            // 横方向の速度を入力と移動速度から設定する
            // 縦方向の速度は現在の値を維持する
            // これにより、ジャンプ中の落下速度などが保持される
            rb.linearVelocity = new Vector2(
                x * moveSpeed,
                rb.linearVelocity.y
            );
        }

        // ジャンプ処理

        // スタン状態でなく、スペースキーが押された瞬間にジャンプ判定を行う
        if (!stunned && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // 現在のジャンプ回数が最大回数未満の場合だけジャンプできる
            if (jumpCount < maxJumpCount)
            {
                // 横方向の速度を維持したまま、上方向にジャンプ速度を与える
                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x,
                    jumpPower
                );

                // ジャンプ回数を1回増やす
                jumpCount++;
            }
        }
    }

    // 状態のリセット

    // プレイヤーの状態を初期状態に戻す
    // ステージの再挑戦やリトライ時などに使用する
    public void ResetState()
    {
        // ジャンプ回数を0に戻す
        jumpCount = 0;

        // プレイヤーの向きを右向きに戻す
        Facing = 1;
    }

    //  着地判定

    // プレイヤーが他のColliderと衝突したときに呼び出される
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 衝突したオブジェクトのタグを確認する
        // Ground：通常の床
        // InitialObj：初期配置されたオブジェクト
        // EditableObj：編集可能な配置オブジェクト
        if (collision.gameObject.CompareTag("Ground") ||
            collision.gameObject.CompareTag("InitialObj") ||
            collision.gameObject.CompareTag("EditableObj"))
        {
            // 対象物に着地したのでジャンプ回数をリセットする
            // これにより、再び最大回数までジャンプできる
            jumpCount = 0;

            // ジャンプ回数がリセットされたことをConsoleに表示する
            Debug.Log("ジャンプ回数をリセットしました");
        }
    }
}
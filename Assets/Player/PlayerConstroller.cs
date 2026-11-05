using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    PlayerHealth health;
    Rigidbody2D rb;

    public float moveSpeed = 3.5f;
    public float jumpPower = 15f;

    public int Facing { get; private set; } = 1;   // 1=右, -1=左
    public Vector2 attackDirection { get; private set; } = Vector2.right;

    int jumpCount = 0;
    int maxJumpCount = 2;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // 画面上の入力を取得
        Vector2 screenInput = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
            (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f)
        );

        // 箱の向きに合わせて移動方向を変換
        float x = ArenaRotator.Instance != null
            ? Mathf.Clamp(ArenaRotator.Instance.ScreenToLocal(screenInput).x, -1f, 1f)
            : screenInput.x;

        // 移動方向に合わせてキャラクターの向きを更新
        if (Mathf.Abs(x) > 0.1f)
        {
            Facing = x > 0f ? 1 : -1;
        }

        // マウス座標をワールド座標に変換
        Vector3 mousePos = Mouse.current.position.ReadValue();
        mousePos.z = -Camera.main.transform.position.z;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        Vector2 mouseDiff = (Vector2)worldPos - (Vector2)transform.position;

        // マウス方向を攻撃方向として保存
        if (mouseDiff.sqrMagnitude > 0.001f)
        {
            attackDirection = mouseDiff.normalized;
        }

        bool stunned = health != null && health.IsStunned;

        if (!stunned)
        {
            rb.linearVelocity = new Vector2(
                x * moveSpeed,
                rb.linearVelocity.y
            );
        }

        // ジャンプ
        if (!stunned && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (jumpCount < maxJumpCount)
            {
                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x,
                    jumpPower
                );

                jumpCount++;
            }
        }
    }

    public void ResetState()
    {
        jumpCount = 0;
        Facing = 1;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground") ||
            collision.gameObject.CompareTag("InitialObj") ||
            collision.gameObject.CompareTag("EditableObj"))
        {
            jumpCount = 0;
            Debug.Log("ジャンプ回数をリセットしました");
        }
    }
}
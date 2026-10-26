using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    PlayerHealth health;
    Rigidbody2D rb;

    public float moveSpeed = 3.5f;
    public float jumpPower = 15f;

    public int Facing { get; private set; } = 1;   // 1=右, -1=左

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

        // 画面上の入力(A/D=左右、W/S=上下)を、箱の向きに合わせて、箱の中の左右の移動に読み替える
        Vector2 screenInput = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
            (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f));

        float x = ArenaRotator.Instance != null
            ? Mathf.Clamp(ArenaRotator.Instance.ScreenToLocal(screenInput).x, -1f, 1f)
            : screenInput.x;

        if (Mathf.Abs(x) > 0.1f) Facing = x > 0f ? 1 : -1;

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
        if (collision.gameObject.CompareTag("Ground"))
        {
            jumpCount = 0;
        }
    }
}
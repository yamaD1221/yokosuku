using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    PlayerHealth health;
    Rigidbody2D rb;

    public float moveSpeed = 3.5f;
    public float jumpPower = 15f;

    public int Facing { get; private set; } = 1;   // 1=âE, -1=ç∂

    int jumpCount = 0;
    int maxJumpCount = 2;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        float x = 0;

        if (Keyboard.current.aKey.isPressed)
        {
            x = -1;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            x = 1;
        }

        if (x != 0) Facing = (int)x;

        bool stunned = health != null && health.IsStunned;

        if (!stunned)
        {
            rb.linearVelocity = new Vector2(
                x * moveSpeed,
                rb.linearVelocity.y
            );
        }

        // ÉWÉÉÉìÉv
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            jumpCount = 0;
        }
    }
}
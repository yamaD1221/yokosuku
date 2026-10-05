using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    Rigidbody2D rb;

    public float moveSpeed = 3.5f;
    public float jumpPower = 500f;

    int jumpCount = 0;
    int maxJumpCount = 2;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
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

        rb.linearVelocity = new Vector2(
            x * moveSpeed,
            rb.linearVelocity.y
        );

        // ƒWƒƒƒ“ƒv
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (jumpCount < maxJumpCount)
            {
                rb.linearVelocity = new Vector2(
                    rb.linearVelocity.x * 0.5f,
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
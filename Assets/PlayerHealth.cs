using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 5;
    [SerializeField] private float invincibleTime = 1f;
    [SerializeField] private float stunTime = 0.25f;
    [SerializeField] private Vector2 knockback = new Vector2(6f, 5f);

    private int hp;
    private float invincibleUntil;
    private float stunUntil;
    private bool dead;
    private Rigidbody2D rb;
    private SpriteRenderer sr;

    public bool IsStunned => Time.time < stunUntil;

    public int Hp => hp;
    public int MaxHp => maxHp;
    public event System.Action<int, int> OnHealthChanged;   // (Œ»İ‚Ì‘Ì—Í, Å‘å‘Ì—Í)

    void Awake()
    {
        hp = maxHp;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        // –³“G’†‚Í“_–Å
        if (sr == null) return;
        bool invincible = Time.time < invincibleUntil;
        var c = sr.color;
        c.a = (invincible && ((int)(Time.time * 15f) % 2 == 0)) ? 0.3f : 1f;
        sr.color = c;
    }

    public void TakeDamage(int damage, int fromDir)
    {
        if (dead || Time.time < invincibleUntil) return;

        hp -= damage;
        OnHealthChanged?.Invoke(Mathf.Max(hp, 0), maxHp);
        Debug.Log($"Player HP: {hp}/{maxHp}");
        invincibleUntil = Time.time + invincibleTime;
        stunUntil = Time.time + stunTime;
        rb.linearVelocity = new Vector2(fromDir * knockback.x, knockback.y);

        if (hp <= 0)
        {
            dead = true;
            stunUntil = float.MaxValue;     // €–SŒã‚Í‘€ì‚Å‚«‚È‚¢
            Invoke(nameof(Restart), 1f);
        }
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
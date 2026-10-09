using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Vector2 attackOffset = new Vector2(0.8f, 0f);
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 0.8f);
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float cooldown = 0.3f;
    [SerializeField] private int damage = 1;

    private float nextAttackTime;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + cooldown;
            Attack();
        }
    }

    void Attack()
    {
        Vector2 center = (Vector2)transform.position
            + new Vector2(attackOffset.x * player.Facing, attackOffset.y);

        var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f, enemyLayer);
        foreach (var h in hits)
        {
            var enemy = h.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(damage, player.Facing);
        }
    }

    // シーンビューで攻撃範囲を確認するため
    void OnDrawGizmosSelected()
    {
        int f = player != null ? player.Facing : 1;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            (Vector2)transform.position + new Vector2(attackOffset.x * f, attackOffset.y),
            attackSize);
    }
}
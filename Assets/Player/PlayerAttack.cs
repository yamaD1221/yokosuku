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
    [SerializeField] private LayerMask ropeLayer;
    private float nextAttackTime;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + cooldown;
            Attack();
        }
    }

    void Attack()
    {
        RunRecorder.MeleeUsed();

        Vector2 direction = player.attackDirection;

        // 攻撃範囲をマウス方向へ移動
        Vector2 center = (Vector2)transform.position
            + direction * attackOffset.x;

        // 攻撃範囲をマウス方向へ回転
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        var hits = Physics2D.OverlapBoxAll(
            center,
            attackSize,
            angle,
            enemyLayer
        );

        foreach (var h in hits)
        {
            var enemy = h.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, player.Facing);
                continue;
            }

            var target = h.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage, player.Facing);
            }
        }

        // 攻撃範囲内のロープを切断
        var ropeHits = Physics2D.OverlapBoxAll(
            center,
            attackSize,
            angle,
            ropeLayer
        );

        foreach (var h in ropeHits)
        {
            var rope = h.GetComponentInParent<Rope>();
            if (rope != null)
            {
                rope.Cut();
            }
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
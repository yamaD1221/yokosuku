// ダメージを受けられる物の共通の入口(ボスなど)
public interface IDamageable
{
    void TakeDamage(int damage, int fromDir);
}
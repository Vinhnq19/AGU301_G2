namespace DungeonBuilder.Core.Interfaces
{
    public interface IDamageable
    {
        void TakeDamage(float amount, ulong attackerClientId = 0);
    }
}

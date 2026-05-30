namespace DungeonBuilder.Core.Interfaces
{
    public interface IPoolable
    {
        void OnGetFromPool();

        void OnReturnToPool();
    }
}

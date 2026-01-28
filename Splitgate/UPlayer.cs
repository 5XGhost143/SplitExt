using System.Numerics;

namespace SplitExt
{
    public struct UPlayerInfo
    {
        public Vector3 Position;
        public bool IsEnemy;
        public byte TeamId;
        public float Distance;
        public float Health;

        public UPlayerInfo(Vector3 position, bool isEnemy, byte teamId, float distance, float health)
        {
            Position = position;
            IsEnemy = isEnemy;
            TeamId = teamId;
            Distance = distance;
            Health = health;
        }
    }
}
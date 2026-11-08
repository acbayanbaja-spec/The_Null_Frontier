using UnityEngine;

namespace NullFrontier.Core
{
    public struct DamageInfo
    {
        public float Amount;
        public bool IsCritical;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        public DamageType Type;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source,
            DamageType type = DamageType.Ballistic, bool isCritical = false)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Type = type;
            IsCritical = isCritical;
        }
    }

    public enum DamageType
    {
        Ballistic,
        Energy,
        Melee,
        Explosive,
        Environmental
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }
        void TakeDamage(DamageInfo damage);
        void Heal(float amount);
    }
}

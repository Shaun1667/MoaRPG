using System;

namespace Moa.Core.Ids
{
    /// <summary>엔티티 고유 ID (핵심 설계 규칙 5). 0 = 없음.</summary>
    [Serializable]
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly long Value;

        public EntityId(long value) { Value = value; }

        public static readonly EntityId None = default;

        public bool IsValid => Value != 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
        public override string ToString() => "#" + Value;
    }
}

using System;

namespace Moa.Core.Ids
{
    /// <summary>
    /// 고유 ID 발급기. 1부터 차례로 발급하고 다시 쓰지 않는다.
    /// 저장할 때는 <see cref="NextValue"/>만 남기고, 불러올 때 그 값으로 다시 만든다.
    /// </summary>
    public sealed class IdGenerator
    {
        public const long FirstValue = 1;

        long _next;

        public IdGenerator() : this(FirstValue) { }

        public IdGenerator(long nextValue)
        {
            if (nextValue < FirstValue)
                throw new ArgumentOutOfRangeException(nameof(nextValue), "nextValue must be >= FirstValue");
            _next = nextValue;
        }

        /// <summary>다음에 발급할 값 (저장용).</summary>
        public long NextValue => _next;

        /// <summary>지금까지 발급한 개수.</summary>
        public long IssuedCount => _next - FirstValue;

        public EntityId Next()
        {
            if (_next == long.MaxValue)
                throw new InvalidOperationException("IdGenerator exhausted");
            return new EntityId(_next++);
        }
    }
}

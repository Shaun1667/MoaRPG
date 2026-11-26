using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>등장 공격이 받아지지 않은 까닭 (끝에만 추가, 3-2).</summary>
    public enum EntryBlock
    {
        None,
        /// <summary>무기 표에 등장 공격이 없다.</summary>
        NoSlash,
        /// <summary>교체해 들어온 직후의 창이 아니다 (닫혔거나 열린 적 없음).</summary>
        NoWindow,
        Climbing,
        Gliding,
        Dodging,
        Airborne,
        /// <summary>이미 등장 공격 중.</summary>
        Busy,
    }

    /// <summary>
    /// 교체 등장 공격 순수 규칙 (3-2). 교체해 들어온 캐릭터가 창(Party가 연다, PartyRules.EntryWindow) 안에 공격을 누르면
    /// 일반 공격 1타 대신 강한 베기 한 번(무기 표의 entrySlash — 돌진 베기)이 나간다. 한 번 쓰면 창이 닫힌다.
    /// 베기 진행은 스킬 · 궁극기와 같은 SlashSequence(선딜 → 판정 → 후딜, 내딛기, 대상마다 한 번).
    /// 스킬 · 궁극기를 쓰면 창이 닫히고(PlayerAttack), 회피 · 점프 · 공중 · 교체로 베기가 끊긴다.
    /// 고정 스텝마다 <see cref="Advance"/> (창이 줄고 베기가 진행된다, 핵심 설계 규칙 2).
    /// </summary>
    public sealed class EntryAttack
    {
        const float Epsilon = 1e-5f;
        static readonly ComboStepRules[] Empty = new ComboStepRules[0];
        readonly ComboStepRules[] _slashes;
        readonly SlashSequence _seq = new SlashSequence();

        /// <summary>slashes: 등장 베기 표 (보통 한 줄, null · 빈 표 = 등장 공격 없음).</summary>
        public EntryAttack(ComboStepRules[] slashes)
        {
            _slashes = slashes != null ? (ComboStepRules[])slashes.Clone() : Empty;
        }

        public bool HasSlash => _slashes.Length > 0;
        public int SlashCount => _slashes.Length;
        public ComboStepRules Slash(int index) => _slashes[index];

        /// <summary>등장 공격을 쓸 수 있는 남은 시간 (초, 0 = 닫힘) · 연 창 전체.</summary>
        public float WindowLeft { get; private set; }
        public float WindowTotal { get; private set; }
        public bool WindowOpen => WindowLeft > 0f;
        /// <summary>베는 중.</summary>
        public bool Active => _seq.Active;
        public ComboPhase Phase => _seq.Phase;
        public int SlashIndex => _seq.SlashIndex;
        public float Elapsed => _seq.Elapsed;
        public ComboStepRules CurrentSlash => _seq.CurrentSlash;
        public int HitsThisSlash => _seq.HitsThisSlash;
        /// <summary>나간 수 · 창이 열린 수 · 쓰지 못하고 닫힌 수 (디버그 · 확인용).</summary>
        public int Count { get; private set; }
        public int OpenCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public EntryBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }

        /// <summary>창을 연다 (Party가 교체 직후에 부른다). seconds ≤ 0 이면 열지 않는다.</summary>
        public void Open(float seconds)
        {
            if (seconds <= 0f || !HasSlash) return;
            WindowLeft = WindowTotal = seconds;
            OpenCount++;
        }

        /// <summary>창을 닫는다 (스킬 · 궁극기를 씀 · 교체로 나감).</summary>
        public void Close() => WindowLeft = 0f;

        public EntryBlock Check(AttackContext ctx)
        {
            if (!HasSlash) return EntryBlock.NoSlash;
            if (!WindowOpen) return EntryBlock.NoWindow;
            if (ctx.Climbing) return EntryBlock.Climbing;
            if (ctx.Gliding) return EntryBlock.Gliding;
            if (ctx.Dodging) return EntryBlock.Dodging;
            if (!ctx.Grounded) return EntryBlock.Airborne;
            if (Active) return EntryBlock.Busy;
            return EntryBlock.None;
        }

        /// <summary>공격 누름: 창 안이고 땅 위면 등장 베기를 시작하고 창을 닫는다. 나갔으면 true (안 되면 까닭은 LastBlock — 창은 그대로).</summary>
        public bool TryStart(AttackContext ctx)
        {
            LastBlock = Check(ctx);
            if (LastBlock != EntryBlock.None) return false;
            WindowLeft = 0f;
            Count++;
            LastEnd = AttackEnd.None;
            _seq.Start(_slashes);
            return true;
        }

        /// <summary>한 고정 스텝: 창이 줄고(다 되면 닫힘) 베기가 진행된다.</summary>
        public ComboTick Advance(float dt)
        {
            if (WindowLeft > 0f)
            {
                WindowLeft -= dt;
                if (WindowLeft <= Epsilon) { WindowLeft = 0f; ExpiredCount++; }
            }
            bool was = _seq.Active;
            ComboTick t = _seq.Advance(dt);
            if (was && !_seq.Active) LastEnd = _seq.LastEnd;
            return t;
        }

        /// <summary>이번 베기에서 이 대상을 맞힐 수 있으면 기록하고 true.</summary>
        public bool TryHit(EntityId target) => _seq.TryHit(target);

        /// <summary>베기를 끊는다 (회피 · 점프 · 공중 · 교체). 창은 그대로.</summary>
        public void Interrupt(AttackEnd reason)
        {
            if (_seq.Interrupt(reason)) LastEnd = reason;
        }

        /// <summary>[상태 변경] 처음 상태로 — 베기를 끊고 창도 닫는다 (기록 수는 그대로).</summary>
        public void Cancel()
        {
            Interrupt(AttackEnd.Cancelled);
            WindowLeft = 0f;
        }
    }
}

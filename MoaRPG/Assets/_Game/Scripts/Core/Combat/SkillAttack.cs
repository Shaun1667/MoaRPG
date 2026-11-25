using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>스킬(E) 한 형태의 수치 (SkillTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 10번). 베기 수 = 표의 줄 수.</summary>
    public sealed class SkillFormRules
    {
        static readonly ComboStepRules[] Empty = new ComboStepRules[0];
        public static readonly SkillFormRules None = new SkillFormRules(null, 0f, 0f);

        readonly ComboStepRules[] _slashes;

        public SkillFormRules(ComboStepRules[] slashes, float cooldown, float energy)
        {
            _slashes = slashes != null ? (ComboStepRules[])slashes.Clone() : Empty;
            Cooldown = cooldown;
            Energy = energy;
        }

        public int SlashCount => _slashes.Length;
        public ComboStepRules Slash(int index) => _slashes[index];
        /// <summary>쓴 순간부터 다시 쓸 수 있기까지 (초).</summary>
        public float Cooldown { get; }
        /// <summary>이 형태가 처음 맞힌 순간 한 번 채우는 궁극기 에너지.</summary>
        public float Energy { get; }
        internal ComboStepRules[] Slashes => _slashes;

        public float Total
        {
            get { float t = 0f; foreach (var s in _slashes) t += s.Total; return t; }
        }
    }

    /// <summary>스킬(E) 수치: 짧게 누르기 · 길게 누르기 두 형태 + 길게 누르기 기준 시간.</summary>
    public sealed class SkillRules
    {
        public SkillRules(SkillFormRules tap, SkillFormRules hold, float holdTime)
        {
            Tap = tap ?? SkillFormRules.None;
            Hold = hold ?? SkillFormRules.None;
            HoldTime = holdTime;
        }

        public SkillFormRules Tap { get; }
        public SkillFormRules Hold { get; }
        /// <summary>누른 스텝 뒤로 이만큼 계속 누르고 있으면 길게 누르기 (초). 그 전에 떼면 짧게 누르기.</summary>
        public float HoldTime { get; }

        public SkillFormRules Form(SkillForm form) => form == SkillForm.Tap ? Tap : form == SkillForm.Hold ? Hold : SkillFormRules.None;
    }

    /// <summary>스킬 형태 (끝에만 추가).</summary>
    public enum SkillForm
    {
        None,
        Tap,
        Hold,
    }

    /// <summary>스킬 입력이 받아지지 않은 까닭 (끝에만 추가).</summary>
    public enum SkillBlock
    {
        None,
        NoForms,
        Climbing,
        Gliding,
        Dodging,
        Airborne,
        /// <summary>스킬 · 궁극기 · 낙하 공격 중.</summary>
        Busy,
        Cooldown,
    }

    /// <summary>
    /// 스킬(E) 순수 규칙 (2-4). 고정 스텝마다 <see cref="Advance"/> (핵심 설계 규칙 2).
    /// - 누르면(<see cref="Press"/>) 그 자리에 서서 길게 누르기를 센다(Pressing). 누른 스텝 뒤로 HoldTime 전에 떼면 짧게 누르기(Tap),
    ///   HoldTime까지 누르고 있으면 길게 누르기(Hold)가 그 스텝에 나간다(떼기를 기다리지 않는다). 누른 스텝에 이미 뗐으면 바로 Tap.
    /// - 쿨타임은 형태가 나가는 순간 그 형태의 Cooldown부터 (원신 방식). 쿨타임 중 · 땅 위가 아니면 받지 않는다.
    /// - 형태가 처음 맞힌 순간 한 번 에너지(<see cref="TakeEnergy"/>, 원소 입자 대신 바로).
    /// - 회피 · 점프 · 공중으로 끊긴다(<see cref="Interrupt"/>) — 누르는 중에 끊기면 쿨타임은 쓰지 않는다.
    /// 콤보 · 강공격을 끊는 것과 몸 방향 · 겹침 검사는 PlayerAttack · Unity 쪽이 한다.
    /// </summary>
    public sealed class SkillAttack
    {
        const float Epsilon = 1e-5f;
        readonly SkillRules _rules;
        readonly SlashSequence _seq = new SlashSequence();
        bool _energyTaken;

        public SkillAttack(SkillRules rules) { _rules = rules ?? new SkillRules(null, null, 0f); }

        public SkillRules Rules => _rules;
        /// <summary>누르고 있는 중 (형태가 정해지기 전).</summary>
        public bool Pressing { get; private set; }
        /// <summary>누른 스텝 뒤로 누르고 있던 시간 (초).</summary>
        public float PressElapsed { get; private set; }
        /// <summary>지금(또는 마지막) 형태.</summary>
        public SkillForm Form { get; private set; }
        public SkillFormRules CurrentForm => _rules.Form(Form);
        /// <summary>형태가 나가 베는 중.</summary>
        public bool Casting => _seq.Active;
        /// <summary>누르는 중이거나 베는 중 (이때 공격 · 스킬 누름은 Busy).</summary>
        public bool Active => Pressing || _seq.Active;
        public ComboPhase Phase => _seq.Phase;
        public int SlashIndex => _seq.SlashIndex;
        public float Elapsed => _seq.Elapsed;
        public ComboStepRules CurrentSlash => _seq.CurrentSlash;
        public int HitsThisSlash => _seq.HitsThisSlash;
        public float CooldownLeft { get; private set; }
        /// <summary>마지막으로 건 쿨타임 전체 (화면 비율용).</summary>
        public float CooldownTotal { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        /// <summary>나간 수 (전체 · 짧게 · 길게).</summary>
        public int Count { get; private set; }
        public int TapCount { get; private set; }
        public int HoldCount { get; private set; }
        public SkillBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }
        public bool HasForms => _rules.Tap.SlashCount > 0 || _rules.Hold.SlashCount > 0;

        public SkillBlock Check(AttackContext ctx)
        {
            if (!HasForms) return SkillBlock.NoForms;
            if (ctx.Climbing) return SkillBlock.Climbing;
            if (ctx.Gliding) return SkillBlock.Gliding;
            if (ctx.Dodging) return SkillBlock.Dodging;
            if (!ctx.Grounded) return SkillBlock.Airborne;
            if (Active) return SkillBlock.Busy;
            if (CooldownLeft > 0f) return SkillBlock.Cooldown;
            return SkillBlock.None;
        }

        /// <summary>스킬 누름. 받아지면 누르는 중이 되고 true (안 되면 까닭은 LastBlock).</summary>
        public bool Press(AttackContext ctx)
        {
            LastBlock = Check(ctx);
            if (LastBlock != SkillBlock.None) return false;
            Pressing = true;
            PressElapsed = 0f;
            return true;
        }

        /// <summary>누른 스텝에 이미 떼어 있었다 → 바로 짧게 누르기. 나갔으면 true.</summary>
        public bool Release() => Pressing && Cast(SkillForm.Tap);

        /// <summary>
        /// 누르는 중 한 스텝 (누른 다음 스텝부터): 떼면 Tap, HoldTime까지 누르고 있으면 Hold. 형태가 나갔으면 true.
        /// 땅 위가 아니게 되면(공중 · 회피 · 등반 · 활공) 누르기를 버린다(쿨타임 안 씀).
        /// </summary>
        public bool HoldStep(bool held, AttackContext ctx, float dt)
        {
            if (!Pressing) return false;
            if (ctx.Climbing || ctx.Gliding || ctx.Dodging || !ctx.Grounded)
            {
                Pressing = false;
                LastEnd = ctx.Dodging ? AttackEnd.Dodge : AttackEnd.Airborne;
                return false;
            }
            if (!held) return Cast(SkillForm.Tap);
            PressElapsed += dt;
            if (PressElapsed >= _rules.HoldTime - Epsilon) return Cast(SkillForm.Hold);
            return false;
        }

        bool Cast(SkillForm form)
        {
            Pressing = false;
            if (_rules.Form(form).SlashCount == 0) form = form == SkillForm.Tap ? SkillForm.Hold : SkillForm.Tap; // 그 형태가 없으면 다른 형태
            SkillFormRules f = _rules.Form(form);
            Form = form;
            Count++;
            if (form == SkillForm.Tap) TapCount++; else HoldCount++;
            CooldownLeft = CooldownTotal = f.Cooldown;
            _energyTaken = false;
            LastEnd = AttackEnd.None;
            _seq.Start(f.Slashes);
            return true;
        }

        /// <summary>한 고정 스텝 진행: 쿨타임 · 베기.</summary>
        public ComboTick Advance(float dt)
        {
            if (CooldownLeft > 0f)
            {
                CooldownLeft -= dt;
                if (CooldownLeft <= Epsilon) CooldownLeft = 0f;
            }
            bool was = _seq.Active;
            ComboTick t = _seq.Advance(dt);
            if (was && !_seq.Active) LastEnd = _seq.LastEnd;
            return t;
        }

        /// <summary>이번 베기에서 이 대상을 맞힐 수 있으면 기록하고 true.</summary>
        public bool TryHit(EntityId target) => _seq.TryHit(target);

        /// <summary>이번에 나간 형태의 에너지를 한 번만 꺼낸다 (처음 맞힌 순간). 이미 꺼냈으면 0.</summary>
        public float TakeEnergy()
        {
            if (_energyTaken || Count == 0) return 0f;
            _energyTaken = true;
            return CurrentForm.Energy;
        }

        /// <summary>누르기 · 베기를 끊는다 (회피 · 점프 · 공중 · 궁극기). 쿨타임은 그대로.</summary>
        public void Interrupt(AttackEnd reason)
        {
            if (Pressing) { Pressing = false; LastEnd = reason; }
            if (_seq.Interrupt(reason)) LastEnd = reason;
        }

        /// <summary>다른 까닭으로 받지 않았다 (PlayerAttack이 기록, 예: 궁극기 · 낙하 중 Busy).</summary>
        public void Refuse(SkillBlock reason) => LastBlock = reason;

        /// <summary>[상태 변경] 쿨타임을 없앤다.</summary>
        public void ResetCooldown() => CooldownLeft = 0f;
    }
}

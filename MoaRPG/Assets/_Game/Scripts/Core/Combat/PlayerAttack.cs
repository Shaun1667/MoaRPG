using Moa.Core.Ids;
using Moa.Core.Motion;

namespace Moa.Core.Combat
{
    /// <summary>지금 나가고 있는 공격 (끝에만 추가).</summary>
    public enum AttackMode
    {
        None,
        Combo,
        Charged,
        Plunge,
        /// <summary>스킬(E) — 누르는 중 포함 (2-4).</summary>
        Skill,
        /// <summary>궁극기(Q) (2-4).</summary>
        Burst,
    }

    /// <summary>한 고정 스텝의 공격 입력 (2-4: 스킬 · 궁극기까지).</summary>
    public readonly struct AttackInput
    {
        public readonly bool AttackPressed;
        public readonly bool AttackHeld;
        public readonly bool SkillPressed;
        public readonly bool SkillHeld;
        public readonly bool BurstPressed;

        public AttackInput(bool attackPressed, bool attackHeld, bool skillPressed, bool skillHeld, bool burstPressed)
        {
            AttackPressed = attackPressed;
            AttackHeld = attackHeld;
            SkillPressed = skillPressed;
            SkillHeld = skillHeld;
            BurstPressed = burstPressed;
        }
    }

    /// <summary>한 고정 스텝의 공격 결과 (PlayerAttack.Step).</summary>
    public readonly struct AttackTick
    {
        /// <summary>콤보 · 강공격 · 스킬 · 궁극기 휘두르기의 진행 (새 단계 · 내딛기 · 판정 겹침).</summary>
        public readonly ComboTick Swing;
        /// <summary>이번 스텝에 강공격이 시작됐는지.</summary>
        public readonly bool ChargedStarted;
        /// <summary>이번 스텝에 낙하 공격이 시작됐는지 (활공 중이면 부르는 쪽이 접는다).</summary>
        public readonly bool PlungeStarted;
        /// <summary>이번 스텝에 스킬 형태(짧게 · 길게)가 나갔는지 (2-4).</summary>
        public readonly bool SkillStarted;
        /// <summary>이번 스텝에 궁극기가 나갔는지 (2-4).</summary>
        public readonly bool BurstStarted;

        public AttackTick(ComboTick swing, bool chargedStarted, bool plungeStarted)
            : this(swing, chargedStarted, plungeStarted, false, false) { }

        public AttackTick(ComboTick swing, bool chargedStarted, bool plungeStarted, bool skillStarted, bool burstStarted)
        {
            Swing = swing;
            ChargedStarted = chargedStarted;
            PlungeStarted = plungeStarted;
            SkillStarted = skillStarted;
            BurstStarted = burstStarted;
        }
    }

    /// <summary>
    /// 플레이어 공격 조정 (2-2 · 2-4) — 일반 공격 콤보 · 강공격 · 낙하 공격 · 스킬(E) · 궁극기(Q)를 한 곳에서 고른다. 고정 스텝마다 <see cref="Step"/> (핵심 설계 규칙 2).
    /// - 누름: 강공격 · 낙하 · 스킬 · 궁극기 중이면 버린다(Busy). 공중이면 낙하 공격을 시도(높이 · 등반 · 회피 조건은 PlungeAttack).
    ///   땅 위면 콤보에 넣고(받아지지 않아도) 길게 누르기를 센다.
    /// - 길게 누르기: 누른 스텝 뒤로 HoldTime 동안 계속 누르고 있으면 강공격 — 지금 콤보 단계를 끊고(AttackEnd.Charged) 콤보는 1타로.
    ///   가만히 서서 길게 누르면 1타가 먼저 나가고 이어서 강공격(원신의 N1C). 스태미나가 모자라면 아무것도 하지 않는다(ChargeBlock.NoStamina, 콤보는 그대로).
    /// - 공중 · 회피 중이 되면 길게 누르기는 버린다. 회피 · 점프 · 순간이동은 <see cref="Interrupt"/> · <see cref="Cancel"/>.
    /// - 2-4 궁극기(Q): 먼저 본다. 에너지 가득 · 쿨 끝 · 땅 위면 콤보 · 강공격 · 스킬을 끊고 나간다(낙하 중에는 Busy). 회피 · 점프 <see cref="Interrupt"/>로 끊기지 않는다.
    /// - 2-4 스킬(E): 누르면 콤보 · 강공격을 끊고 누르는 중이 된다(궁극기 · 낙하 중이면 Busy) — 떼면 짧게, HoldTime이면 길게 (SkillAttack).
    /// - 2-4 에너지: <see cref="GainEnergy"/> · <see cref="TakeSkillEnergy"/> (궁극기 중에는 안 찬다).
    /// 몸 방향 · 실제 이동 · 낙하 속도 · 착지 검사 · 겹침 검사는 Unity 쪽(PlayerMotor · PlayerCombat)이 한다.
    /// </summary>
    public sealed class PlayerAttack
    {
        const float Epsilon = 1e-5f;

        public PlayerAttack(ComboRules combo, ChargedRules charged, PlungeRules plunge, float holdTime)
            : this(combo, charged, plunge, holdTime, null, null) { }

        /// <summary>2-4: 스킬 · 궁극기까지 (null이면 그 동작 없음 — 누르면 NoForms · NoSlashes).</summary>
        public PlayerAttack(ComboRules combo, ChargedRules charged, PlungeRules plunge, float holdTime, SkillRules skill, BurstRules burst)
        {
            Combo = new ComboAttack(combo);
            Charged = new ChargedAttack(charged);
            Plunge = new PlungeAttack(plunge);
            HoldTime = holdTime;
            Skill = new SkillAttack(skill);
            Burst = new BurstAttack(burst);
            Energy = new Energy(Burst.Rules.EnergyCost);
        }

        public ComboAttack Combo { get; }
        public ChargedAttack Charged { get; }
        public PlungeAttack Plunge { get; }
        /// <summary>스킬(E) · 궁극기(Q) · 에너지 (2-4).</summary>
        public SkillAttack Skill { get; }
        public BurstAttack Burst { get; }
        public Energy Energy { get; }
        /// <summary>누른 스텝 뒤로 이만큼 계속 누르고 있으면 강공격 (초).</summary>
        public float HoldTime { get; }

        /// <summary>길게 누르기를 세는 중인지.</summary>
        public bool Holding { get; private set; }
        /// <summary>누른 스텝 뒤로 계속 누르고 있던 시간 (초).</summary>
        public float HoldElapsed { get; private set; }
        /// <summary>마지막 누름이 받아지지 않은 까닭 (콤보 · 낙하의 까닭을 그대로, 강공격 · 낙하 · 스킬 · 궁극기 중이면 Busy).</summary>
        public AttackBlock LastBlock { get; private set; }
        /// <summary>마지막 길게 누르기가 강공격이 되지 못한 까닭 (None = 나감).</summary>
        public ChargeBlock LastChargeBlock { get; private set; }
        /// <summary>강공격 · 낙하 · 스킬 · 궁극기 중에 누른 수 (Busy, 디버그 · 확인용).</summary>
        public int BusyCount { get; private set; }

        public AttackMode Mode => Burst.Active ? AttackMode.Burst
            : Skill.Active ? AttackMode.Skill
            : Plunge.Active ? AttackMode.Plunge
            : Charged.Active ? AttackMode.Charged
            : Combo.Attacking ? AttackMode.Combo
            : AttackMode.None;

        /// <summary>땅 위에서 휘두르는 중 (콤보 · 강공격 · 스킬(누르는 중 포함) · 궁극기).</summary>
        public bool Swinging => Combo.Attacking || Charged.Active || Skill.Active || Burst.Active;
        /// <summary>강공격 · 낙하 · 스킬 · 궁극기 중 (이때 공격을 누르면 Busy).</summary>
        public bool Busy => Charged.Active || Plunge.Active || Skill.Active || Burst.Active;
        /// <summary>궁극기로 무적인지 (2-4).</summary>
        public bool Invulnerable => Burst.Invulnerable;

        /// <summary>
        /// 한 고정 스텝: 누름(pressed, 이번 스텝에 한 번) · 누르고 있음(held) → 공격 고르기 → 진행 (스킬 · 궁극기 입력 없음).
        /// stamina는 강공격 비용 (null이면 비용 없음).
        /// </summary>
        public AttackTick Step(bool pressed, bool held, AttackContext ctx, Stamina stamina, float dt) =>
            Step(new AttackInput(pressed, held, false, false, false), ctx, stamina, dt);

        /// <summary>한 고정 스텝 (2-4): 궁극기 → 스킬 → 일반 공격 순서로 입력을 보고 → 모두 진행.</summary>
        public AttackTick Step(AttackInput input, AttackContext ctx, Stamina stamina, float dt)
        {
            bool chargedStarted = false, plungeStarted = false, skillStarted = false, burstStarted = false;
            bool canHold = ctx.Grounded && !ctx.Dodging && !ctx.Climbing && !ctx.Gliding;

            // 궁극기 (2-4)
            if (input.BurstPressed)
            {
                if (Plunge.Active) Burst.Refuse(BurstBlock.Busy);
                else if (Burst.TryStart(ctx, Energy))
                {
                    burstStarted = true;
                    Holding = false;
                    Combo.Interrupt(AttackEnd.Burst);
                    Charged.Interrupt(AttackEnd.Burst);
                    Skill.Interrupt(AttackEnd.Burst);
                }
            }

            // 스킬 (2-4)
            if (input.SkillPressed)
            {
                if (Burst.Active || Plunge.Active) Skill.Refuse(SkillBlock.Busy);
                else if (Skill.Press(ctx))
                {
                    Holding = false;
                    Combo.Interrupt(AttackEnd.Skill);
                    Charged.Interrupt(AttackEnd.Skill);
                    if (!input.SkillHeld) skillStarted = Skill.Release();
                }
            }
            else if (Skill.Pressing)
            {
                skillStarted = Skill.HoldStep(input.SkillHeld, ctx, dt);
            }

            // 일반 공격 · 강공격 · 낙하 (2-1 · 2-2)
            if (input.AttackPressed)
            {
                if (Busy)
                {
                    LastBlock = AttackBlock.Busy;
                    BusyCount++;
                }
                else if (!ctx.Grounded)
                {
                    plungeStarted = Plunge.TryStart(ctx);
                    LastBlock = Plunge.LastBlock;
                    if (plungeStarted) { Combo.Interrupt(AttackEnd.Airborne); Holding = false; }
                }
                else
                {
                    Combo.Press(ctx);
                    LastBlock = Combo.LastBlock;
                    Holding = canHold;
                    HoldElapsed = 0f;
                }
            }
            else if (Holding)
            {
                if (!input.AttackHeld || !canHold || Busy)
                {
                    Holding = false;
                }
                else
                {
                    HoldElapsed += dt;
                    if (HoldElapsed >= HoldTime - Epsilon)
                    {
                        Holding = false;
                        if (Charged.TryStart(ctx, stamina))
                        {
                            Combo.Interrupt(AttackEnd.Charged);
                            chargedStarted = true;
                        }
                        LastChargeBlock = Charged.LastBlock;
                    }
                }
            }

            ComboTick comboTick = Combo.Advance(dt);
            ComboTick chargedTick = Charged.Advance(dt);
            Plunge.Advance(dt);
            ComboTick skillTick = Skill.Advance(dt);
            ComboTick burstTick = Burst.Advance(dt);
            ComboTick swing;
            if (burstStarted || Burst.Active || burstTick.HitOpen || burstTick.Lunge > 0f) swing = burstTick;
            else if (skillStarted || Skill.Active || skillTick.HitOpen || skillTick.Lunge > 0f) swing = skillTick;
            else swing = chargedStarted || Charged.Active || chargedTick.HitOpen || chargedTick.Lunge > 0f ? chargedTick : comboTick;
            return new AttackTick(swing, chargedStarted, plungeStarted, skillStarted, burstStarted);
        }

        /// <summary>떨어지던 낙하 공격이 땅에 닿았다 (PlayerMotor가 이동 뒤에 부른다). 착지했으면 true — 그 자리에서 범위 판정을 한 번.</summary>
        public bool Land(float feetY) => Plunge.Land(feetY);

        /// <summary>지금 휘두르기(궁극기 · 스킬 · 강공격이면 그 베기, 아니면 콤보 단계)에서 이 대상을 맞힐 수 있으면 기록하고 true.</summary>
        public bool TryHitSwing(EntityId target) =>
            Burst.Active ? Burst.TryHit(target)
            : Skill.Casting ? Skill.TryHit(target)
            : Charged.Active ? Charged.TryHit(target)
            : Combo.TryHit(target);

        /// <summary>지금 휘두르기의 수치 (궁극기 · 스킬 · 강공격이면 그 베기).</summary>
        public ComboStepRules CurrentSwing =>
            Burst.Active ? Burst.CurrentSlash
            : Skill.Casting ? Skill.CurrentSlash
            : Charged.Active ? Charged.CurrentSlash
            : Combo.CurrentStep;

        /// <summary>에너지를 채운다 (적 처치 등, 2-4). 궁극기 중에는 차지 않는다(원신). 실제로 찬 양.</summary>
        public float GainEnergy(float amount) => Burst.Active ? 0f : Energy.Add(amount);

        /// <summary>스킬이 맞혔다 — 이번에 나간 형태의 에너지를 한 번만 채운다 (2-4). 실제로 찬 양.</summary>
        public float TakeSkillEnergy() => GainEnergy(Skill.TakeEnergy());

        /// <summary>공격을 모두 끊는다 (회피 · 점프 · 공중). 콤보도 1타로, 길게 누르기도 버린다. 궁극기는 끊기지 않는다 (2-4).</summary>
        public void Interrupt(AttackEnd reason)
        {
            Holding = false;
            Combo.Interrupt(reason);
            Charged.Interrupt(reason);
            Plunge.Interrupt(reason);
            Skill.Interrupt(reason);
        }

        /// <summary>땅 위 휘두르기(콤보 · 강공격 · 스킬)만 끊는다 — 낙하 · 궁극기는 그대로 (발밑이 사라짐).</summary>
        public void InterruptSwing(AttackEnd reason)
        {
            Holding = false;
            Combo.Interrupt(reason);
            Charged.Interrupt(reason);
            Skill.Interrupt(reason);
        }

        /// <summary>[상태 변경] 처음 상태로 (순간이동, 기록 수 · 쿨타임 · 에너지는 그대로).</summary>
        public void Cancel()
        {
            Holding = false;
            HoldElapsed = 0f;
            Combo.Cancel();
            Charged.Interrupt(AttackEnd.Cancelled);
            Plunge.Interrupt(AttackEnd.Cancelled);
            Skill.Interrupt(AttackEnd.Cancelled);
            Burst.Cancel();
        }

        /// <summary>대기 캐릭터의 한 고정 스텝 (2-6, Party가 부른다): 스킬 · 궁극기 쿨만 돈다 (교체로 끊겨 베기는 없다).</summary>
        public void AdvanceBench(float dt)
        {
            Skill.Advance(dt);
            Burst.Advance(dt);
        }

    }
}

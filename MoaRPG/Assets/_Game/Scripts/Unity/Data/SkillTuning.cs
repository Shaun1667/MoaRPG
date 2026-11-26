using System;
using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 캐릭터 한 명의 스킬(E) · 궁극기(Q) 수치 (2-4). 에셋 Assets/_Game/Data/Skills/&lt;캐릭터&gt;.asset, 값은 BALANCE.md 10번.
    /// 새 캐릭터의 스킬 = 이 에셋 하나 (형태 · 베기 표 · 쿨타임 · 에너지 · 범위가 모두 여기) — 캐릭터별 분기 코드 없음.
    /// 베기 한 줄은 무기 표와 같은 WeaponTuning.Step(배율 · 선딜 · 판정 · 후딜 · 내딛기 · 경직, inputOpen은 쓰지 않음).
    /// 처치 에너지는 적 표(DummyTuning energyOnDefeat). 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Skill Tuning", fileName = "SkillTuning")]
    public sealed class SkillTuning : ScriptableObject
    {
        [Serializable]
        public struct Form
        {
            [Tooltip("Slashes, one row per slash (inputOpen is not used)")]
            public WeaponTuning.Step[] slashes;
            [Tooltip("s from the cast until the skill can be used again")]
            public float cooldown;
            [Tooltip("Burst energy gained once, when this cast first hits")]
            public float energy;
            [Tooltip("m from the body center to the target surface")]
            public float hitRange;
            [Tooltip("Full fan angle, degrees (360 = all around)")]
            public float hitAngle;
        }

        [Header("Elemental skill (E)")]
        [Tooltip("s the Skill button must stay held after the press for the hold form (release earlier = tap form)")]
        [SerializeField] float skillHoldTime;
        [Tooltip("Tap form (press and release)")]
        [SerializeField] Form tap;
        [Tooltip("Hold form (held for skillHoldTime)")]
        [SerializeField] Form hold;

        [Header("Burst (Q)")]
        [Tooltip("Energy spent = energy gauge size (the burst needs a full gauge)")]
        [SerializeField] float burstEnergyCost;
        [Tooltip("s from the cast until the burst can be used again")]
        [SerializeField] float burstCooldown;
        [Tooltip("Invulnerable for the whole burst")]
        [SerializeField] bool burstInvulnerable;
        [Tooltip("Burst slashes, one row per slash (inputOpen is not used)")]
        [SerializeField] WeaponTuning.Step[] burstSlashes;
        [Tooltip("m from the body center to the target surface")]
        [SerializeField] float burstHitRange;
        [Tooltip("Full fan angle, degrees (360 = all around)")]
        [SerializeField] float burstHitAngle;

        [Header("Switch handoff (3-2): switch out within PartyTuning handoffWindow after E")]
        [Tooltip("Incoming character deals damage x (1 + this) for handoffDuration (0.25 = +25%)")]
        [SerializeField] float handoffDamageBonus;
        [Tooltip("Incoming character deals poise x (1 + this) for handoffDuration")]
        [SerializeField] float handoffPoiseBonus;
        [Tooltip("Shield on the incoming character: blocks this much damage first, gone when the buff ends")]
        [SerializeField] int handoffShield;
        [Tooltip("Burst energy the incoming character gets at once on the switch")]
        [SerializeField] float handoffEnergy;
        [Tooltip("s the buff lasts (it also ends when the incoming character leaves the field)")]
        [SerializeField] float handoffDuration;

        public float SkillHoldTime => skillHoldTime;
        public Form Tap => tap;
        public Form Hold => hold;
        public Form GetForm(SkillForm form) => form == SkillForm.Hold ? hold : tap;
        public float BurstEnergyCost => burstEnergyCost;
        public float BurstCooldown => burstCooldown;
        public bool BurstInvulnerable => burstInvulnerable;
        public int BurstSlashCount => burstSlashes != null ? burstSlashes.Length : 0;
        public WeaponTuning.Step GetBurstSlash(int i) => burstSlashes[i];
        public float BurstHitRange => burstHitRange;
        public float BurstHitAngle => burstHitAngle;
        public float HandoffDamageBonus => handoffDamageBonus;
        public float HandoffPoiseBonus => handoffPoiseBonus;
        public int HandoffShield => handoffShield;
        public float HandoffEnergy => handoffEnergy;
        public float HandoffDuration => handoffDuration;

        /// <summary>스킬 뒤 교체 강화 (3-2).</summary>
        public HandoffRules ToHandoffRules() => new HandoffRules(handoffDamageBonus, handoffPoiseBonus, handoffShield, handoffEnergy, handoffDuration);

        /// <summary>베기 경직 (index는 0부터, 없으면 0).</summary>
        public float SkillPoise(SkillForm form, int index)
        {
            var s = GetForm(form).slashes;
            return s != null && index >= 0 && index < s.Length ? s[index].poise : 0f;
        }

        public float BurstPoise(int index) => burstSlashes != null && index >= 0 && index < burstSlashes.Length ? burstSlashes[index].poise : 0f;

        public SkillRules ToSkillRules() => new SkillRules(ToFormRules(tap), ToFormRules(hold), skillHoldTime);

        public BurstRules ToBurstRules() => new BurstRules(ToSteps(burstSlashes), burstEnergyCost, burstCooldown, burstInvulnerable);

        static SkillFormRules ToFormRules(Form f) => new SkillFormRules(ToSteps(f.slashes), f.cooldown, f.energy);

        static ComboStepRules[] ToSteps(WeaponTuning.Step[] steps)
        {
            int n = steps != null ? steps.Length : 0;
            var list = new ComboStepRules[n];
            for (int i = 0; i < n; i++)
            {
                WeaponTuning.Step s = steps[i];
                list[i] = new ComboStepRules(s.multiplier, s.windup, s.active, s.recovery, s.inputOpen, s.lunge);
            }
            return list;
        }
    }
}

using Moa.Core.Combat;
using Moa.Unity.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 3-1 수치 에셋 검산: 캐릭터 체력 · 방어(Data/Characters) · Grunt 공격력(Data/Enemies/Grunt.asset) · PlayerHealthTuning(Data/Player)이 BALANCE.md 13번과 같은지,
    /// Grunt 한 대가 캐릭터마다 몇 % 깎는지, 플레이어에 붙인 PlayerVitals가 파티 4명 체력으로 묶이고 쓰러지면 PlayerCombat의 현재 캐릭터가 바뀌는지.
    /// 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class PlayerHealthAssetTests
    {
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string GruntPath = "Assets/_Game/Data/Enemies/Grunt.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string HealthPath = "Assets/_Game/Data/Player/PlayerHealthTuning.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static bool WholeSteps(float seconds)
        {
            float steps = seconds / FixedStep;
            return System.Math.Abs(steps - System.Math.Round(steps)) < 1e-3;
        }

        [Test]
        public void Characters_HealthAndDefense_MatchBalance13_GuardianHighest()
        {
            var p = Load<PartyTuning>(PartyPath);
            int[] hp = { 1000, 1300, 850, 900 };
            float[] def = { 100f, 200f, 80f, 100f };
            for (int i = 0; i < p.MemberCount; i++)
            {
                var c = p.GetMember(i);
                Assert.That(c.MaxHealth, Is.EqualTo(hp[i]), c.name + " max health");
                Assert.That(c.Defense, Is.EqualTo(def[i]).Within(Eps), c.name + " defense");
            }
            var guardian = p.GetMember(1);
            Assert.That(guardian.name, Is.EqualTo("SwordGuardian"));
            for (int i = 0; i < p.MemberCount; i++)
            {
                if (i == 1) continue;
                Assert.That(guardian.MaxHealth, Is.GreaterThan(p.GetMember(i).MaxHealth), "guardian has the most health");
                Assert.That(guardian.Defense, Is.GreaterThan(p.GetMember(i).Defense), "guardian has the most defense");
            }
        }

        [Test]
        public void GruntHit_PerCharacter_DamageShareAndHitsToDown()
        {
            var p = Load<PartyTuning>(PartyPath);
            var g = Load<EnemyTuning>(GruntPath);
            var c = Load<CombatTuning>(CombatPath);
            Assert.That(g.Attack, Is.EqualTo(100f).Within(Eps), "Grunt attack");
            Assert.That(g.AttackMultiplier, Is.EqualTo(1.25f).Within(Eps), "Grunt hit multiplier");
            // 칸: 한 대 피해 · 최대 체력 대비 % · 쓰러지기까지 맞는 수
            int[] damage = { 100, 83, 104, 100 };
            float[] share = { 10.0f, 6.4f, 12.2f, 11.1f };
            int[] hits = { 10, 16, 9, 9 };
            for (int i = 0; i < p.MemberCount; i++)
            {
                var m = p.GetMember(i);
                int d = DamageMath.Compute(g.Attack, g.AttackMultiplier, m.Defense, c.DefenseConstant);
                Assert.That(d, Is.EqualTo(damage[i]), m.name + " damage");
                Assert.That(100f * d / m.MaxHealth, Is.EqualTo(share[i]).Within(0.05f), m.name + " % per hit");
                Assert.That((m.MaxHealth + d - 1) / d, Is.EqualTo(hits[i]), m.name + " hits to down");
            }
        }

        [Test]
        public void PlayerHealthTuning_MatchesBalance13()
        {
            var t = Load<PlayerHealthTuning>(HealthPath);
            Assert.That(t.HitStunTime, Is.EqualTo(0.30f).Within(Eps));
            Assert.That(t.KnockbackDistance, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(t.WipeFadeOutTime, Is.EqualTo(1.0f).Within(Eps));
            Assert.That(t.WipeDarkTime, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(t.WipeFadeInTime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(t.ReviveHealthRatio, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(t.HealPointRadius, Is.EqualTo(2.5f).Within(Eps));
            Assert.That(t.HealPerSecond, Is.EqualTo(0.5f).Within(Eps), "empty to full in 2 s");
            foreach (float s in new[] { t.HitStunTime, t.WipeFadeOutTime, t.WipeDarkTime, t.WipeFadeInTime })
                Assert.That(WholeSteps(s), Is.True, s + " s = whole fixed steps");
            var g = Load<EnemyTuning>(GruntPath);
            Assert.That(t.HitStunTime, Is.LessThan(g.AttackInterval + g.TelegraphTime), "the stun ends long before the next Grunt hit");
        }

        [Test]
        public void PlayerVitals_OnAPartyPlayer_HoldsFourHealths_AndADownSwitchesTheFieldCharacter()
        {
            var go = new UnityEngine.GameObject("PlayerHealthAssetTests_Player");
            try
            {
                var pc = go.AddComponent<PlayerCombat>();
                var so = new SerializedObject(pc);
                so.FindProperty("weapon").objectReferenceValue = Load<WeaponTuning>(SwordPath);
                so.FindProperty("tuning").objectReferenceValue = Load<CombatTuning>(CombatPath);
                so.FindProperty("party").objectReferenceValue = Load<PartyTuning>(PartyPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                var vitals = go.AddComponent<PlayerVitals>();
                var vso = new SerializedObject(vitals);
                vso.FindProperty("tuning").objectReferenceValue = Load<PlayerHealthTuning>(HealthPath);
                vso.ApplyModifiedPropertiesWithoutUndo();

                pc.Bind(null);
                vitals.Bind(go.GetComponent<Moa.Unity.Player.PlayerMotor>());
                Assert.That(vitals.Bound, Is.True);
                Assert.That(vitals.Count, Is.EqualTo(4));
                Assert.That(vitals.Vitals.Member(1).Max, Is.EqualTo(1300));

                int switched = -1;
                pc.Switched += (from, to) => switched = to;
                PlayerHit h = vitals.DebugDownActive();
                Assert.That(h.SwitchedTo, Is.EqualTo(1));
                Assert.That(pc.ActiveIndex, Is.EqualTo(1));
                Assert.That(switched, Is.EqualTo(1), "PlayerCombat follows the forced switch");
                Assert.That(pc.ActiveCharacter.name, Is.EqualTo("SwordGuardian"));
                Assert.That(pc.TrySwitch(0, false), Is.False, "downed slot 1");
                Assert.That(pc.Party.LastBlock, Is.EqualTo(SwitchBlock.Downed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}

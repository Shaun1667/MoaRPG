using System;
using Moa.Core.Combat;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 플레이어 체력 (3-1) — 규칙은 Core PartyVitals(캐릭터마다 체력 · 방어, 필드 캐릭터만 맞음, 쓰러지면 다음 캐릭터로 자동 교체, 전원 쓰러짐 → 어두워졌다 부활),
    /// 수치는 PlayerHealthTuning(공통) · CharacterData(최대 체력 · 방어) · EnemyTuning(적 공격력).
    /// 적 공격은 PlayerDefense.ReceiveEnemyAttack이 판정해 Hit일 때만 <see cref="TakeEnemyHit"/>로 온다(저스트 회피 · 무적이면 오지 않음).
    /// PlayerMotor가 고정 스텝 처음에 <see cref="Step"/>을 부르고, 경직 중에는 입력 대신 <see cref="KnockbackVelocity"/>로 밀려난다(회피로 끊김).
    /// 쓰러져 교체되면 PlayerCombat.SyncActive로 무기 · 스킬 · 몸 색을 맞춘다. 부활 지점 이동 · 적 되돌리기는 GameRoot가 <see cref="Respawned"/>를 받아 한다.
    /// 파티가 없으면 PlayerCombat의 character 칸 한 명. GameRoot가 Bind로 연결한다 (PlayerCombat.Bind 뒤).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerVitals : MonoBehaviour
    {
        [SerializeField] PlayerHealthTuning tuning;

        PlayerMotor _motor;
        PlayerCombat _combat;
        PartyVitals _vitals;
        CharacterData[] _characters = new CharacterData[0];
        Vector3 _knockDir;

        public PlayerHealthTuning Tuning => tuning;
        /// <summary>체력 규칙 (Core, Bind 전 · 캐릭터가 없으면 null).</summary>
        public PartyVitals Vitals => _vitals;
        public bool Bound => _vitals != null;
        public int Count => _vitals != null ? _vitals.Count : 0;
        public CharacterData GetCharacter(int index) => index >= 0 && index < _characters.Length ? _characters[index] : null;
        public bool Stunned => _vitals != null && _vitals.Stunned;
        public bool Wiping => _vitals != null && _vitals.Wiping;
        public bool InputBlocked => _vitals != null && _vitals.InputBlocked;
        public float Darkness => _vitals != null ? _vitals.Darkness : 0f;
        /// <summary>경직 동안 밀려나는 속도 (m/s, 수평).</summary>
        public Vector3 KnockbackVelocity => Stunned ? _knockDir * _vitals.KnockbackSpeed : Vector3.zero;
        /// <summary>피해 숫자를 띄울 자리 (머리 위).</summary>
        public Vector3 NumberPosition => transform.position + Vector3.up * (_motor != null && _motor.Tuning != null ? _motor.Tuning.CapsuleHeight : 0f);
        /// <summary>회복 지점에서 지난 스텝에 찬 양 (디버그 · 확인용).</summary>
        public int LastHealed { get; private set; }

        /// <summary>맞을 때마다 (결과, 숫자 자리) — 피해 숫자용.</summary>
        public event Action<PlayerHit, Vector3> Hurt;
        /// <summary>전원이 쓰러졌을 때 (어두워지기 시작).</summary>
        public event Action WipeStarted;
        /// <summary>어두워진 뒤 전원 되살아났을 때 — 부활 지점으로 옮기고 적을 제자리로.</summary>
        public event Action Respawned;

        public void Bind(PlayerMotor motor)
        {
            _motor = motor;
            _combat = GetComponent<PlayerCombat>();
            _vitals = null;
            if (tuning == null) { Debug.LogError("PlayerVitals: PlayerHealthTuning is not assigned"); enabled = false; return; }
            Party party = _combat != null ? _combat.Party : null;
            CharacterData[] cs;
            if (party != null)
            {
                cs = new CharacterData[party.Count];
                for (int i = 0; i < cs.Length; i++) cs[i] = _combat.GetCharacter(i);
            }
            else
            {
                CharacterData solo = _combat != null ? _combat.SoloCharacter : null;
                if (solo == null) { Debug.LogWarning("PlayerVitals: no character - the player has no health"); return; }
                cs = new[] { solo };
            }
            var max = new int[cs.Length];
            var defense = new float[cs.Length];
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] == null || cs[i].MaxHealth < 1)
                {
                    Debug.LogError("PlayerVitals: character " + (cs[i] != null ? cs[i].name : "slot " + (i + 1)) + " has no max health");
                    enabled = false;
                    return;
                }
                max[i] = cs[i].MaxHealth;
                defense[i] = cs[i].Defense;
            }
            _characters = cs;
            _vitals = new PartyVitals(tuning.ToRules(), max, defense, party);
        }

        float DefenseConstant => _combat != null && _combat.Tuning != null ? _combat.Tuning.DefenseConstant : 0f;

        /// <summary>땅 위에서만 경직 · 밀려남 (등반 · 활공 · 낙하 중 · 공중은 피해만).</summary>
        bool CanStagger => _motor != null && _motor.Grounded && !_motor.Climbing && !_motor.Gliding && !_motor.PlungeFalling;

        /// <summary>적 공격이 맞았다 (PlayerDefense가 Hit으로 판정한 뒤). from = 공격한 쪽 발.</summary>
        public PlayerHit TakeEnemyHit(Vector3 from, float attack, float multiplier)
        {
            if (_vitals == null) return PlayerHit.None(0);
            PlayerHit hit = _vitals.TakeHit(attack, multiplier, DefenseConstant, CanStagger);
            AfterHit(hit, from);
            return hit;
        }

        void AfterHit(PlayerHit hit, Vector3 from)
        {
            if (hit.Ignored) return;
            if (hit.Stunned)
            {
                Vector3 d = transform.position - from;
                d.y = 0f;
                _knockDir = d.sqrMagnitude > 1e-6f ? d.normalized : -transform.forward;
                _combat?.Interrupt(AttackEnd.Hit);
                _motor?.Dodge?.Cancel();
            }
            if (hit.AllDown) _combat?.Interrupt(AttackEnd.Hit);
            _combat?.SyncActive(); // 쓰러져 교체됐으면 무기 · 스킬 · 몸 색
            Hurt?.Invoke(hit, NumberPosition);
            if (hit.AllDown) WipeStarted?.Invoke();
        }

        /// <summary>한 고정 스텝 (PlayerMotor가 처음에 부른다): 경직 · 전멸 연출.</summary>
        public void Step(float dt)
        {
            if (_vitals == null) return;
            VitalsTick t = _vitals.Advance(dt);
            if (!t.Respawned) return;
            _combat?.SyncActive();
            Respawned?.Invoke();
        }

        /// <summary>[상태 변경] 경직을 끝낸다 (회피로 빠져나감 — PlayerMotor가 부른다).</summary>
        public void EndStun() => _vitals?.EndStun();

        /// <summary>회복 지점 안에서 한 스텝 (HealingPoint가 부른다). 찬 양.</summary>
        public int HealTick(float dt)
        {
            if (_vitals == null) return 0;
            int healed = _vitals.Heal(dt);
            LastHealed = healed;
            _combat?.SyncActive();
            return healed;
        }

        // ---- 디버그 진입점 (GameRoot.DebugPlayerHealth*가 부른다) ----

        /// <summary>[상태 변경] 필드 캐릭터에게 amount 피해 (방어 없이, 땅 위면 경직 — 뒤로 밀림).</summary>
        public PlayerHit DebugDamage(int amount)
        {
            if (_vitals == null) return PlayerHit.None(0);
            PlayerHit hit = _vitals.TakeDamage(amount, CanStagger);
            AfterHit(hit, transform.position + transform.forward);
            return hit;
        }

        /// <summary>[상태 변경] 필드 캐릭터를 쓰러뜨린다.</summary>
        public PlayerHit DebugDownActive()
        {
            if (_vitals == null) return PlayerHit.None(0);
            PlayerHit hit = _vitals.DownActive();
            AfterHit(hit, transform.position + transform.forward);
            return hit;
        }

        /// <summary>[상태 변경] 전원 쓰러뜨린다 → 어두워졌다 부활.</summary>
        public PlayerHit DebugDownAll()
        {
            if (_vitals == null) return PlayerHit.None(0);
            PlayerHit hit = _vitals.DownAll();
            AfterHit(hit, transform.position + transform.forward);
            return hit;
        }

        /// <summary>[상태 변경] 전원 가득 · 되살리기.</summary>
        public void DebugRestoreAll()
        {
            if (_vitals == null) return;
            _vitals.RestoreAll();
            _combat?.SyncActive();
        }

        /// <summary>[상태 변경] 플레이어 무적 (맞아도 체력 · 경직 없음, 숫자는 회색).</summary>
        public bool Invulnerable
        {
            get => _vitals != null && _vitals.Invulnerable;
            set { if (_vitals != null) _vitals.Invulnerable = value; }
        }
    }
}

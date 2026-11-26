using System;

namespace Moa.Core.Combat
{
    /// <summary>플레이어 체력 공통 수치 (PlayerHealthTuning 에셋 → PlayerVitals가 넘긴다, BALANCE 13번). 캐릭터마다 최대 체력 · 방어는 CharacterData.</summary>
    public sealed class VitalsRules
    {
        public VitalsRules(float hitStunTime, float knockbackDistance, float wipeFadeOut, float wipeDark, float wipeFadeIn, float reviveRatio, float healRatePerSecond)
        {
            HitStunTime = Pos(hitStunTime);
            KnockbackDistance = Pos(knockbackDistance);
            WipeFadeOut = Pos(wipeFadeOut);
            WipeDark = Pos(wipeDark);
            WipeFadeIn = Pos(wipeFadeIn);
            ReviveRatio = reviveRatio < 0f ? 0f : reviveRatio > 1f ? 1f : reviveRatio;
            HealRatePerSecond = Pos(healRatePerSecond);
        }

        static float Pos(float v) => v > 0f ? v : 0f;

        /// <summary>맞으면 짧게 멈추는 시간 (초, 땅 위에서만).</summary>
        public float HitStunTime { get; }
        /// <summary>그동안 공격한 쪽 반대로 밀려나는 거리 (m).</summary>
        public float KnockbackDistance { get; }
        /// <summary>전원 쓰러짐: 어두워지는 시간 → 어두운 채(이 처음에 부활 지점으로) → 밝아지는 시간 (초).</summary>
        public float WipeFadeOut { get; }
        public float WipeDark { get; }
        public float WipeFadeIn { get; }
        public float WipeTotal => WipeFadeOut + WipeDark + WipeFadeIn;
        /// <summary>전원 쓰러진 뒤 되살아나는 체력 (최대 체력 × 이 비율, 0 ~ 1).</summary>
        public float ReviveRatio { get; }
        /// <summary>회복 지점에서 1초에 차는 체력 (최대 체력 × 이 비율).</summary>
        public float HealRatePerSecond { get; }
    }

    public enum WipePhase
    {
        None,
        FadeOut,
        Dark,
        FadeIn,
    }

    /// <summary>플레이어가 받은 한 방의 결과 (3-1).</summary>
    public readonly struct PlayerHit
    {
        /// <summary>맞은 칸 (0부터).</summary>
        public readonly int Member;
        /// <summary>계산된 피해 (방어 반영 뒤) · 실제로 줄어든 체력 (무적이면 0).</summary>
        public readonly int Requested;
        public readonly int Applied;
        /// <summary>받지 않았다 (전멸 연출 중 · 이미 쓰러짐) — 체력 · 숫자 · 경직 없음.</summary>
        public readonly bool Ignored;
        public readonly bool Stunned;
        /// <summary>이 한 방으로 쓰러졌다.</summary>
        public readonly bool Downed;
        /// <summary>쓰러져 자동 교체된 칸 (-1 = 없음).</summary>
        public readonly int SwitchedTo;
        /// <summary>이 한 방으로 전원이 쓰러졌다 (전멸 연출 시작).</summary>
        public readonly bool AllDown;
        /// <summary>교체 강화 보호막이 막은 양 (3-2, Requested 중 체력에 가지 않은 몸).</summary>
        public readonly int Absorbed;
        /// <summary>3-4: 이 한 방으로 넘어졌다 (회피로 못 끊는 긴 경직).</summary>
        public readonly bool KnockedDown;

        public PlayerHit(int member, int requested, int applied, bool ignored, bool stunned, bool downed, int switchedTo, bool allDown)
            : this(member, requested, applied, ignored, stunned, downed, switchedTo, allDown, 0) { }

        public PlayerHit(int member, int requested, int applied, bool ignored, bool stunned, bool downed, int switchedTo, bool allDown, int absorbed)
            : this(member, requested, applied, ignored, stunned, downed, switchedTo, allDown, absorbed, false) { }

        public PlayerHit(int member, int requested, int applied, bool ignored, bool stunned, bool downed, int switchedTo, bool allDown, int absorbed, bool knockedDown)
        {
            Absorbed = absorbed;
            KnockedDown = knockedDown;
            Member = member;
            Requested = requested;
            Applied = applied;
            Ignored = ignored;
            Stunned = stunned;
            Downed = downed;
            SwitchedTo = switchedTo;
            AllDown = allDown;
        }

        public static PlayerHit None(int member) => new PlayerHit(member, 0, 0, true, false, false, -1, false);
    }

    /// <summary>한 고정 스텝의 결과 (3-1).</summary>
    public readonly struct VitalsTick
    {
        /// <summary>이 스텝에 전원이 되살아났다 (어두워진 뒤 — 부활 지점으로 옮기고 적을 제자리로).</summary>
        public readonly bool Respawned;
        /// <summary>이 스텝에 전멸 연출이 끝났다 (다시 움직일 수 있다).</summary>
        public readonly bool WipeEnded;

        public VitalsTick(bool respawned, bool wipeEnded)
        {
            Respawned = respawned;
            WipeEnded = wipeEnded;
        }
    }

    /// <summary>
    /// 파티 체력 순수 규칙 (3-1, 원신 방식). 캐릭터마다 Health · 방어를 따로 들고, 맞는 것은 필드 캐릭터뿐(대기 캐릭터 체력은 그대로).
    /// - 피해(<see cref="TakeHit"/>): DamageMath(공격력 × 배율, 그 캐릭터의 방어). 무적(디버그)이면 0. 땅 위면 짧은 경직(HitStunTime) + 밀려남.
    ///   저스트 회피 · 회피 무적 · 궁극기 무적은 PlayerDefense가 판정해 여기까지 오지 않는다.
    /// - 쓰러짐: 필드 캐릭터가 0이 되면 Party에 쓰러짐을 표시하고(쓰러진 칸으로는 교체 불가) 다음 칸부터 돌아 살아 있는 캐릭터로 강제 교체(쿨 무시).
    ///   살아 있는 캐릭터가 없으면 전멸 연출(<see cref="Wipe"/>: 어두워짐 → 어두운 채 → 밝아짐). 어두워짐이 끝나는 스텝에 전원 ReviveRatio로 되살아난다(VitalsTick.Respawned).
    ///   연출 중에는 맞지 않고 입력도 막힌다(<see cref="InputBlocked"/>).
    /// - 회복 지점(<see cref="Heal"/>): 스텝마다 전원 최대 × HealRatePerSecond × dt(최소 1), 쓰러진 캐릭터도 되살린다.
    /// party가 null이면 한 명(파티 없는 플레이어).
    /// </summary>
    public sealed class PartyVitals
    {
        const float Epsilon = 1e-5f;
        readonly Health[] _health;
        readonly float[] _defense;
        readonly Party _party;

        public PartyVitals(VitalsRules rules, int[] maxHealth, float[] defense, Party party)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (maxHealth == null || maxHealth.Length == 0) throw new ArgumentException("needs at least one member", nameof(maxHealth));
            if (defense == null || defense.Length != maxHealth.Length) throw new ArgumentException("one defense per member", nameof(defense));
            if (party != null && party.Count != maxHealth.Length) throw new ArgumentException("one health per party member", nameof(party));
            if (party == null && maxHealth.Length != 1) throw new ArgumentException("without a party there is one member", nameof(maxHealth));
            _health = new Health[maxHealth.Length];
            _defense = new float[maxHealth.Length];
            for (int i = 0; i < maxHealth.Length; i++)
            {
                _health[i] = new Health(maxHealth[i]);
                _defense[i] = defense[i] > 0f ? defense[i] : 0f;
            }
            _party = party;
        }

        public VitalsRules Rules { get; }
        public Party Party => _party;
        public int Count => _health.Length;
        /// <summary>필드 캐릭터 칸 (파티가 없으면 0).</summary>
        public int ActiveIndex => _party != null ? _party.ActiveIndex : 0;
        public Health Member(int index) => _health[index];
        public Health Active => _health[ActiveIndex];
        public float Defense(int index) => _defense[index];
        public bool IsDowned(int index) => _health[index].IsDead;
        public int AliveCount { get { int n = 0; foreach (var h in _health) if (!h.IsDead) n++; return n; } }
        public bool AllDown => AliveCount == 0;
        /// <summary>[상태 변경] 플레이어 무적 (디버그): 맞아도 체력이 줄지 않고 경직도 없다.</summary>
        public bool Invulnerable { get; set; }

        /// <summary>남은 경직 (초, 넘어짐 포함).</summary>
        public float StunLeft { get; private set; }
        public bool Stunned => StunLeft > 0f;
        /// <summary>3-4: 남은 넘어짐 (초) — 이 동안은 회피로 끊을 수 없다(EndStun이 안 됨).</summary>
        public float KnockdownLeft { get; private set; }
        public bool KnockedDown => KnockdownLeft > 0f;
        /// <summary>3-4: 밀려나는 중인지 — 맞은 뒤 HitStunTime 동안만 (넘어져도 밀려나는 거리는 같다).</summary>
        public float PushLeft { get; private set; }
        public bool Pushing => PushLeft > 0f && Stunned;
        /// <summary>경직 동안 밀려나는 속력 (m/s) = 거리 ÷ 경직 시간.</summary>
        public float KnockbackSpeed => Rules.HitStunTime > 0f ? Rules.KnockbackDistance / Rules.HitStunTime : 0f;

        public WipePhase Wipe { get; private set; }
        public bool Wiping => Wipe != WipePhase.None;
        /// <summary>지금 전멸 단계에서 지난 초.</summary>
        public float WipeElapsed { get; private set; }
        /// <summary>화면 어둡기 (0 = 그대로, 1 = 검게).</summary>
        public float Darkness
        {
            get
            {
                switch (Wipe)
                {
                    case WipePhase.FadeOut: return Rules.WipeFadeOut > 0f ? Clamp01(WipeElapsed / Rules.WipeFadeOut) : 1f;
                    case WipePhase.Dark: return 1f;
                    case WipePhase.FadeIn: return Rules.WipeFadeIn > 0f ? 1f - Clamp01(WipeElapsed / Rules.WipeFadeIn) : 0f;
                    default: return 0f;
                }
            }
        }
        /// <summary>이동 · 공격 · 교체 입력을 막는지 (경직 · 전멸 연출). 경직은 회피로 끊을 수 있다(Unity 쪽).</summary>
        public bool InputBlocked => Stunned || Wiping;

        /// <summary>기록 (디버그 · 확인용).</summary>
        public int HitCount { get; private set; }
        public int DownCount { get; private set; }
        public int WipeCount { get; private set; }
        public int RespawnCount { get; private set; }
        public int HealedTotal { get; private set; }
        public int ReviveByHealCount { get; private set; }
        public PlayerHit LastHit { get; private set; } = PlayerHit.None(0);

        /// <summary>적 공격이 필드 캐릭터에게 맞았다. canStagger = 땅 위라 경직 · 밀려남이 되는지.</summary>
        public PlayerHit TakeHit(float attack, float multiplier, float defenseConstant, bool canStagger)
            => TakeHit(attack, multiplier, defenseConstant, canStagger, 0f);

        /// <summary>3-4: knockdownTime이 있으면 땅 위에서 넘어진다 (그만큼 행동 불가, 회피로 못 끊음). 공중 · 무적 · 보호막이 다 막으면 없음.</summary>
        public PlayerHit TakeHit(float attack, float multiplier, float defenseConstant, bool canStagger, float knockdownTime)
        {
            int i = ActiveIndex;
            if (Wiping || _health[i].IsDead) return PlayerHit.None(i);
            int damage = DamageMath.Compute(attack, multiplier, _defense[i], defenseConstant);
            if (damage <= 0) return PlayerHit.None(i);
            return Apply(i, damage, canStagger, Invulnerable, true, knockdownTime);
        }

        /// <summary>[상태 변경] 방어를 거치지 않고 amount만큼 (디버그 피해 주기).</summary>
        public PlayerHit TakeDamage(int amount, bool canStagger)
        {
            int i = ActiveIndex;
            if (Wiping || _health[i].IsDead || amount <= 0) return PlayerHit.None(i);
            return Apply(i, amount, canStagger, Invulnerable, true, 0f);
        }

        /// <summary>[상태 변경] 필드 캐릭터를 쓰러뜨린다 (무적이어도).</summary>
        public PlayerHit DownActive()
        {
            int i = ActiveIndex;
            if (Wiping || _health[i].IsDead) return PlayerHit.None(i);
            return Apply(i, _health[i].Current, false, false, false, 0f);
        }

        /// <summary>[상태 변경] 전원 쓰러뜨린다 → 전멸 연출.</summary>
        public PlayerHit DownAll()
        {
            int active = ActiveIndex;
            if (Wiping) return PlayerHit.None(active);
            for (int i = 0; i < _health.Length; i++)
            {
                if (i == active || _health[i].IsDead) continue;
                _health[i].TakeDamage(_health[i].Current);
                _party?.SetDowned(i, true);
                DownCount++;
            }
            return DownActive();
        }

        /// <summary>useShield: 3-2 교체 강화 보호막이 먼저 막는다 (무적이면 보호막도 그대로). 다 막으면 경직도 없다. knockdown: 3-4 넘어짐 시간 (0 = 보통 경직).</summary>
        PlayerHit Apply(int i, int damage, bool canStagger, bool invulnerable, bool useShield, float knockdown)
        {
            Health h = _health[i];
            int absorbed = useShield && !invulnerable && _party != null ? _party.AbsorbShield(damage) : 0;
            int rest = damage - absorbed;
            h.Invulnerable = invulnerable;
            int applied = rest > 0 ? h.TakeDamage(rest) : 0;
            if (rest <= 0) canStagger = false;
            h.Invulnerable = false;
            HitCount++;
            bool stunned = false, downed = false, allDown = false, knocked = false;
            int switchedTo = -1;
            if (h.IsDead)
            {
                downed = true;
                StunLeft = 0f;
                KnockdownLeft = 0f;
                PushLeft = 0f;
                switchedTo = OnDowned(i);
                allDown = switchedTo < 0;
            }
            else if (canStagger && !invulnerable && knockdown > 0f)
            {
                knocked = stunned = StartStun(knockdown, true);
            }
            else if (canStagger && !invulnerable && Rules.HitStunTime > 0f)
            {
                stunned = StartStun(Rules.HitStunTime, false);
            }
            LastHit = new PlayerHit(i, damage, applied, false, stunned, downed, switchedTo, allDown, absorbed, knocked);
            return LastHit;
        }

        /// <summary>경직(또는 넘어짐) 시작 — 남은 것보다 짧게 줄지 않는다(넘어진 채 맞아도 넘어짐이 그대로). 밀려남은 매번 HitStunTime.</summary>
        bool StartStun(float time, bool knockdown)
        {
            if (StunLeft < time) StunLeft = time;
            if (knockdown && KnockdownLeft < time) KnockdownLeft = time;
            PushLeft = Rules.HitStunTime;
            return true;
        }

        /// <summary>[상태 변경] 피해 없이 넘어뜨린다 (디버그 넘어짐 흉내). canStagger가 false(공중)이거나 전멸 중이면 false.</summary>
        public bool Knockdown(float time, bool canStagger)
        {
            if (Wiping || Active.IsDead || !canStagger || time <= 0f) return false;
            return StartStun(time, true);
        }

        /// <summary>쓰러짐 표시 → 다음 살아 있는 칸으로 강제 교체, 없으면 전멸. 교체한 칸(없으면 -1).</summary>
        int OnDowned(int index)
        {
            DownCount++;
            _party?.SetDowned(index, true);
            int next = NextAlive(index);
            if (next >= 0)
            {
                _party?.ForceSwitch(next);
                return next;
            }
            Wipe = WipePhase.FadeOut;
            WipeElapsed = 0f;
            WipeCount++;
            return -1;
        }

        /// <summary>from 다음 칸부터 돌아 처음 만나는 살아 있는 칸 (from 자신은 빼고, 없으면 -1).</summary>
        public int NextAlive(int from)
        {
            for (int k = 1; k < _health.Length; k++)
            {
                int j = (from + k) % _health.Length;
                if (!_health[j].IsDead) return j;
            }
            return -1;
        }

        /// <summary>한 고정 스텝: 경직 · 전멸 연출. 어두워짐이 끝나는 스텝에 전원 되살아난다.</summary>
        public VitalsTick Advance(float dt)
        {
            if (StunLeft > 0f)
            {
                StunLeft -= dt;
                if (StunLeft <= Epsilon) StunLeft = 0f;
            }
            if (KnockdownLeft > 0f)
            {
                KnockdownLeft -= dt;
                if (KnockdownLeft <= Epsilon) KnockdownLeft = 0f;
            }
            if (PushLeft > 0f)
            {
                PushLeft -= dt;
                if (PushLeft <= Epsilon) PushLeft = 0f;
            }
            if (!Wiping) return default;
            WipeElapsed += dt;
            bool respawned = false, ended = false;
            if (Wipe == WipePhase.FadeOut && WipeElapsed >= Rules.WipeFadeOut - Epsilon)
            {
                WipeElapsed = Math.Max(0f, WipeElapsed - Rules.WipeFadeOut);
                Wipe = WipePhase.Dark;
                ReviveAll(Rules.ReviveRatio);
                RespawnCount++;
                respawned = true;
            }
            if (Wipe == WipePhase.Dark && WipeElapsed >= Rules.WipeDark - Epsilon)
            {
                WipeElapsed = Math.Max(0f, WipeElapsed - Rules.WipeDark);
                Wipe = WipePhase.FadeIn;
            }
            if (Wipe == WipePhase.FadeIn && WipeElapsed >= Rules.WipeFadeIn - Epsilon)
            {
                WipeElapsed = 0f;
                Wipe = WipePhase.None;
                ended = true;
            }
            return new VitalsTick(respawned, ended);
        }

        /// <summary>[상태 변경] 경직을 끝낸다 (회피로 빠져나감). 3-4: 넘어진 동안에는 끊지 못한다 — 끊었으면 true.</summary>
        public bool EndStun()
        {
            if (KnockedDown) return false;
            StunLeft = 0f;
            PushLeft = 0f;
            return true;
        }

        /// <summary>쓰러진 캐릭터를 최대 × ratio(최소 1)로 되살린다. 살아 있는 캐릭터는 그대로.</summary>
        public void ReviveAll(float ratio)
        {
            for (int i = 0; i < _health.Length; i++)
            {
                Health h = _health[i];
                if (!h.IsDead) continue;
                h.Revive(Round(h.Max * ratio));
                _party?.SetDowned(i, false);
            }
        }

        /// <summary>회복 지점 한 스텝: 전원 최대 × HealRatePerSecond × dt(최소 1)만큼, 쓰러진 캐릭터는 그만큼으로 되살린다. 전멸 연출 중에는 0. 찬 합.</summary>
        public int Heal(float dt)
        {
            if (Wiping || dt <= 0f || Rules.HealRatePerSecond <= 0f) return 0;
            int total = 0;
            for (int i = 0; i < _health.Length; i++)
            {
                Health h = _health[i];
                int amount = Round(h.Max * Rules.HealRatePerSecond * dt);
                if (h.IsDead)
                {
                    h.Revive(amount);
                    _party?.SetDowned(i, false);
                    ReviveByHealCount++;
                    total += h.Current;
                }
                else total += h.Heal(amount);
            }
            HealedTotal += total;
            return total;
        }

        /// <summary>[상태 변경] 전원 가득 채우고 되살린다 (전멸 연출은 그대로).</summary>
        public void RestoreAll()
        {
            for (int i = 0; i < _health.Length; i++)
            {
                _health[i].Restore();
                _party?.SetDowned(i, false);
            }
        }

        static int Round(float v)
        {
            int r = (int)Math.Round(v, MidpointRounding.AwayFromZero);
            return r < 1 ? 1 : r;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}

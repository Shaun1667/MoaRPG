using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 그레이박스 칼 휘두르기 (2-1 · 2-2): 칼 상자의 회전 중심(pivot)을 공격에 맞춰 돌리고, 판정 구간 동안만 궤적(TrailRenderer)을 남긴다.
    /// 콤보: 선딜 = 쉬는 자세 → 시작 자세, 판정 = 시작 → 끝, 후딜 = 끝 → 쉬는 자세. 홀수 타는 오른쪽 → 왼쪽, 짝수 타는 반대, 마지막 타는 위 → 아래.
    /// 강공격(2-2): 같은 방식으로 더 크게(charged 자세), 베기마다 방향을 바꾸고 궤적 색을 바꾼다.
    /// 낙하(2-2): 떨어지는 동안 칼을 아래로 겨누고(궤적 켬), 착지 경직 동안 쉬는 자세로 돌아온다. 착지하면 바닥에 판정 반지름만한 원을 잠깐 보인다.
    /// 스킬(2-4): 누르는 동안 강공격 시작 자세로 겨누고, 베기는 강공격 자세 + 스킬 궤적 색. 궁극기(2-4): burst 자세 + 궁극기 궤적 색, 쓰는 순간 바닥에 범위 원.
    /// 자세 각도 · 색 · 원을 보이는 시간은 Setup 표(표현 값, 게임 수치 아님). 캐릭터 모델 · 애니메이션이 오면 이것을 뺀다 — 규칙(PlayerCombat)은 이것을 모른다.
    /// </summary>
    public sealed class SwordSwingVisual : MonoBehaviour
    {
        const int RingSegments = 48;
        const float RingLift = 0.03f; // 바닥에서 띄우는 높이 (m, z-싸움 방지)

        [SerializeField] PlayerCombat combat;
        [Tooltip("Rotated transform (blade is its child, pointing along local +Z)")]
        [SerializeField] Transform pivot;
        [SerializeField] TrailRenderer trail;
        [Tooltip("Local euler of the pivot at rest")]
        [SerializeField] Vector3 restEuler;
        [Tooltip("Side swing: start and end local euler (odd hits; even hits go the other way)")]
        [SerializeField] Vector3 sweepStartEuler;
        [SerializeField] Vector3 sweepEndEuler;
        [Tooltip("Last hit: start and end local euler")]
        [SerializeField] Vector3 finisherStartEuler;
        [SerializeField] Vector3 finisherEndEuler;

        [Header("Charged attack (2-2)")]
        [Tooltip("Big swing: start and end local euler (first slash; the second goes the other way)")]
        [SerializeField] Vector3 chargedStartEuler;
        [SerializeField] Vector3 chargedEndEuler;
        [SerializeField] Color chargedTrailColor;

        [Header("Plunge attack (2-2)")]
        [Tooltip("Local euler while falling (blade pointing down)")]
        [SerializeField] Vector3 plungeEuler;
        [Tooltip("World-space loop drawn on the ground at the landing spot")]
        [SerializeField] LineRenderer landRing;
        [Tooltip("s the landing ring stays")]
        [SerializeField] float landRingTime;

        [Header("Skill / burst (2-4)")]
        [Tooltip("Skill slashes use the charged poses with this trail color")]
        [SerializeField] Color skillTrailColor;
        [Tooltip("Burst slash: start and end local euler")]
        [SerializeField] Vector3 burstStartEuler;
        [SerializeField] Vector3 burstEndEuler;
        [SerializeField] Color burstTrailColor;
        [Tooltip("Ground ring color shown when the burst is cast (radius = burst hit range)")]
        [SerializeField] Color burstRingColor;
        [Tooltip("s the burst ring stays")]
        [SerializeField] float burstRingTime;

        [Header("Switch entry attack (3-2)")]
        [Tooltip("Entry (dash) slash uses the charged poses with this trail color")]
        [SerializeField] Color entryTrailColor;


        Gradient _normalGradient;
        Gradient _chargedGradient;
        Gradient _skillGradient;
        Gradient _burstGradient;
        Gradient _entryGradient;
        int _trailKind; // 0 콤보 · 1 강공격 · 2 스킬 · 3 궁극기 · 4 등장 공격 (3-2)
        float _ringTime;
        float _ringLeft;
        Color _landColor;
        Color _ringColor;
        readonly Vector3[] _ringPoints = new Vector3[RingSegments];

        /// <summary>궤적을 남기는 중인지 (디버그 · 확인용).</summary>
        public bool TrailOn => trail != null && trail.emitting;
        /// <summary>궤적이 강공격 색인지.</summary>
        public bool ChargedTrail => _trailKind == 1;
        /// <summary>궤적 색 종류 (0 콤보 · 1 강공격 · 2 스킬 · 3 궁극기, 2-4).</summary>
        public int TrailKind => _trailKind;
        /// <summary>착지 원이 보이는 중인지.</summary>
        public bool RingShown => landRing != null && landRing.enabled;

        void Awake()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(restEuler);
            if (trail != null)
            {
                trail.emitting = false;
                _normalGradient = trail.colorGradient;
                _chargedGradient = Fade(chargedTrailColor);
                _skillGradient = Fade(skillTrailColor);
                _burstGradient = Fade(burstTrailColor);
                _entryGradient = Fade(entryTrailColor);
            }
            if (landRing != null)
            {
                landRing.useWorldSpace = true;
                landRing.loop = true;
                landRing.positionCount = RingSegments;
                _landColor = landRing.startColor;
                _ringColor = landRing.startColor;
                landRing.enabled = false;
            }
        }

        void OnEnable() { if (combat != null) { combat.PlungeLanded += OnPlungeLanded; combat.BurstCast += OnBurstCast; } }
        void OnDisable() { if (combat != null) { combat.PlungeLanded -= OnPlungeLanded; combat.BurstCast -= OnBurstCast; } }

        static Gradient Fade(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void OnBurstCast(Vector3 feet, float radius) => ShowRing(feet, radius, burstRingColor, burstRingTime);

        void OnPlungeLanded(Vector3 feet, float radius) => ShowRing(feet, radius, _landColor, landRingTime);

        void ShowRing(Vector3 feet, float radius, Color color, float time)
        {
            if (landRing == null) return;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                _ringPoints[i] = feet + new Vector3(Mathf.Cos(a) * radius, RingLift, Mathf.Sin(a) * radius);
            }
            landRing.SetPositions(_ringPoints);
            _ringColor = color;
            _ringTime = time;
            _ringLeft = time;
            SetRingAlpha(1f);
            landRing.enabled = true;
        }

        void LateUpdate()
        {
            UpdateRing();
            if (pivot == null || combat == null) return;
            PlayerAttack a = combat.Attacks;
            Quaternion rest = Quaternion.Euler(restEuler);
            if (a == null || a.Mode == AttackMode.None)
            {
                pivot.localRotation = rest;
                SetTrail(false, 0);
                return;
            }

            if (a.Plunge.Active)
            {
                Quaternion down = Quaternion.Euler(plungeEuler);
                if (a.Plunge.Falling) { pivot.localRotation = down; SetTrail(true, 0); }
                else
                {
                    float total = a.Plunge.Rules.LandRecovery;
                    float k = total > 0f ? 1f - a.Plunge.RecoveryLeft / total : 1f;
                    pivot.localRotation = Quaternion.Slerp(down, rest, Smooth(k));
                    SetTrail(false, 0);
                }
                return;
            }

            // 2-4: 스킬을 누르는 중 = 강공격 시작 자세로 겨눈다 (길게 눌러 갈수록 가까이)
            if (a.Skill.Pressing)
            {
                float hold = a.Skill.Rules.HoldTime;
                float k = hold > 0f ? a.Skill.PressElapsed / hold : 1f;
                pivot.localRotation = Quaternion.Slerp(rest, Quaternion.Euler(chargedStartEuler), Smooth(k));
                SetTrail(false, 0);
                return;
            }

            ComboStepRules s;
            Quaternion start, end;
            float t;
            int kind;
            if (a.Burst.Active)
            {
                s = a.Burst.CurrentSlash;
                start = Quaternion.Euler(burstStartEuler);
                end = Quaternion.Euler(burstEndEuler);
                t = a.Burst.Elapsed;
                kind = 3;
            }
            else if (a.Entry.Active)
            {
                // 3-2: 등장 돌진 베기 = 강공격 첫 베기 자세 + 등장 궤적 색
                s = a.Entry.CurrentSlash;
                start = Quaternion.Euler(chargedStartEuler);
                end = Quaternion.Euler(chargedEndEuler);
                t = a.Entry.Elapsed;
                kind = 4;
            }
            else if (a.Skill.Casting || a.Charged.Active)
            {
                bool skill = a.Skill.Casting;
                s = skill ? a.Skill.CurrentSlash : a.Charged.CurrentSlash;
                bool back = (skill ? a.Skill.SlashIndex : a.Charged.SlashIndex) % 2 == 1;
                start = Quaternion.Euler(back ? chargedEndEuler : chargedStartEuler);
                end = Quaternion.Euler(back ? chargedStartEuler : chargedEndEuler);
                t = skill ? a.Skill.Elapsed : a.Charged.Elapsed;
                kind = skill ? 2 : 1;
            }
            else
            {
                ComboAttack combo = a.Combo;
                s = combo.CurrentStep;
                bool last = combo.IsLastStep;
                bool reverse = !last && combo.StepIndex % 2 == 1;
                start = Quaternion.Euler(last ? finisherStartEuler : reverse ? sweepEndEuler : sweepStartEuler);
                end = Quaternion.Euler(last ? finisherEndEuler : reverse ? sweepStartEuler : sweepEndEuler);
                t = combo.Elapsed;
                kind = 0;
            }

            Quaternion q;
            bool active = false;
            if (t < s.Windup) q = Quaternion.Slerp(rest, start, Smooth(s.Windup > 0f ? t / s.Windup : 1f));
            else if (t < s.ActiveEnd) { q = Quaternion.Slerp(start, end, Smooth(s.Active > 0f ? (t - s.Windup) / s.Active : 1f)); active = true; }
            else q = Quaternion.Slerp(end, rest, Smooth(s.Recovery > 0f ? (t - s.ActiveEnd) / s.Recovery : 1f));
            pivot.localRotation = q;
            SetTrail(active, kind);
        }

        void UpdateRing()
        {
            if (landRing == null || !landRing.enabled) return;
            _ringLeft -= Time.deltaTime;
            if (_ringLeft <= 0f) { landRing.enabled = false; return; }
            SetRingAlpha(_ringTime > 0f ? Mathf.Clamp01(_ringLeft / _ringTime) : 0f);
        }

        void SetRingAlpha(float alpha)
        {
            Color c = _ringColor;
            c.a *= alpha;
            landRing.startColor = c;
            landRing.endColor = c;
        }

        /// <summary>궤적 켜기 · 끄기, kind = 0 콤보 · 1 강공격 · 2 스킬 · 3 궁극기 (색).</summary>
        void SetTrail(bool on, int kind)
        {
            if (trail == null) return;
            if (on && kind != _trailKind && _chargedGradient != null)
            {
                _trailKind = kind;
                trail.colorGradient = kind == 1 ? _chargedGradient : kind == 2 ? _skillGradient : kind == 3 ? _burstGradient : kind == 4 ? _entryGradient : _normalGradient;
            }
            if (trail.emitting == on) return;
            trail.emitting = on;
            if (on) trail.Clear();
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}

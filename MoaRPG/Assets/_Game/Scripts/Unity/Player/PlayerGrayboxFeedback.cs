using UnityEngine;

namespace Moa.Unity.Player
{
    /// <summary>
    /// 그레이박스 상태 표시 (1-2 · 1-3 · 2-1, 공격은 회피 다음): 캡슐 색으로 무적 · 회피 · 등반 · 활공 · 지침 · 질주를 보인다 (앞의 것이 우선),
    /// 활공 중에는 날개 상자(wings)를 보인다.
    /// 색은 Setup 메뉴가 정한다(표현 값, 게임 수치 아님). 머티리얼은 그대로 두고 MaterialPropertyBlock으로 바꾼다.
    /// 캐릭터 모델이 오면 이 컴포넌트를 빼거나 이펙트로 바꾼다 — PlayerMotor는 이것을 모른다.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerGrayboxFeedback : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer body;
        [SerializeField] Color sprintColor;
        [SerializeField] Color dodgeColor;
        [SerializeField] Color invulnerableColor;
        [SerializeField] Color exhaustedColor;
        [SerializeField] Color climbColor;
        [SerializeField] Color glideColor;
        [Tooltip("Normal attack in progress (2-1)")]
        [SerializeField] Color attackColor;
        [Tooltip("Charged attack in progress (2-2)")]
        [SerializeField] Color chargedColor;
        [Tooltip("Plunge attack: falling and landing recovery (2-2)")]
        [SerializeField] Color plungeColor;
        [Tooltip("Skill (E): pressing and casting (2-4)")]
        [SerializeField] Color skillColor;
        [Tooltip("Burst (Q), invulnerable (2-4)")]
        [SerializeField] Color burstColor;
        [Tooltip("Hit stun after an enemy hit (3-1)")]
        [SerializeField] Color hitColor;
        [Tooltip("Everyone down, while the screen is dark (3-1)")]
        [SerializeField] Color downedColor;
        [Tooltip("Knocked down by a heavy enemy hit (3-4) - the body also lies down")]
        [SerializeField] Color knockdownColor;
        [Tooltip("Degrees the body capsule tips over while knocked down (visual only)")]
        [SerializeField] float knockdownTilt;
        [Tooltip("Swimming (3-6), after climb / glide")]
        [SerializeField] Color swimColor;

        [Tooltip("Glider visual, shown only while gliding (1-3)")]
        [SerializeField] GameObject wings;

        PlayerMotor _motor;
        MaterialPropertyBlock _block;
        Vector3 _bodyPos;
        Quaternion _bodyRot = Quaternion.identity;
        bool _lying;
        int _shown = -1;
        Moa.Unity.Data.CharacterData _shownCharacter; // 2-6: 보통 상태 색 = 현재 캐릭터 색 (파티가 없으면 머티리얼 색)

        /// <summary>지금 몸 색을 정한 캐릭터 (2-6, 없으면 null) — 디버그 · 확인용.</summary>
        public Moa.Unity.Data.CharacterData ShownCharacter => _shownCharacter;

        /// <summary>지금 보이는 상태 (0 보통 · 1 질주 · 2 지침 · 3 회피 · 4 무적 · 5 등반 · 6 활공 · 7 공격 · 8 강공격 · 9 낙하 · 10 스킬 · 11 궁극기 · 12 피격 · 13 전멸 · 14 넘어짐 · 15 수영(3-6, 활공 다음)) — 디버그 · 확인용.
        /// 우선순위: 회피 무적 > 궁극기 > 회피 > 스킬 > 낙하 > 강공격 > 공격 > 등반 > 활공 > 지침 > 질주 (2-4).</summary>
        public int ShownState => _shown;
        public bool WingsShown => wings != null && wings.activeSelf;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _block = new MaterialPropertyBlock();
            if (wings != null) wings.SetActive(false);
            if (body != null) { _bodyPos = body.transform.localPosition; _bodyRot = body.transform.localRotation; }
        }

        void LateUpdate()
        {
            if (_motor == null) return;
            if (wings != null && wings.activeSelf != _motor.Gliding) wings.SetActive(_motor.Gliding);
            if (body == null) return;
            // 3-4: 넘어지면 몸 캡슐을 뒤로 눕힌다 (발을 중심으로, 표현만)
            bool lying = _motor.KnockedDown;
            if (lying != _lying)
            {
                _lying = lying;
                Transform t = body.transform;
                if (lying)
                {
                    Quaternion tip = Quaternion.Euler(-knockdownTilt, 0f, 0f);
                    t.localRotation = tip * _bodyRot;
                    t.localPosition = tip * _bodyPos;
                }
                else
                {
                    t.localRotation = _bodyRot;
                    t.localPosition = _bodyPos;
                }
            }
            // 3-1: 전멸 13 > 회피 무적 > 궁극기 > 넘어짐 14 (3-4) > 피격 경직 12 > 회피 > …
            int state = _motor.Wiped ? 13 : _motor.Invulnerable ? 4 : _motor.Bursting ? 11 : _motor.KnockedDown ? 14 : _motor.HitStunned ? 12 : _motor.Dodging ? 3 : _motor.SkillActive ? 10 : _motor.Plunging ? 9 : _motor.ChargedAttacking ? 8 : _motor.Attacking ? 7 : _motor.Climbing ? 5 : _motor.Gliding ? 6 : _motor.Swimming ? 15
                : _motor.Exhausted ? 2 : _motor.Sprinting ? 1 : 0;
            var character = _motor.Combat != null ? _motor.Combat.ActiveCharacter : null;
            if (character != _shownCharacter) { _shownCharacter = character; _shown = -1; } // 2-6: 교체하면 색을 다시
            if (state == _shown) return;
            _shown = state;
            if (state == 0 && character == null) { body.SetPropertyBlock(null); return; }
            if (state == 0)
            {
                body.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, character.Color);
                body.SetPropertyBlock(_block);
                return;
            }
            Color c = state == 14 ? knockdownColor : state == 13 ? downedColor : state == 12 ? hitColor : state == 4 ? invulnerableColor : state == 11 ? burstColor : state == 10 ? skillColor : state == 3 ? dodgeColor : state == 9 ? plungeColor : state == 8 ? chargedColor : state == 7 ? attackColor : state == 5 ? climbColor : state == 6 ? glideColor : state == 15 ? swimColor
                : state == 2 ? exhaustedColor : sprintColor;
            body.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            body.SetPropertyBlock(_block);
        }
    }
}

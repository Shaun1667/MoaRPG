using UnityEngine;

namespace Moa.Unity.Player
{
    /// <summary>
    /// 그레이박스 상태 표시 (1-2 · 1-3): 캡슐 색으로 무적 · 회피 · 등반 · 활공 · 지침 · 질주를 보인다 (앞의 것이 우선),
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
        [Tooltip("Glider visual, shown only while gliding (1-3)")]
        [SerializeField] GameObject wings;

        PlayerMotor _motor;
        MaterialPropertyBlock _block;
        int _shown = -1;

        /// <summary>지금 보이는 상태 (0 보통 · 1 질주 · 2 지침 · 3 회피 · 4 무적 · 5 등반 · 6 활공) — 디버그 · 확인용.</summary>
        public int ShownState => _shown;
        public bool WingsShown => wings != null && wings.activeSelf;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _block = new MaterialPropertyBlock();
            if (wings != null) wings.SetActive(false);
        }

        void LateUpdate()
        {
            if (_motor == null) return;
            if (wings != null && wings.activeSelf != _motor.Gliding) wings.SetActive(_motor.Gliding);
            if (body == null) return;
            int state = _motor.Invulnerable ? 4 : _motor.Dodging ? 3 : _motor.Climbing ? 5 : _motor.Gliding ? 6
                : _motor.Exhausted ? 2 : _motor.Sprinting ? 1 : 0;
            if (state == _shown) return;
            _shown = state;
            if (state == 0) { body.SetPropertyBlock(null); return; }
            Color c = state == 4 ? invulnerableColor : state == 3 ? dodgeColor : state == 5 ? climbColor : state == 6 ? glideColor
                : state == 2 ? exhaustedColor : sprintColor;
            body.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            body.SetPropertyBlock(_block);
        }
    }
}

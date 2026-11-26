using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 회복 지점 (3-1, 원신 일곱 신상 같은 것): 플레이어가 반지름 안(평면 거리 · 높이 차)에 서 있으면 고정 스텝마다 파티 전원 회복 · 쓰러진 캐릭터 부활.
    /// 규칙은 Core PartyVitals.Heal, 반지름 · 회복 빠르기는 PlayerHealthTuning(BALANCE 13번). 바닥 원판 색은 Setup 표(회복 중이면 healingColor).
    /// 배치는 TestFieldSetup 표 한 줄. GameRoot가 Bind로 플레이어를 준다.
    /// </summary>
    public sealed class HealingPoint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Floor disc (color shows healing)")]
        [SerializeField] Renderer disc;
        [SerializeField] Color idleColor;
        [SerializeField] Color healingColor;

        PlayerVitals _vitals;
        MaterialPropertyBlock _block;
        int _shown = -1;

        /// <summary>지난 고정 스텝에 플레이어가 안에 있었는지 · 그때 찬 양 · 지금까지 찬 합 (디버그 · 확인용).</summary>
        public bool PlayerInside { get; private set; }
        public int LastHealed { get; private set; }
        public int HealedTotal { get; private set; }
        public float Radius => _vitals != null && _vitals.Tuning != null ? _vitals.Tuning.HealPointRadius : 0f;

        public void Bind(PlayerVitals vitals) => _vitals = vitals;

        void FixedUpdate()
        {
            PlayerInside = false;
            LastHealed = 0;
            if (_vitals == null || !_vitals.Bound) return;
            float r = Radius;
            Vector3 d = _vitals.transform.position - transform.position;
            if (Mathf.Abs(d.y) > r || d.x * d.x + d.z * d.z > r * r) return;
            PlayerInside = true;
            LastHealed = _vitals.HealTick(Time.fixedDeltaTime);
            HealedTotal += LastHealed;
        }

        void LateUpdate()
        {
            if (disc == null) return;
            int want = PlayerInside && LastHealed > 0 ? 1 : 0;
            if (want == _shown) return;
            _shown = want;
            if (_block == null) _block = new MaterialPropertyBlock();
            disc.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, want == 1 ? healingColor : idleColor);
            disc.SetPropertyBlock(_block);
        }
    }
}

using UnityEngine;

namespace Moa.Unity.Player
{
    /// <summary>
    /// 플레이어 입력 한 곳의 창구 (1-1). 실제 읽기는 Moa.Unity.Input 어셈블리의 PlayerInputReader 하나가 한다
    /// (Input System 참조를 그 어셈블리에만 둔다). 이동 · 카메라는 이 인터페이스만 본다.
    /// 디버그 주입(DebugXxx)은 GameRoot.DebugInputXxx가 부른다 — 주입 중에는 실제 장치 입력 대신 주입 값을 쓴다.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>이동 입력 (x = 오른쪽, y = 앞), 길이 0 ~ 1.</summary>
        Vector2 Move { get; }

        /// <summary>걷기 모드 (전환 키로 켜고 끈다). 꺼져 있으면 뛰기.</summary>
        bool WalkMode { get; }

        /// <summary>이번 프레임 마우스 이동량 (픽셀). 커서가 풀려 있으면 0.</summary>
        Vector2 LookMouseDelta { get; }

        /// <summary>패드 오른쪽 스틱 (-1 ~ 1).</summary>
        Vector2 LookStick { get; }

        /// <summary>이번 프레임 휠 값 (부호만 의미 있음, 위 = 양수).</summary>
        float ZoomScroll { get; }

        bool CursorCaptured { get; }

        /// <summary>눌린 점프를 한 번 꺼낸다 (FixedUpdate에서 소비).</summary>
        bool ConsumeJump();

        /// <summary>질주 키를 누르고 있는지 (1-2).</summary>
        bool SprintHeld { get; }

        /// <summary>눌린 회피를 한 번 꺼낸다 (FixedUpdate에서 소비, 1-2).</summary>
        bool ConsumeDodge();

        /// <summary>눌린 일반 공격을 한 번 꺼낸다 (FixedUpdate에서 소비, 2-1). 마우스 왼쪽은 커서가 잠겨 있을 때만 공격 — 풀려 있으면 그 클릭은 커서 잠금.</summary>
        bool ConsumeAttack();

        /// <summary>공격 버튼을 누르고 있는지 (길게 누르면 강공격, 2-2). 마우스 왼쪽은 커서가 잠겨 있을 때만.</summary>
        bool AttackHeld { get; }

        /// <summary>눌린 스킬(E)을 한 번 꺼낸다 (FixedUpdate에서 소비, 2-4). 입력 에셋에 Skill 액션이 없으면 주입만.</summary>
        bool ConsumeSkill();

        /// <summary>스킬 버튼을 누르고 있는지 (길게 누르면 길게 누르기 형태, 2-4).</summary>
        bool SkillHeld { get; }

        /// <summary>눌린 궁극기(Q)를 한 번 꺼낸다 (FixedUpdate에서 소비, 2-4). 입력 에셋에 Burst 액션이 없으면 주입만.</summary>
        bool ConsumeBurst();

        /// <summary>눌린 파티 교체를 한 번 꺼낸다 (FixedUpdate에서 소비, 2-6): 1 ~ 4 = 칸 번호, 0 = 없음. 같은 프레임에 여럿이면 마지막 것. 입력 에셋에 Party1 ~ 4가 없으면 주입만.</summary>
        int ConsumeSwitch();

        /// <summary>눌린 캐릭터 화면 열기/닫기를 한 번 꺼낸다 (3-5, 화면이 Update에서). 화면이 열려 있으면 ESC도 닫기. 입력 에셋에 CharacterMenu가 없으면 주입만.</summary>
        bool ConsumeCharacterMenu();

        /// <summary>
        /// 메뉴(캐릭터 화면)가 열려 있는지 (3-5, 화면이 정한다). 열려 있으면 커서를 풀고, 클릭은 버튼에만 —
        /// 이동 · 시점 · 줌 · 점프 · 회피 · 공격 · 스킬 · 교체 입력은 0 (메뉴 키만 받는다). 닫으면 커서를 다시 잠근다.
        /// </summary>
        bool MenuOpen { get; set; }

        /// <summary>
        /// 눌린 지도 화면 열기/닫기를 한 번 꺼낸다 (3-6, 화면이 Update에서). 메뉴가 열려 있으면 ESC도 (캐릭터 화면 키와 함께 — 열린 화면만 닫힌다).
        /// MenuOpen이 바뀔 때 받아 둔 메뉴 키는 버린다. 입력 에셋에 Map이 없으면 주입만.
        /// </summary>
        bool ConsumeMap();

        /// <summary>눌린 가챠 화면 열기/닫기를 한 번 꺼낸다 (3-7, 화면이 Update에서). 메뉴가 열려 있으면 ESC도. 입력 에셋에 Gacha가 없으면 주입만.</summary>
        bool ConsumeGacha();


        // ---- 디버그 주입 ----
        bool DebugOverriding { get; }
        void DebugSetMove(Vector2 move);
        void DebugAddLook(Vector2 mousePixels);
        void DebugPressJump();
        void DebugZoom(float scroll);
        void DebugSetWalk(bool walk);
        void DebugSetSprint(bool sprint);
        void DebugPressDodge();
        void DebugPressAttack();
        /// <summary>공격을 누르고, 누른 스텝 뒤로 seconds 동안 누른 채로 둔다 (고정 스텝 시간 기준, 2-2).</summary>
        void DebugHoldAttack(float seconds);
        /// <summary>스킬을 짧게 눌렀다 떼다 (2-4).</summary>
        void DebugPressSkill();
        /// <summary>스킬을 누르고, 누른 스텝 뒤로 seconds 동안 누른 채로 둔다 (고정 스텝 시간 기준, 2-4).</summary>
        void DebugHoldSkill(float seconds);
        /// <summary>궁극기를 누른다 (2-4).</summary>
        void DebugPressBurst();
        /// <summary>파티 교체를 누른다 (slot 1 ~ 4, 2-6).</summary>
        void DebugPressSwitch(int slot);
        /// <summary>캐릭터 화면 키를 누른다 (3-5).</summary>
        void DebugPressCharacterMenu();
        /// <summary>지도 화면 키를 누른다 (3-6).</summary>
        void DebugPressMap();
        /// <summary>가챠 화면 키를 누른다 (3-7).</summary>
        void DebugPressGacha();

        void DebugClear();
    }
}

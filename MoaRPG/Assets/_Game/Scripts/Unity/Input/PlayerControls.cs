using UnityEngine;
using UnityEngine.InputSystem;

namespace Moa.Unity.Input
{
    /// <summary>
    /// 플레이어 액션 이름과 기본 키 배치 (1-1). 에셋 Assets/_Game/Input/MoaControls.inputactions는 Setup 메뉴가
    /// 없을 때만 <see cref="BuildDefaultJson"/>로 만든다 — 만든 뒤 키를 바꾸려면 에셋을 고친다.
    /// 새 액션 = 이름 상수 한 줄 + 여기 바인딩 한 줄 + (에셋이 이미 있으면) 에셋에 액션 추가.
    /// </summary>
    public static class PlayerControls
    {
        public const string AssetName = "MoaControls";
        public const string MapName = "Player";
        public const string Move = "Move";
        public const string Look = "Look";
        public const string LookStick = "LookStick";
        public const string Zoom = "Zoom";
        public const string Jump = "Jump";
        public const string WalkToggle = "WalkToggle";
        public const string ReleaseCursor = "ReleaseCursor";
        public const string CaptureCursor = "CaptureCursor";
        public const string Sprint = "Sprint";
        public const string Dodge = "Dodge";
        public const string Attack = "Attack";
        public const string Skill = "Skill"; // 2-4: 스킬(E)
        public const string Burst = "Burst"; // 2-4: 궁극기(Q)
        public const string CharacterMenu = "CharacterMenu"; // 3-5: 캐릭터(성장) 화면 열기/닫기 (C · 패드 View)
        /// <summary>2-6: 파티 교체 칸 1 ~ 4 (키 1 · 2 · 3 · 4, 패드 십자 위 · 오른쪽 · 아래 · 왼쪽).</summary>
        public static readonly string[] PartySlots = { "Party1", "Party2", "Party3", "Party4" };
        static readonly string[] PartyKeys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4" };
        static readonly string[] PartyPads = { "<Gamepad>/dpad/up", "<Gamepad>/dpad/right", "<Gamepad>/dpad/down", "<Gamepad>/dpad/left" };


        /// <summary>기본 키: WASD · 방향키 / 왼쪽 스틱 이동, 마우스 · 오른쪽 스틱 시점, 휠 줌, Space · A(South) 점프, 왼쪽 Ctrl · 왼쪽 스틱 누르기 걷기 전환, ESC 커서 풀기, 왼쪽 클릭 커서 잠그기, 왼쪽 Shift · RT 누르는 동안 질주, 오른쪽 클릭 · B(East) 회피 (1-2), 왼쪽 클릭(커서가 잠겼을 때) · X(West) 일반 공격 (2-1).</summary>
        public static string BuildDefaultJson()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = AssetName;
            var map = asset.AddActionMap(MapName);

            var move = map.AddAction(Move, InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            map.AddAction(Look, InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            map.AddAction(LookStick, InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            map.AddAction(Zoom, InputActionType.Value, "<Mouse>/scroll/y", expectedControlLayout: "Axis");

            var jump = map.AddAction(Jump, InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            var walk = map.AddAction(WalkToggle, InputActionType.Button, "<Keyboard>/leftCtrl");
            walk.AddBinding("<Gamepad>/leftStickPress");

            map.AddAction(ReleaseCursor, InputActionType.Button, "<Keyboard>/escape");
            map.AddAction(CaptureCursor, InputActionType.Button, "<Mouse>/leftButton");

            var sprint = map.AddAction(Sprint, InputActionType.Button, "<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/rightTrigger");

            var dodge = map.AddAction(Dodge, InputActionType.Button, "<Mouse>/rightButton");
            dodge.AddBinding("<Gamepad>/buttonEast");

            // 2-1: 일반 공격 — 마우스 왼쪽은 커서가 잠겨 있을 때만 공격(PlayerInputReader), 풀려 있으면 CaptureCursor
            var attack = map.AddAction(Attack, InputActionType.Button, "<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");

            // 2-4: 스킬(E · 패드 RB) · 궁극기(Q · 패드 Y) — 에셋에 없으면 PlayerInputReader가 조용히 건너뛴다
            var skill = map.AddAction(Skill, InputActionType.Button, "<Keyboard>/e");
            skill.AddBinding("<Gamepad>/rightShoulder");
            var burst = map.AddAction(Burst, InputActionType.Button, "<Keyboard>/q");
            burst.AddBinding("<Gamepad>/buttonNorth");

            // 2-6: 파티 교체 — 에셋에 없으면 PlayerInputReader가 조용히 건너뛴다
            for (int i = 0; i < PartySlots.Length; i++)
            {
                var slot = map.AddAction(PartySlots[i], InputActionType.Button, PartyKeys[i]);
                slot.AddBinding(PartyPads[i]);
            }


            // 3-5: 캐릭터 화면 — 에셋에 없으면 PlayerInputReader가 조용히 건너뛴다
            var menu = map.AddAction(CharacterMenu, InputActionType.Button, "<Keyboard>/c");
            menu.AddBinding("<Gamepad>/select");

            string json = asset.ToJson();
            Object.DestroyImmediate(asset);
            return json;
        }
    }
}

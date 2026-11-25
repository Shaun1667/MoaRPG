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

        /// <summary>기본 키: WASD · 방향키 / 왼쪽 스틱 이동, 마우스 · 오른쪽 스틱 시점, 휠 줌, Space · A(South) 점프, 왼쪽 Ctrl · 왼쪽 스틱 누르기 걷기 전환, ESC 커서 풀기, 왼쪽 클릭 커서 잠그기, 왼쪽 Shift · RT 누르는 동안 질주, 오른쪽 클릭 · B(East) 회피 (1-2).</summary>
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

            string json = asset.ToJson();
            Object.DestroyImmediate(asset);
            return json;
        }
    }
}

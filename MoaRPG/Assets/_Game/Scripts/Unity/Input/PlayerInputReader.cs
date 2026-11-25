using Moa.Unity.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Moa.Unity.Input
{
    /// <summary>
    /// 플레이어 입력은 여기 한 곳에서만 읽는다 (CLAUDE.md 코딩 규칙). 액션 에셋 Assets/_Game/Input/MoaControls.inputactions.
    /// 커서: 시작할 때 잠그고 ESC로 풀고, 왼쪽 클릭으로 다시 잠근다. 커서가 풀려 있으면 마우스 시점 입력은 0.
    /// 디버그 주입 중(DebugOverriding)에는 장치 대신 주입 값을 쓴다 — 에디터 포커스 없이 확인할 때 쓴다.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap _map;
        InputAction _move, _look, _lookStick, _zoom, _jump, _walkToggle, _releaseCursor, _captureCursor, _sprint, _dodge;

        bool _walkMode;
        bool _jumpLatched;
        bool _dodgeLatched;
        Vector2 _lookMouse;
        Vector2 _lookStickValue;
        float _zoomValue;

        bool _override;
        Vector2 _ovMove;
        Vector2 _ovLookPending;
        float _ovZoomPending;
        bool _ovSprint;

        public Vector2 Move => _override ? Vector2.ClampMagnitude(_ovMove, 1f) : (_move != null ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero);
        public bool WalkMode => _walkMode;
        public Vector2 LookMouseDelta => _lookMouse;
        public Vector2 LookStick => _lookStickValue;
        public float ZoomScroll => _zoomValue;
        public bool CursorCaptured => Cursor.lockState == CursorLockMode.Locked;
        public bool DebugOverriding => _override;
        public bool SprintHeld => _override ? _ovSprint : (_sprint != null && _sprint.IsPressed());

        void Awake()
        {
            if (actions == null) { Debug.LogError("PlayerInputReader: input action asset is not assigned"); enabled = false; return; }
            _map = actions.FindActionMap(PlayerControls.MapName, true);
            _move = _map.FindAction(PlayerControls.Move, true);
            _look = _map.FindAction(PlayerControls.Look, true);
            _lookStick = _map.FindAction(PlayerControls.LookStick, true);
            _zoom = _map.FindAction(PlayerControls.Zoom, true);
            _jump = _map.FindAction(PlayerControls.Jump, true);
            _walkToggle = _map.FindAction(PlayerControls.WalkToggle, true);
            _releaseCursor = _map.FindAction(PlayerControls.ReleaseCursor, true);
            _captureCursor = _map.FindAction(PlayerControls.CaptureCursor, true);
            _sprint = _map.FindAction(PlayerControls.Sprint, false);
            _dodge = _map.FindAction(PlayerControls.Dodge, false);
            if (_sprint == null || _dodge == null)
                Debug.LogWarning("PlayerInputReader: action '" + (_sprint == null ? PlayerControls.Sprint : PlayerControls.Dodge) + "' is missing in " + actions.name + " - add Sprint/Dodge (see PlayerControls.BuildDefaultJson). Debug injection still works.");
        }

        void OnEnable()
        {
            _map?.Enable();
            SetCursorCaptured(true);
        }

        void OnDisable()
        {
            _map?.Disable();
            SetCursorCaptured(false);
        }

        void Update()
        {
            if (_map == null) return;

            // 커서
            if (_releaseCursor.WasPressedThisFrame()) SetCursorCaptured(false);
            else if (!CursorCaptured && _captureCursor.WasPressedThisFrame()) SetCursorCaptured(true);

            // 버튼은 프레임 단위로 받아 둔다 (점프는 FixedUpdate가 꺼낼 때까지 유지)
            if (_jump.WasPressedThisFrame()) _jumpLatched = true;
            if (_dodge != null && _dodge.WasPressedThisFrame() && !_override) _dodgeLatched = true;
            if (_walkToggle.WasPressedThisFrame()) _walkMode = !_walkMode;

            if (_override)
            {
                _lookMouse = _ovLookPending;
                _lookStickValue = Vector2.zero;
                _zoomValue = _ovZoomPending;
                _ovLookPending = Vector2.zero;
                _ovZoomPending = 0f;
            }
            else
            {
                _lookMouse = CursorCaptured ? _look.ReadValue<Vector2>() : Vector2.zero;
                _lookStickValue = Vector2.ClampMagnitude(_lookStick.ReadValue<Vector2>(), 1f);
                _zoomValue = _zoom.ReadValue<float>();
            }
        }

        public bool ConsumeJump()
        {
            bool j = _jumpLatched;
            _jumpLatched = false;
            return j;
        }

        public bool ConsumeDodge()
        {
            bool d = _dodgeLatched;
            _dodgeLatched = false;
            return d;
        }

        static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        // ---- 디버그 주입 (GameRoot.DebugInputXxx가 부른다) ----

        public void DebugSetMove(Vector2 move) { _override = true; _ovMove = move; }
        public void DebugAddLook(Vector2 mousePixels) { _override = true; _ovLookPending += mousePixels; }
        public void DebugPressJump() { _jumpLatched = true; }
        public void DebugZoom(float scroll) { _override = true; _ovZoomPending += scroll; }
        public void DebugSetWalk(bool walk) { _walkMode = walk; }
        public void DebugSetSprint(bool sprint) { _override = true; _ovSprint = sprint; }
        public void DebugPressDodge() { _dodgeLatched = true; }

        public void DebugClear()
        {
            _override = false;
            _ovMove = Vector2.zero;
            _ovLookPending = Vector2.zero;
            _ovZoomPending = 0f;
            _ovSprint = false;
        }
    }
}

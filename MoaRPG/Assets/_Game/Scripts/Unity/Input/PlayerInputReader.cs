using Moa.Unity.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Moa.Unity.Input
{
    /// <summary>
    /// 플레이어 입력은 여기 한 곳에서만 읽는다 (CLAUDE.md 코딩 규칙). 액션 에셋 Assets/_Game/Input/MoaControls.inputactions.
    /// 커서: 시작할 때 잠그고 ESC로 풀고, 왼쪽 클릭으로 다시 잠근다. 커서가 풀려 있으면 마우스 시점 입력은 0.
    /// 공격(2-1): 커서가 잠겨 있을 때의 왼쪽 클릭 · 패드 X. 커서가 풀려 있을 때의 왼쪽 클릭은 커서를 잠그기만 한다.
    /// 에셋에 Attack 액션이 없으면 CaptureCursor(왼쪽 클릭)를 공격으로도 쓴다(패드 X는 없음).
    /// 디버그 주입 중(DebugOverriding)에는 장치 대신 주입 값을 쓴다 — 에디터 포커스 없이 확인할 때 쓴다.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap _map;
        InputAction _move, _look, _lookStick, _zoom, _jump, _walkToggle, _releaseCursor, _captureCursor, _sprint, _dodge, _attack;
        InputAction _skill, _burst; // 2-4: 에셋에 없으면 null (주입만 받는다)
        bool _skillLatched;
        bool _burstLatched;
        InputAction[] _party = new InputAction[0]; // 2-6: Party1 ~ 4, 에셋에 없는 칸은 null
        int _switchLatched; // 1 ~ 4, 0 = 없음
        InputAction _characterMenu; // 3-5: 에셋에 없으면 null (주입만 받는다)
        bool _menuLatched;
        bool _menuOpen;
        float _debugSkillHoldEnd = float.NegativeInfinity; // 주입한 스킬 길게 누르기가 끝나는 고정 스텝 시각 (2-4)

        bool _walkMode;
        bool _jumpLatched;
        bool _dodgeLatched;
        bool _attackLatched;
        Vector2 _lookMouse;
        Vector2 _lookStickValue;
        float _zoomValue;

        bool _override;
        Vector2 _ovMove;
        Vector2 _ovLookPending;
        float _ovZoomPending;
        bool _ovSprint;
        float _debugHoldEnd = float.NegativeInfinity; // 주입한 길게 누르기가 끝나는 고정 스텝 시각 (2-2)

        public Vector2 Move => _menuOpen ? Vector2.zero : _override ? Vector2.ClampMagnitude(_ovMove, 1f) : (_move != null ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero);
        public bool WalkMode => _walkMode;
        public Vector2 LookMouseDelta => _lookMouse;
        public Vector2 LookStick => _lookStickValue;
        public float ZoomScroll => _zoomValue;
        public bool CursorCaptured => Cursor.lockState == CursorLockMode.Locked;
        public bool DebugOverriding => _override;
        /// <summary>스킬 버튼을 누르고 있는지 (2-4). 주입 중에는 DebugHoldSkill만.</summary>
        public bool SkillHeld => !_menuOpen && (Time.fixedTime <= _debugSkillHoldEnd || (!_override && _skill != null && _skill.IsPressed()));
        
public bool SprintHeld => !_menuOpen && (_override ? _ovSprint : (_sprint != null && _sprint.IsPressed()));

        /// <summary>공격 버튼을 누르고 있는지 (2-2). 마우스는 커서가 잠겨 있을 때만, 주입 중에는 DebugHoldAttack만.</summary>
        public bool AttackHeld
        {
            get
            {
                if (_menuOpen) return false;
                if (Time.fixedTime <= _debugHoldEnd) return true;
                if (_override || _map == null) return false;
                if (_attack != null)
                {
                    if (!_attack.IsPressed()) return false;
                    return !(_attack.activeControl != null && _attack.activeControl.device is Mouse) || CursorCaptured;
                }
                return _captureCursor != null && _captureCursor.IsPressed() && CursorCaptured;
            }
        }

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
            _attack = _map.FindAction(PlayerControls.Attack, false);
            _skill = _map.FindAction(PlayerControls.Skill, false); // 2-4: 없으면 조용히 건너뛴다
            _burst = _map.FindAction(PlayerControls.Burst, false);
            _characterMenu = _map.FindAction(PlayerControls.CharacterMenu, false); // 3-5: 없으면 조용히 건너뛴다
            _party = new InputAction[PlayerControls.PartySlots.Length]; // 2-6: 없으면 조용히 건너뛴다
            for (int i = 0; i < _party.Length; i++) _party[i] = _map.FindAction(PlayerControls.PartySlots[i], false);

            if (_attack == null)
                Debug.LogWarning("PlayerInputReader: action '" + PlayerControls.Attack + "' is missing in " + actions.name + " - left click (CaptureCursor) attacks while the cursor is locked, no gamepad attack. Add Attack (see PlayerControls.BuildDefaultJson).");
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

            // 3-5: 메뉴가 열려 있으면 커서를 푼 채 메뉴 키(· ESC)만 받는다 — 클릭은 버튼(uGUI)에만
            if (_menuOpen)
            {
                if ((_characterMenu != null && _characterMenu.WasPressedThisFrame()) || _releaseCursor.WasPressedThisFrame()) _menuLatched = true;
                if (CursorCaptured) SetCursorCaptured(false);
                _lookMouse = Vector2.zero;
                _lookStickValue = Vector2.zero;
                _zoomValue = 0f;
                _ovLookPending = Vector2.zero;
                _ovZoomPending = 0f;
                return;
            }
            if (_characterMenu != null && _characterMenu.WasPressedThisFrame() && !_override) _menuLatched = true;

            // 커서
            // 커서 · 공격 (2-1): 커서가 풀려 있을 때의 클릭은 잠금만, 잠겨 있을 때의 클릭은 공격
            bool click = _captureCursor.WasPressedThisFrame();
            bool attack = _attack != null ? _attack.WasPressedThisFrame() : click;
            bool mouseAttack = _attack == null || (attack && (_attack.activeControl != null ? _attack.activeControl.device is Mouse : click));
            bool wasCaptured = CursorCaptured;
            if (_releaseCursor.WasPressedThisFrame()) SetCursorCaptured(false);
            else if (!wasCaptured && click) SetCursorCaptured(true);
            if (attack && (!mouseAttack || wasCaptured) && !_override) _attackLatched = true;

            // 버튼은 프레임 단위로 받아 둔다 (점프는 FixedUpdate가 꺼낼 때까지 유지)
            if (_jump.WasPressedThisFrame()) _jumpLatched = true;
            if (_dodge != null && _dodge.WasPressedThisFrame() && !_override) _dodgeLatched = true;
            if (_walkToggle.WasPressedThisFrame()) _walkMode = !_walkMode;
            if (_skill != null && _skill.WasPressedThisFrame() && !_override) _skillLatched = true; // 2-4
            if (_burst != null && _burst.WasPressedThisFrame() && !_override) _burstLatched = true;
            if (!_override)
                for (int i = 0; i < _party.Length; i++)
                    if (_party[i] != null && _party[i].WasPressedThisFrame()) _switchLatched = i + 1; // 2-6


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

        public bool ConsumeAttack()
        {
            bool a = _attackLatched;
            _attackLatched = false;
            return a;
        }

        public bool ConsumeDodge()
        {
            bool d = _dodgeLatched;
            _dodgeLatched = false;
            return d;
        }

        public bool ConsumeSkill()
        {
            bool s = _skillLatched;
            _skillLatched = false;
            return s;
        }

        public bool ConsumeBurst()
        {
            bool b = _burstLatched;
            _burstLatched = false;
            return b;
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
        public void DebugPressAttack() { _attackLatched = true; }
        public void DebugHoldAttack(float seconds)
        {
            _attackLatched = true;
            // 누름은 다음 고정 스텝에 꺼내진다 — 그 스텝 뒤로 seconds 동안 누른 채로
            _debugHoldEnd = Time.fixedTime + Time.fixedDeltaTime + seconds + 1e-4f;
        }

        public void DebugPressSkill() { _skillLatched = true; _debugSkillHoldEnd = float.NegativeInfinity; }

        public void DebugHoldSkill(float seconds)
        {
            _skillLatched = true;
            // 누름은 다음 고정 스텝에 꺼내진다 — 그 스텝 뒤로 seconds 동안 누른 채로 (2-4)
            _debugSkillHoldEnd = Time.fixedTime + Time.fixedDeltaTime + seconds + 1e-4f;
        }

        public void DebugPressBurst() { _burstLatched = true; }

        public int ConsumeSwitch()
        {
            int s = _switchLatched;
            _switchLatched = 0;
            return s;
        }

        public void DebugPressSwitch(int slot) { _switchLatched = slot; }

        public bool ConsumeCharacterMenu()
        {
            bool m = _menuLatched;
            _menuLatched = false;
            return m;
        }

        public void DebugPressCharacterMenu() { _menuLatched = true; }

        public bool MenuOpen
        {
            get => _menuOpen;
            set
            {
                if (_menuOpen == value) return;
                _menuOpen = value;
                // 열고 닫을 때 받아 둔 게임 입력은 버린다
                _jumpLatched = _dodgeLatched = _attackLatched = _skillLatched = _burstLatched = false;
                _switchLatched = 0;
                _debugHoldEnd = _debugSkillHoldEnd = float.NegativeInfinity;
                SetCursorCaptured(!value);
            }
        }


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

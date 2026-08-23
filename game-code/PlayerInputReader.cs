using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
	private const float PARRY_BUFFER_TIME = 0.15f;

	private const float JUMP_BUFFER_TIME = 0.1f;

	private const float STICK_UP_THRESHOLD = 0.5f;

	[Header("Input Actions Asset")]
	[SerializeField]
	private InputActionAsset inputActions;

	private InputActionMap playerActionMap;

	private InputAction moveAction;

	private InputAction jumpAction;

	private InputAction stickJumpAction;

	private InputAction attackAction;

	private InputAction blockAction;

	private InputAction dashAction;

	private InputAction pauseAction;

	private float parryBufferTime;

	private float jumpBufferTime;

	public Vector2 MoveInput { get; private set; }

	public bool JumpPressed { get; private set; }

	public bool JumpHeld { get; private set; }

	public bool StickJumpPressed { get; private set; }

	public bool StickJumpHeld { get; private set; }

	public bool AttackPressed { get; private set; }

	public bool BlockPressed { get; private set; }

	public bool BlockHeld { get; private set; }

	public bool DashPressed { get; private set; }

	public bool HasBufferedJump => jumpBufferTime > 0f;

	public bool HasBufferedParry => parryBufferTime > 0f;

	public event Action OnPausePressed;

	private void Awake()
	{
		playerActionMap = inputActions.FindActionMap("Player");
		moveAction = playerActionMap.FindAction("Move");
		jumpAction = playerActionMap.FindAction("Jump");
		stickJumpAction = playerActionMap.FindAction("StickJump");
		attackAction = playerActionMap.FindAction("Attack");
		blockAction = playerActionMap.FindAction("Block");
		dashAction = playerActionMap.FindAction("Dash");
		pauseAction = playerActionMap.FindAction("Pause");
	}

	private void OnEnable()
	{
		playerActionMap.Enable();
		jumpAction.performed += OnJumpPerformed;
		jumpAction.canceled += OnJumpCanceled;
		stickJumpAction.performed += OnStickJumpPerformed;
		stickJumpAction.canceled += OnStickJumpCanceled;
		attackAction.performed += OnAttackPerformed;
		blockAction.performed += OnBlockPerformed;
		blockAction.canceled += OnBlockCanceled;
		dashAction.performed += OnDashPerformed;
		pauseAction.performed += OnPausePerformed;
	}

	private void OnDisable()
	{
		jumpAction.performed -= OnJumpPerformed;
		jumpAction.canceled -= OnJumpCanceled;
		stickJumpAction.performed -= OnStickJumpPerformed;
		stickJumpAction.canceled -= OnStickJumpCanceled;
		attackAction.performed -= OnAttackPerformed;
		blockAction.performed -= OnBlockPerformed;
		blockAction.canceled -= OnBlockCanceled;
		dashAction.performed -= OnDashPerformed;
		pauseAction.performed -= OnPausePerformed;
		playerActionMap.Disable();
	}

	private void Update()
	{
		MoveInput = moveAction.ReadValue<Vector2>();
		bool flag = MoveInput.y >= 0.5f;
		StickJumpHeld = stickJumpAction.IsPressed() | flag;
		JumpHeld = jumpAction.IsPressed();
		BlockHeld = blockAction.IsPressed();
		if (JumpPressed)
		{
			jumpBufferTime = 0.1f;
		}
		else if (jumpBufferTime > 0f)
		{
			jumpBufferTime -= Time.deltaTime;
		}
		if (AttackPressed)
		{
			parryBufferTime = 0.15f;
		}
		else if (parryBufferTime > 0f)
		{
			parryBufferTime -= Time.deltaTime;
		}
	}

	private void LateUpdate()
	{
		JumpPressed = false;
		StickJumpPressed = false;
		AttackPressed = false;
		DashPressed = false;
		BlockPressed = false;
	}

	private void OnJumpPerformed(InputAction.CallbackContext context)
	{
		JumpPressed = true;
		JumpHeld = true;
	}

	private void OnJumpCanceled(InputAction.CallbackContext context)
	{
		JumpHeld = false;
	}

	private void OnStickJumpPerformed(InputAction.CallbackContext context)
	{
		StickJumpPressed = true;
		StickJumpHeld = true;
	}

	private void OnStickJumpCanceled(InputAction.CallbackContext context)
	{
		if (MoveInput.y < 0.5f)
		{
			StickJumpHeld = false;
		}
	}

	private void OnAttackPerformed(InputAction.CallbackContext context)
	{
		AttackPressed = true;
	}

	private void OnBlockPerformed(InputAction.CallbackContext context)
	{
		BlockPressed = true;
	}

	private void OnBlockCanceled(InputAction.CallbackContext context)
	{
		BlockHeld = false;
	}

	private void OnDashPerformed(InputAction.CallbackContext context)
	{
		DashPressed = true;
	}

	private void OnPausePerformed(InputAction.CallbackContext context)
	{
		OnPausePressed?.Invoke();
	}

	public void ConsumeJumpBuffer()
	{
		jumpBufferTime = 0f;
	}

	public void ConsumeParryBuffer()
	{
		parryBufferTime = 0f;
	}

	public bool ConsumeJumpPress()
	{
		bool jumpPressed = JumpPressed;
		JumpPressed = false;
		return jumpPressed;
	}

	public bool ConsumeStickJumpPress()
	{
		bool stickJumpPressed = StickJumpPressed;
		StickJumpPressed = false;
		return stickJumpPressed;
	}

	public bool ConsumeAttackPress()
	{
		bool attackPressed = AttackPressed;
		AttackPressed = false;
		return attackPressed;
	}

	public bool ConsumeBlockPress()
	{
		bool blockPressed = BlockPressed;
		BlockPressed = false;
		return blockPressed;
	}

	public bool ConsumeDashPress()
	{
		bool dashPressed = DashPressed;
		DashPressed = false;
		return dashPressed;
	}

	public void DisableInput()
	{
		playerActionMap.Disable();
	}

	public void EnableInput()
	{
		playerActionMap.Enable();
	}
}

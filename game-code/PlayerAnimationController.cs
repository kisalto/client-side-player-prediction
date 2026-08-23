using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
	[Header("Animation Settings")]
	[SerializeField]
	private float movementSpeedThreshold = 0.1f;

	[SerializeField]
	private bool debugMode;

	private Animator animator;

	private PlayerStateMachine stateMachine;

	private PlayerMovementController movement;

	private PlayerCombatController combat;

	private PlayerDashController dash;

	private static readonly int StateHash = Animator.StringToHash("State");

	private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");

	private static readonly int BlockTypeHash = Animator.StringToHash("BlockType");

	private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

	private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");

	public const int STATE_IDLE = 0;

	public const int STATE_WALK = 1;

	public const int STATE_JUMP = 2;

	public const int STATE_FALL = 3;

	public const int STATE_DASH = 4;

	public const int STATE_ATTACK = 5;

	public const int STATE_BLOCK = 6;

	public const int STATE_PARRY = 7;

	public const int STATE_DEATH = 8;

	public const int STATE_CLIMB = 9;

	public const int STATE_HURT = 10;

	public const int STATE_PARRY_UP = 11;

	public const int STATE_DEFLECTED = 12;

	public const int ATTACK_NORMAL = 0;

	public const int ATTACK_DOWN = 1;

	public const int ATTACK_LADDER = 2;

	public const int BLOCK_SIDE = 0;

	public const int BLOCK_WALK = 1;

	public const int BLOCK_UP = 2;

	public const int BLOCK_WALK_UP = 3;

	private PlayerAnimationState currentAnimState;

	private void Awake()
	{
		animator = GetComponentInChildren<Animator>();
		stateMachine = GetComponent<PlayerStateMachine>();
		movement = GetComponent<PlayerMovementController>();
		combat = GetComponent<PlayerCombatController>();
		dash = GetComponent<PlayerDashController>();
	}

	private void LateUpdate()
	{
		if (!(animator == null) && !(stateMachine == null))
		{
			UpdateAllAnimationParameters();
		}
	}

	private void UpdateAllAnimationParameters()
	{
		if (!combat.IsAttacking)
		{
			UpdateStateParameter();
			UpdateMovementParameters();
			UpdateCombatParameters();
			UpdateStatusParameters();
		}
	}

	private void UpdateStateParameter()
	{
		currentAnimState = DetermineAnimationState();
		animator.SetInteger(StateHash, (int)currentAnimState);
	}

	private PlayerAnimationState DetermineAnimationState()
	{
		return stateMachine.GetCurrentAnimationState();
	}

	private void UpdateMovementParameters()
	{
		if (!(movement == null))
		{
			Mathf.Abs(movement.Velocity.x);
			_ = movementSpeedThreshold;
			_ = movement.IsGrounded;
		}
	}

	private void UpdateCombatParameters()
	{
		if (combat != null)
		{
			_ = combat.IsAttacking;
		}
		else
			_ = 0;
		if (currentAnimState == PlayerAnimationState.Block || currentAnimState == PlayerAnimationState.Parry)
		{
			_ = 1;
		}
		else
			_ = currentAnimState == PlayerAnimationState.ParryUp;
		if (dash != null)
		{
			_ = dash.IsDashing;
		}
		else
			_ = 0;
	}

	private void UpdateStatusParameters()
	{
		_ = currentAnimState;
	}

	public void SetState(int stateValue)
	{
		animator.SetInteger(StateHash, stateValue);
	}

	public void SetAttackType(int attackType)
	{
		animator.SetInteger(AttackTypeHash, attackType);
	}

	public void SetBlockType(int blockType)
	{
		animator.SetInteger(BlockTypeHash, blockType);
	}

	public void SetAttacking(bool isAttacking)
	{
		animator.SetBool(IsAttackingHash, isAttacking);
	}

	public void SetBlocking(bool isBlocking)
	{
		animator.SetBool(IsBlockingHash, isBlocking);
	}

	public bool HasAnimationFinished()
	{
		if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
		{
			return !animator.IsInTransition(0);
		}
		return false;
	}

	public float GetAnimationProgress()
	{
		return animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
	}

	public void DisableAnimator()
	{
		animator.enabled = false;
	}
}

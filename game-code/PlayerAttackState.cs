public class PlayerAttackState : PlayerStateBase
{
	private PlayerAnimationController animController;

	private bool isDownAttack;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Attack;

	public PlayerAttackState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		if (animController == null)
		{
			animController = player.GetComponent<PlayerAnimationController>();
		}
		if (IsOnLadder())
		{
			float y = player.Input.MoveInput.y;
			float x = player.Input.MoveInput.x;
			player.Movement.Climb(y, x);
		}
		int num = DetermineAttackType();
		isDownAttack = num == 1;
		animController.SetAttackType(num);
		animController.SetState(5);
		animController.SetAttacking(isAttacking: false);
		player.Combat.StartAttack(isDownAttack);
		SFXManager.Instance?.Play("P_Attack", player.Movement.transform.position);
	}

	public override void OnUpdate()
	{
		animController.SetAttacking(isAttacking: true);
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		player.Movement.Move(player.Input.MoveInput.x);
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
		if (IsOnLadder())
		{
			player.Movement.StopClimbing();
		}
	}

	private void CheckTransitions()
	{
		if (!player.Combat.IsAttacking && animController.HasAnimationFinished())
		{
			if (IsOnLadder())
			{
				player.ChangeState<PlayerClimbState>();
			}
			else if (!player.Movement.IsGrounded)
			{
				player.ChangeState<PlayerFallState>();
			}
			else if (IsMovementInputActive())
			{
				player.ChangeState<PlayerMoveState>();
			}
			else
			{
				player.ChangeState<PlayerIdleState>();
			}
		}
		else if (player.Movement.IsGrounded && !LadderCollided() && player.Input.JumpPressed)
		{
			player.ChangeState<PlayerJumpState>();
		}
	}

	private int DetermineAttackType()
	{
		if (IsOnLadder())
		{
			return 2;
		}
		if (!player.Movement.IsGrounded && player.Input.MoveInput.y < -0.5f)
		{
			return 1;
		}
		return 0;
	}

	private bool IsOnLadder()
	{
		return player.Movement.IsOnLadder;
	}

	private bool LadderCollided()
	{
		return player.Movement.LadderCollided;
	}
}

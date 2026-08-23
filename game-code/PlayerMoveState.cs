using UnityEngine;

public class PlayerMoveState : PlayerStateBase
{
	public override PlayerAnimationState AnimationState => PlayerAnimationState.Move;

	public PlayerMoveState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
	}

	public override void OnUpdate()
	{
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		player.Movement.Move(player.Input.MoveInput.x);
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
	}

	private void CheckTransitions()
	{
		if (CheckCommonTransitions())
		{
			return;
		}
		if (!player.Movement.IsGrounded)
		{
			player.ChangeState<PlayerFallState>();
			return;
		}
		if (player.Movement.LadderCollided)
		{
			bool num = Mathf.Abs(player.Input.MoveInput.y) > player.Data.movementThreshold;
			bool stickJumpPressed = player.Input.StickJumpPressed;
			bool flag = player.Input.MoveInput.y < 0f - player.Data.movementThreshold;
			if ((num | stickJumpPressed) && !flag)
			{
				player.ChangeState<PlayerClimbState>();
				return;
			}
		}
		if (player.Input.BlockPressed || player.Input.BlockHeld)
		{
			player.ChangeState<PlayerBlockState>();
		}
		else if (player.Input.AttackPressed)
		{
			TransitionToAttackOrDeflect();
		}
		else if (player.Input.JumpPressed || player.Input.HasBufferedJump)
		{
			player.Input.ConsumeJumpBuffer();
			player.ChangeState<PlayerJumpState>();
		}
		else if (!IsMovementInputActive())
		{
			player.ChangeState<PlayerIdleState>();
		}
	}
}

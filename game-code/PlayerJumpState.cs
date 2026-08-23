using UnityEngine;

public class PlayerJumpState : PlayerStateBase
{
	private bool jumpReleased;

	private float externalLaunchTimer;

	private const float MIN_EXTERNAL_LAUNCH_TIME = 0.1f;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Jump;

	public PlayerJumpState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		if (player.Movement.WasLaunchedExternally)
		{
			jumpReleased = true;
			externalLaunchTimer = 0.1f;
			RestartJumpAnimation();
			return;
		}
		externalLaunchTimer = 0f;
		if (player.Input.JumpPressed || player.Input.JumpHeld)
		{
			player.Movement.Jump();
			SFXManager.Instance?.Play("P_Jump", player.Movement.transform.position);
		}
		else if (player.Input.StickJumpPressed || player.Input.StickJumpHeld)
		{
			player.Movement.StickJump();
			SFXManager.Instance?.Play("P_Jump", player.Movement.transform.position);
		}
	}

	public override void OnUpdate()
	{
		if (externalLaunchTimer > 0f)
		{
			externalLaunchTimer -= Time.deltaTime;
		}
		if (!jumpReleased && !player.Input.JumpHeld && !player.Input.StickJumpHeld)
		{
			jumpReleased = true;
			player.Movement.CutJump();
		}
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		player.Movement.Move(player.Input.MoveInput.x);
		if (jumpReleased)
		{
			player.Movement.ApplyLowJumpGravity();
		}
		else
		{
			player.Movement.ApplyGravity();
		}
	}

	public override void OnExit()
	{
		jumpReleased = false;
		externalLaunchTimer = 0f;
		player.Movement.ClearExternalLaunch();
	}

	private void RestartJumpAnimation()
	{
		if (player.Animator != null)
		{
			player.Animator.Play("Jump", 0, 0f);
		}
	}

	private void CheckTransitions()
	{
		if (!CheckCommonTransitions())
		{
			if (!player.Combat.IsAttacking && player.Input.AttackPressed)
			{
				TransitionToAttackOrDeflect();
			}
			else if (player.Input.BlockPressed)
			{
				player.ChangeState<PlayerBlockState>();
			}
			else if (!(externalLaunchTimer > 0f) && player.Movement.Velocity.y < 0f)
			{
				player.ChangeState<PlayerFallState>();
			}
		}
	}
}

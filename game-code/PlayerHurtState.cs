using UnityEngine;

public class PlayerHurtState : PlayerStateBase
{
	private float hurtTimer;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Hurt;

	public PlayerHurtState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		hurtTimer = player.Data.hurtStateDuration;
		player.Movement.StopMovement();
		player.GetComponent<PlayerVisualEffectsController>()?.StartHurtShake();
	}

	public override void OnUpdate()
	{
		hurtTimer -= Time.deltaTime;
		if (hurtTimer <= 0f)
		{
			ExitHurt();
		}
	}

	public override void OnFixedUpdate()
	{
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
		player.NotifyHurtEnd();
	}

	private void ExitHurt()
	{
		if (!player.Health.IsAlive)
		{
			player.ChangeState<PlayerDeadState>();
		}
		else if (player.Movement.IsGrounded)
		{
			player.ChangeState<PlayerIdleState>();
		}
		else
		{
			player.ChangeState<PlayerFallState>();
		}
	}
}

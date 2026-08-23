using UnityEngine;

public abstract class PlayerStateBase : IState
{
	protected readonly PlayerStateMachine player;

	public abstract PlayerAnimationState AnimationState { get; }

	protected PlayerStateBase(PlayerStateMachine player)
	{
		this.player = player;
	}

	public abstract void OnEnter();

	public abstract void OnUpdate();

	public abstract void OnFixedUpdate();

	public abstract void OnExit();

	protected bool CheckCommonTransitions()
	{
		if (!player.Health.IsAlive)
		{
			player.ChangeState<PlayerDeadState>();
			return true;
		}
		if (player.Input.DashPressed && player.Dash.enabled && player.Dash.CanDash)
		{
			player.ChangeState<PlayerDashState>();
			return true;
		}
		return false;
	}

	protected bool IsMovementInputActive()
	{
		return Mathf.Abs(player.Input.MoveInput.x) > player.Data.movementThreshold;
	}

	protected void TransitionToAttackOrDeflect()
	{
		if (player.Combat.WouldAttackBeDeflected())
		{
			player.Combat.InvokeDeflected();
			player.ChangeState<PlayerDeflectedState>();
		}
		else
		{
			player.ChangeState<PlayerAttackState>();
		}
	}
}

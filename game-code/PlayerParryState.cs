using UnityEngine;

public class PlayerParryState : PlayerStateBase
{
	private float parryTimer;

	private bool hasParriedThisWindow;

	private float graceTimer;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Parry;

	public PlayerParryState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		player.Movement.StopMovement();
		player.Combat.StartParry();
		hasParriedThisWindow = false;
		graceTimer = 0f;
		parryTimer = player.Data.parryDuration;
		SFXManager.Instance?.Play("P_ParryAction", player.transform.position);
		CheckForParry();
	}

	public override void OnUpdate()
	{
		if (!hasParriedThisWindow)
		{
			CheckForParry();
		}
		else
		{
			graceTimer -= Time.deltaTime;
			if (graceTimer > 0f)
			{
				CheckForParry();
			}
		}
		parryTimer -= Time.deltaTime;
		if (parryTimer <= 0f)
		{
			if (player.Input.BlockHeld)
			{
				player.ChangeState<PlayerBlockState>();
			}
			else
			{
				player.ChangeState<PlayerIdleState>();
			}
		}
	}

	public override void OnFixedUpdate()
	{
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
		player.Combat.StopParry();
	}

	private void CheckForParry()
	{
		if (player.Combat.PerformParry(!hasParriedThisWindow))
		{
			hasParriedThisWindow = true;
			graceTimer = player.Data.autoParryGraceWindow;
		}
	}
}

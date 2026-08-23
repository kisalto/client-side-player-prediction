using UnityEngine;

public class PlayerDeflectedState : PlayerStateBase
{
	private PlayerAnimationController animController;

	private PlayerVisualEffectsController vfx;

	private bool shakeFinished;

	private float deflectedTimer;

	private const float MAX_DEFLECTED_DURATION = 1f;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Deflected;

	public PlayerDeflectedState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		if (animController == null)
		{
			animController = player.GetComponent<PlayerAnimationController>();
		}
		if (vfx == null)
		{
			vfx = player.GetComponent<PlayerVisualEffectsController>();
		}
		shakeFinished = false;
		deflectedTimer = 0f;
		animController.SetAttacking(isAttacking: false);
		animController.SetState(12);
		player.Combat.StopAttack();
		if (vfx != null)
		{
			vfx.OnDeflectShakeEnd += HandleShakeEnd;
			vfx.OnDeflectedShake();
		}
	}

	public override void OnUpdate()
	{
		deflectedTimer += Time.deltaTime;
		if (shakeFinished || deflectedTimer >= 1f)
		{
			TransitionOut();
		}
	}

	public override void OnFixedUpdate()
	{
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
		if (vfx != null)
		{
			vfx.OnDeflectShakeEnd -= HandleShakeEnd;
		}
	}

	private void HandleShakeEnd()
	{
		shakeFinished = true;
	}

	private void TransitionOut()
	{
		if (!player.Movement.IsGrounded)
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
}

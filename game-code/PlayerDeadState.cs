using UnityEngine;

public class PlayerDeadState : PlayerStateBase
{
	private DeathSequenceManager deathSequenceManager;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Dead;

	public PlayerDeadState(PlayerStateMachine player)
		: base(player)
	{
		deathSequenceManager = Object.FindFirstObjectByType<DeathSequenceManager>();
	}

	public override void OnEnter()
	{
		player.Health.NotifyDeath();
		if ((bool)player.Animator && player.Animator.HasState(0, Animator.StringToHash("Death")))
		{
			player.Animator.SetTrigger("Death");
		}
		player.Movement.StopMovement();
		DisablePlayerControls();
		Debug.Log("Player has died!");
		if (deathSequenceManager != null)
		{
			deathSequenceManager.TriggerDeathSequence();
		}
		else
		{
			Debug.LogError("DeathSequenceManager not found in scene!");
		}
	}

	public override void OnUpdate()
	{
	}

	public override void OnFixedUpdate()
	{
	}

	public override void OnExit()
	{
	}

	private void DisablePlayerControls()
	{
		if (player.Input != null)
		{
			player.Input.enabled = false;
		}
		Rigidbody2D component = player.GetComponent<Rigidbody2D>();
		if (component != null)
		{
			component.linearVelocity = Vector2.zero;
			component.bodyType = RigidbodyType2D.Kinematic;
		}
	}
}

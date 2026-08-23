using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerSFXHandler : MonoBehaviour
{
	private const float FootstepInterval = 0.3f;

	private const float ClimbSoundInterval = 0.35f;

	private PlayerStateMachine stateMachine;

	private PlayerCombatController combat;

	private PlayerHealthController health;

	private PlayerDashController dash;

	private PlayerMovementController movement;

	private float footstepTimer;

	private int footstepIndex;

	private float climbTimer;

	private bool wasGrounded;

	private void Awake()
	{
		stateMachine = GetComponent<PlayerStateMachine>();
		combat = GetComponent<PlayerCombatController>();
		health = GetComponent<PlayerHealthController>();
		dash = GetComponent<PlayerDashController>();
		movement = GetComponent<PlayerMovementController>();
	}

	private void OnEnable()
	{
		if (combat != null)
		{
			combat.OnAttackHit += HandleAttackHit;
			combat.OnAttackDeflected += HandleAttackDeflected;
			combat.OnParrySuccess += HandleParrySuccess;
			combat.OnPogoBounceTrigger += HandlePogoBounce;
		}
		if (health != null)
		{
			health.OnHurt += HandleHurt;
			health.OnDeath += HandleDeath;
		}
		if (dash != null)
		{
			dash.OnDashStart += HandleDashStart;
		}
	}

	private void OnDisable()
	{
		if (combat != null)
		{
			combat.OnAttackHit -= HandleAttackHit;
			combat.OnAttackDeflected -= HandleAttackDeflected;
			combat.OnParrySuccess -= HandleParrySuccess;
			combat.OnPogoBounceTrigger -= HandlePogoBounce;
		}
		if (health != null)
		{
			health.OnHurt -= HandleHurt;
			health.OnDeath -= HandleDeath;
		}
		if (dash != null)
		{
			dash.OnDashStart -= HandleDashStart;
		}
	}

	private void Update()
	{
		if (!(SFXManager.Instance == null))
		{
			PlayerAnimationState currentAnimationState = stateMachine.GetCurrentAnimationState();
			Vector3 position = base.transform.position;
			HandleFootsteps(currentAnimationState, position);
			HandleClimbSound(currentAnimationState, position);
			HandleLanding(position);
			wasGrounded = movement.IsGrounded;
		}
	}

	private void HandleFootsteps(PlayerAnimationState state, Vector3 pos)
	{
		if (state != PlayerAnimationState.Move || !movement.IsGrounded)
		{
			footstepTimer = 0f;
			return;
		}
		footstepTimer += Time.deltaTime;
		if (footstepTimer >= 0.3f)
		{
			footstepTimer = 0f;
			string id = ((footstepIndex % 2 == 0) ? "P_Walk1" : "P_Walk2");
			SFXManager.Instance.Play(id, pos);
			footstepIndex++;
		}
	}

	private void HandleClimbSound(PlayerAnimationState state, Vector3 pos)
	{
		if (state != PlayerAnimationState.Climb)
		{
			if (climbTimer > 0f)
			{
				climbTimer = 0f;
			}
			return;
		}
		if (climbTimer == 0f)
		{
			SFXManager.Instance.Play("P_ClimbGrab", pos);
		}
		float num = Mathf.Abs(stateMachine.Input.MoveInput.y);
		float num2 = Mathf.Abs(stateMachine.Input.MoveInput.x);
		if (num > 0.1f || num2 > 0.1f)
		{
			climbTimer += Time.deltaTime;
			if (climbTimer >= 0.35f)
			{
				climbTimer = 0.01f;
				SFXManager.Instance.Play("P_Climb", pos);
			}
		}
	}

	private void HandleLanding(Vector3 pos)
	{
		if (movement.IsGrounded && !wasGrounded)
		{
			SFXManager.Instance.Play("P_Land", pos);
		}
	}

	private void HandleAttackHit()
	{
		SFXManager.Instance?.Play("P_AttackHit", base.transform.position);
	}

	private void HandleAttackDeflected()
	{
		Debug.Log("[PlayerSFX] HandleAttackDeflected called!");
		SFXManager.Instance?.Play("P_AttackDeflect", base.transform.position);
	}

	private void HandleParrySuccess()
	{
		SFXManager.Instance?.Play("P_Parry", base.transform.position);
	}

	private void HandlePogoBounce()
	{
		SFXManager.Instance?.Play("P_PogoHit", base.transform.position);
	}

	private void HandleHurt()
	{
		SFXManager.Instance?.Play("P_Hurt", base.transform.position);
	}

	private void HandleDeath()
	{
		SFXManager.Instance?.Play("P_Death", base.transform.position);
	}

	private void HandleDashStart()
	{
		SFXManager.Instance?.Play("P_Dash", base.transform.position);
	}
}

using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
	[Header("Debug")]
	[SerializeField]
	private bool debugMode;

	private Animator animator;

	private EnemyController enemy;

	private TrashCanBehavior trashCanBehavior;

	private static readonly int StateHash = Animator.StringToHash("State");

	private static readonly int HurtHash = Animator.StringToHash("Hurt");

	private EnemyAnimationState currentAnimState;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		enemy = GetComponent<EnemyController>();
		trashCanBehavior = GetComponent<TrashCanBehavior>();
	}

	private void LateUpdate()
	{
		if (!(animator == null) && !(enemy == null) && enemy.IsAlive)
		{
			UpdateAnimationState();
		}
	}

	private void UpdateAnimationState()
	{
		currentAnimState = enemy.GetCurrentAnimationState();
		animator.SetInteger(StateHash, (int)currentAnimState);
		if (debugMode)
		{
			Debug.Log($"Enemy Animation State: {currentAnimState}");
		}
	}

	public void TriggerHurt()
	{
		if (animator != null)
		{
			animator.SetTrigger(HurtHash);
		}
	}

	public void TriggerDeath()
	{
		if (animator != null)
		{
			EnemyAnimationState enemyAnimationState = DetermineDeathState();
			animator.SetInteger("State", (int)enemyAnimationState);
			if (debugMode)
			{
				Debug.Log($"[EnemyAnimationController] Triggered death state: {enemyAnimationState}");
			}
		}
	}

	private EnemyAnimationState DetermineDeathState()
	{
		if (trashCanBehavior != null)
		{
			EnemyAnimationState currentAnimationState = enemy.GetCurrentAnimationState();
			if (currentAnimationState == EnemyAnimationState.Hide || currentAnimationState == EnemyAnimationState.HideIdle || currentAnimationState == EnemyAnimationState.HideScared)
			{
				return EnemyAnimationState.DeathHidden;
			}
		}
		return EnemyAnimationState.Death;
	}

	public bool HasAnimationFinished()
	{
		if (animator == null)
		{
			return true;
		}
		if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
		{
			return !animator.IsInTransition(0);
		}
		return false;
	}
}

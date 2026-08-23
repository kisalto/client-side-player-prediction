using System;
using UnityEngine;

public class EnemyCombat : MonoBehaviour, IParryable
{
	[Header("Shoot Point")]
	[SerializeField]
	private Transform shootPoint;

	private EnemyConfig config;

	private Transform cachedTransform;

	private IVFXService vfxService;

	private float attackCooldownTimer;

	private bool isAttacking;

	private bool isInAttackWindow;

	public bool CanAttack
	{
		get
		{
			if (!isAttacking)
			{
				return attackCooldownTimer <= 0f;
			}
			return false;
		}
	}

	public bool IsAttacking => isAttacking;

	public event Action OnAttackStarted;

	public event Action OnAttackCompleted;

	public void Initialize(EnemyConfig enemyConfig)
	{
		config = enemyConfig;
		cachedTransform = base.transform;
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
	}

	public void UpdateCooldowns(float deltaTime)
	{
		if (attackCooldownTimer > 0f)
		{
			attackCooldownTimer -= deltaTime;
		}
	}

	public void OnParried()
	{
		SFXManager.Instance?.Play("E_Stunned", base.transform.position);
		if (isAttacking)
		{
			CompleteAttack();
		}
		isInAttackWindow = false;
	}

	public void InterruptAttack()
	{
		CancelInvoke("CompleteAttack");
		CancelInvoke("ExitAttackWindow");
		isAttacking = false;
		isInAttackWindow = false;
	}

	public void PerformMeleeAttack(Vector3 attackPosition)
	{
		StartMeleeAttack();
	}

	public void PerformRangedAttack(Vector3 direction)
	{
		StartRangedAttack();
	}

	public void StartMeleeAttack()
	{
		if (CanAttack)
		{
			isAttacking = true;
			isInAttackWindow = false;
			OnAttackStarted?.Invoke();
			attackCooldownTimer = config.meleeAttackCooldown;
		}
	}

	public void ExecuteMeleeAttack(Vector3 attackPosition)
	{
		isInAttackWindow = true;
		if (!config.enableAttackEffect)
		{
			attackPosition = cachedTransform.position;
		}
		Collider2D[] array = Physics2D.OverlapCircleAll(attackPosition, config.meleeAttackRange, config.playerLayer);
		bool flag = false;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			if (!(Vector2.Distance(cachedTransform.position, collider2D.transform.position) > config.meleeAttackRange))
			{
				PlayerCombatController component = collider2D.GetComponent<PlayerCombatController>();
				if (component != null && component.IsParrying && component.TryAutoParry(cachedTransform))
				{
					isInAttackWindow = false;
					return;
				}
				IDamageable component2 = collider2D.GetComponent<IDamageable>();
				if (component2 != null && component2.TakeDamage(config.meleeAttackDamage))
				{
					flag = true;
				}
			}
		}
		if (flag && config.enableAttackEffect)
		{
			vfxService?.PlayEffect(config.attackEffectName, attackPosition);
		}
		SFXManager.Instance?.Play("E_KnifeStab", cachedTransform.position);
		Invoke("ExitAttackWindow", 0.1f);
		if (isAttacking)
		{
			Invoke("CompleteAttack", config.meleeAttackDuration);
		}
	}

	private void ExitAttackWindow()
	{
		isInAttackWindow = false;
	}

	public void StartRangedAttack()
	{
		if (CanAttack && (!(config.projectilePrefab == null) || config.enemyType == EnemyType.TrashCan))
		{
			isAttacking = true;
			isInAttackWindow = false;
			OnAttackStarted?.Invoke();
			attackCooldownTimer = config.rangedAttackCooldown;
		}
	}

	public void ExecuteRangedAttack(Vector3 direction)
	{
		if (!isAttacking)
		{
			return;
		}
		isInAttackWindow = true;
		Vector3 position = ((shootPoint != null) ? shootPoint.position : cachedTransform.position);
		GameObject gameObject = ObjectPool.Instance.SpawnFromPool("EnemyProjectile", position, Quaternion.identity);
		SFXManager.Instance?.Play("E_GunShot", cachedTransform.position);
		if (gameObject != null)
		{
			PooledProjectile component = gameObject.GetComponent<PooledProjectile>();
			if (component != null)
			{
				component.SetSource(base.gameObject);
				component.Launch(direction, config.projectileSpeed);
			}
		}
		Invoke("ExitAttackWindow", 0.15f);
		Invoke("CompleteAttack", 0.5f);
	}

	public void CompleteAttack()
	{
		CancelInvoke("CompleteAttack");
		CancelInvoke("ExitAttackWindow");
		isAttacking = false;
		isInAttackWindow = false;
		OnAttackCompleted?.Invoke();
	}

	public bool IsTargetInMeleeRange(Vector3 targetPosition)
	{
		float num = Vector2.Distance(cachedTransform.position, targetPosition);
		if (num <= config.meleeAttackRange)
		{
			return num >= 0.1f;
		}
		return false;
	}

	public bool IsTargetInRangedRange(Vector3 targetPosition)
	{
		return Vector2.Distance(cachedTransform.position, targetPosition) <= config.rangedAttackRange;
	}

	private void OnDrawGizmosSelected()
	{
		if (!(config == null))
		{
			Gizmos.color = Color.red;
			if (config.enemyType == EnemyType.Melee)
			{
				Gizmos.DrawWireSphere(base.transform.position, config.meleeAttackRange);
			}
			else if (config.enemyType == EnemyType.Ranged)
			{
				Gizmos.DrawWireSphere(base.transform.position, config.rangedAttackRange);
			}
			if (shootPoint != null)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere(shootPoint.position, 0.1f);
			}
		}
	}

	public float GetParryKnockback()
	{
		if (config.enemyType == EnemyType.Melee)
		{
			return 2f;
		}
		if (config.enemyType == EnemyType.Ranged)
		{
			return 5f;
		}
		return 2f;
	}
}

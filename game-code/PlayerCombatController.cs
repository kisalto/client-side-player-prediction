using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
	private PlayerData data;

	[SerializeField]
	private Transform attackPoint;

	[SerializeField]
	private Transform downAttackPoint;

	[SerializeField]
	private Transform playerTransform;

	private IVFXService vfxService;

	private PlayerMovementController movement;

	private float attackTimer;

	private bool isDownAttack;

	public bool IsAttacking { get; private set; }

	public bool IsBlocking { get; private set; }

	public bool IsBlockingUp { get; private set; }

	public bool IsParrying { get; private set; }

	public float FacingSign => Mathf.Sign(playerTransform.localScale.x);

	public event Action OnAttackHit;

	public event Action OnParrySuccess;

	public event Action OnPogoBounceTrigger;

	public event Action OnAttackDeflected;

	public void InvokeDeflected()
	{
		OnAttackDeflected?.Invoke();
	}

	private void Awake()
	{
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
		if (playerTransform == null)
		{
			playerTransform = base.transform;
		}
		movement = GetComponent<PlayerMovementController>();
	}

	public void Init(PlayerData playerData)
	{
		data = playerData;
	}

	public void UpdateTimers(float deltaTime)
	{
		if (IsAttacking)
		{
			attackTimer -= deltaTime;
			if (attackTimer <= 0f)
			{
				IsAttacking = false;
				isDownAttack = false;
			}
		}
	}

	public void StartAttack(bool isDownAttackType = false)
	{
		if (!IsAttacking)
		{
			IsAttacking = true;
			isDownAttack = isDownAttackType;
			attackTimer = data.attackDuration;
		}
	}

	public void StopAttack()
	{
		IsAttacking = false;
		isDownAttack = false;
		attackTimer = 0f;
	}

	public void OnAttackImpact()
	{
		if (PerformAttackCheck() && data.enableHitstop)
		{
			HitstopManager.Instance.ApplyHitstop(data.hitstopDuration);
		}
	}

	public void OnDownAttackImpact()
	{
		if (PerformAttackCheck() && data.enableHitstop)
		{
			HitstopManager.Instance.ApplyHitstop(data.hitstopDuration);
		}
	}

	public void StartBlock()
	{
		IsBlocking = true;
		IsBlockingUp = false;
	}

	public void StartBlockUp()
	{
		IsBlocking = true;
		IsBlockingUp = true;
	}

	public void StopBlock()
	{
		IsBlocking = false;
		IsBlockingUp = false;
	}

	public void ApplyBlockKnockback(Vector2 direction)
	{
		if (movement != null)
		{
			movement.SetVelocity(direction.normalized * 1.5f);
		}
		SFXManager.Instance?.Play("P_BlockHit", base.transform.position);
	}

	public bool IsInSideBlockZone()
	{
		float num = Mathf.Sign(playerTransform.localScale.x);
		Vector3 vector = attackPoint.position + (Vector3)(Vector2.right * num * data.parryRange);
		return Physics2D.OverlapBoxAll(layerMask: (LayerMask)((int)data.enemyLayer | (int)data.projectileLayer), point: vector, size: data.parryBoxSize, angle: 0f).Length != 0;
	}

	public void StartParry()
	{
		IsParrying = true;
	}

	public void StopParry()
	{
		IsParrying = false;
	}

	public bool PerformParry(bool isFirstParry = true)
	{
		float num = Mathf.Sign(playerTransform.localScale.x);
		Vector3 vector = attackPoint.position + (Vector3)(Vector2.right * num * data.parryRange);
		Vector3 position = attackPoint.position;
		LayerMask layerMask = (int)data.enemyLayer | (int)data.projectileLayer;
		Collider2D[] collection = Physics2D.OverlapBoxAll(vector, data.parryBoxSize, 0f, layerMask);
		Collider2D[] array = Physics2D.OverlapBoxAll(position, data.parryBoxSize, 0f, layerMask);
		HashSet<Collider2D> hashSet = new HashSet<Collider2D>(collection);
		Collider2D[] array2 = array;
		foreach (Collider2D item in array2)
		{
			hashSet.Add(item);
		}
		bool flag = false;
		float num2 = 0f;
		foreach (Collider2D item2 in hashSet)
		{
			IParryable component = item2.GetComponent<IParryable>();
			if (component != null && component.IsAttacking)
			{
				num2 = ResolveParryHit(component, item2);
				flag = true;
			}
		}
		if (flag)
		{
			OnParryResolved(applyHitstop: true, isFirstParry);
			if (movement != null)
			{
				Vector2 vector2 = -Vector2.right * num;
				movement.SetVelocity(vector2 * num2);
			}
		}
		return flag;
	}

	public bool TryAutoParry(Transform attacker)
	{
		if (!IsParrying || attacker == null)
		{
			return false;
		}
		if (Vector2.Distance(attackPoint.position + (Vector3)(Vector2.right * Mathf.Sign(playerTransform.localScale.x) * data.parryRange), attacker.position) > Mathf.Max(data.parryBoxSize.x, data.parryBoxSize.y))
		{
			return false;
		}
		IParryable component = attacker.GetComponent<IParryable>();
		if (component == null || !component.IsAttacking)
		{
			return false;
		}
		Collider2D component2 = attacker.GetComponent<Collider2D>();
		if (component2 == null)
		{
			return false;
		}
		float num = ResolveParryHit(component, component2);
		OnParryResolved(applyHitstop: false, applySlowMo: true);
		if (movement != null)
		{
			Vector2 vector = -Vector2.right * Mathf.Sign(playerTransform.localScale.x);
			movement.SetVelocity(vector * num);
		}
		return true;
	}

	public bool PerformParryUp(bool isFirstParry = true)
	{
		Vector3 vector = (Vector2)playerTransform.position + data.blockUpOffset;
		Collider2D[] obj = Physics2D.OverlapBoxAll(layerMask: (LayerMask)((int)data.enemyLayer | (int)data.projectileLayer), point: vector, size: data.blockUpBoxSize, angle: 0f);
		bool flag = false;
		float num = 0f;
		Collider2D[] array = obj;
		foreach (Collider2D collider2D in array)
		{
			IParryable component = collider2D.GetComponent<IParryable>();
			if (component != null && component.IsAttacking)
			{
				num = ResolveParryHit(component, collider2D);
				flag = true;
			}
		}
		if (flag)
		{
			OnParryResolved(applyHitstop: true, isFirstParry);
			if (movement != null && movement.Velocity.y <= 0f)
			{
				movement.SetVelocity(new Vector2(movement.Velocity.x, (0f - num) * data.upParryKnockbackMultiplier));
			}
		}
		return flag;
	}

	public bool WouldAttackBeDeflected()
	{
		Collider2D[] array = Physics2D.OverlapBoxAll(attackPoint.position + (Vector3)(Vector2.right * Mathf.Sign(playerTransform.localScale.x) * data.attackRange), data.attackBoxSize, 0f, data.enemyLayer);
		bool result = false;
		Collider2D[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			IDamageable component = array2[i].GetComponent<IDamageable>();
			if (component != null)
			{
				result = true;
				if (component.CanReceiveDamage())
				{
					return false;
				}
			}
		}
		return result;
	}

	private bool PerformAttackCheck()
	{
		Vector3 vector = ((isDownAttack && downAttackPoint != null) ? downAttackPoint : attackPoint).position + (Vector3)(Vector2.right * Mathf.Sign(playerTransform.localScale.x) * data.attackRange);
		Collider2D[] array = Physics2D.OverlapBoxAll(vector, data.attackBoxSize, 0f, data.enemyLayer);
		bool flag = false;
		bool flag2 = false;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			IDamageable component = collider2D.GetComponent<IDamageable>();
			if (component != null)
			{
				Vector2 knockbackDirection = (collider2D.transform.position - playerTransform.position).normalized;
				if (component.TakeDamage(data.attackDamage, knockbackDirection, data.knockbackForce))
				{
					OnAttackHit?.Invoke();
					flag = true;
				}
				else
				{
					flag2 = true;
				}
			}
		}
		if (flag)
		{
			vfxService.PlayEffect("Slash", vector);
			if (isDownAttack)
			{
				ApplyPogoBounce();
			}
		}
		if (!flag & flag2)
		{
			OnAttackDeflected?.Invoke();
		}
		return flag;
	}

	private float ResolveParryHit(IParryable parryable, Collider2D hitCollider)
	{
		parryable.OnParried();
		IDamageable component = hitCollider.GetComponent<IDamageable>();
		if (component != null)
		{
			Vector2 knockbackDirection = (hitCollider.transform.position - playerTransform.position).normalized;
			component.TakeDamage(data.parryDamage, knockbackDirection, data.knockbackForce);
		}
		return parryable.GetParryKnockback();
	}

	private void OnParryResolved(bool applyHitstop, bool applySlowMo)
	{
		OnParrySuccess?.Invoke();
		vfxService?.PlayEffect("Parry", playerTransform.position);
		if (applyHitstop)
		{
			HitstopManager.Instance.ApplyHitstop(data.parryHitstopDuration);
		}
		if (applySlowMo && data.enableParrySlowMotion)
		{
			TimeManager.Instance.SlowTime(data.parrySlowMotionScale, data.parrySlowMotionDuration);
		}
	}

	private void ApplyPogoBounce()
	{
		if (movement != null)
		{
			movement.SetVelocity(new Vector2(movement.Velocity.x, data.pogoBounceForce));
			OnPogoBounceTrigger?.Invoke();
		}
	}

	private void OnDrawGizmosSelected()
	{
		if ((bool)attackPoint && (bool)data)
		{
			float num = ((playerTransform != null) ? Mathf.Sign(playerTransform.localScale.x) : 1f);
			Vector3 center = attackPoint.position + (Vector3)(Vector2.right * num * data.attackRange);
			Gizmos.color = Color.red;
			Gizmos.DrawWireCube(center, data.attackBoxSize);
			if (downAttackPoint != null)
			{
				Vector3 center2 = downAttackPoint.position + (Vector3)(Vector2.right * num * data.attackRange);
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireCube(center2, data.attackBoxSize);
			}
			Vector3 center3 = attackPoint.position + (Vector3)(Vector2.right * num * data.parryRange);
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(center3, data.parryBoxSize);
			Vector3 center4 = (Vector2)playerTransform.position + data.blockUpOffset;
			Gizmos.color = Color.blue;
			Gizmos.DrawWireCube(center4, data.blockUpBoxSize);
		}
	}
}

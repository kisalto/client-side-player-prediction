using System;
using UnityEngine;

public class PlayerHealthController : MonoBehaviour, IDamageable
{
	private PlayerData data;

	[Header("VFX")]
	[SerializeField]
	private string hurtEffectName = "Hurt";

	private float invincibilityTimer;

	private IVFXService vfxService;

	private PlayerCombatController combat;

	public int CurrentHealth { get; private set; }

	public int MaxHealth => data.maxHealth;

	public bool IsInvincible { get; private set; }

	public bool IsAlive => CurrentHealth > 0;

	public int GetCurrentHealth => CurrentHealth;

	public event Action OnHealthChanged;

	public event Action OnDeath;

	public event Action OnHurt;

	public event Action OnInvincibilityStart;

	public event Action OnInvincibilityEnd;

	private void Awake()
	{
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
		combat = GetComponent<PlayerCombatController>();
	}

	public void Init(PlayerData playerData)
	{
		data = playerData;
		CurrentHealth = data.maxHealth;
	}

	public void UpdateTimers(float deltaTime)
	{
		if (IsInvincible)
		{
			invincibilityTimer -= deltaTime;
			if (invincibilityTimer <= 0f)
			{
				EndInvincibility();
			}
		}
	}

	public bool TakeDamage(int damage)
	{
		return TakeDamage(damage, bypassBlock: false);
	}

	public bool TakeDamage(int damage, bool bypassBlock)
	{
		if (IsInvincible || !IsAlive)
		{
			return false;
		}
		if (!bypassBlock && combat != null && combat.IsBlocking)
		{
			combat.ApplyBlockKnockback(Vector2.left * Mathf.Sign(base.transform.localScale.x));
			return false;
		}
		CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
		OnHealthChanged?.Invoke();
		SpawnHurtEffect();
		OnHurt?.Invoke();
		if (IsAlive)
		{
			StartInvincibility();
		}
		return true;
	}

	public bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce)
	{
		return TakeDamage(damageAmount, knockbackDirection, knockbackForce, bypassBlock: false);
	}

	public bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce, bool bypassBlock)
	{
		if (IsInvincible || !IsAlive)
		{
			return false;
		}
		if (!bypassBlock && combat != null)
		{
			bool flag = knockbackDirection.y < -0.5f;
			if (flag && combat.IsBlockingUp)
			{
				combat.ApplyBlockKnockback(-knockbackDirection);
				return false;
			}
			if (!flag && combat.IsBlocking && !combat.IsBlockingUp)
			{
				float facingSign = combat.FacingSign;
				if ((facingSign > 0f && knockbackDirection.x < 0f) || (facingSign < 0f && knockbackDirection.x > 0f))
				{
					combat.ApplyBlockKnockback(knockbackDirection);
					return false;
				}
			}
			if (combat.IsParrying)
			{
				Transform transform = FindAttackerFromDirection(-knockbackDirection);
				if (transform != null && combat.TryAutoParry(transform))
				{
					return false;
				}
			}
		}
		return TakeDamage(damageAmount, bypassBlock: true);
	}

	public bool CanReceiveDamage()
	{
		if (IsAlive)
		{
			return !IsInvincible;
		}
		return false;
	}

	public void NotifyDeath()
	{
		OnDeath?.Invoke();
	}

	private void SpawnHurtEffect()
	{
		vfxService?.PlayEffect(hurtEffectName, base.transform.position);
	}

	private Transform FindAttackerFromDirection(Vector2 direction)
	{
		RaycastHit2D raycastHit2D = Physics2D.Raycast(base.transform.position, direction, 5f, LayerMask.GetMask("Enemy"));
		if (!(raycastHit2D.collider != null))
		{
			return null;
		}
		return raycastHit2D.transform;
	}

	public void Heal(int amount)
	{
		if (IsAlive)
		{
			CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
			OnHealthChanged?.Invoke();
		}
	}

	private void StartInvincibility()
	{
		IsInvincible = true;
		invincibilityTimer = data.invincibilityDuration;
		OnInvincibilityStart?.Invoke();
	}

	private void EndInvincibility()
	{
		IsInvincible = false;
		OnInvincibilityEnd?.Invoke();
	}
}

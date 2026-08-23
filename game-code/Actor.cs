using System.Collections;
using UnityEngine;

public class Actor : MonoBehaviour, IDamageable
{
	[Header("Health Settings")]
	[SerializeField]
	protected int maxHealthPoints = 1;

	protected int currentHealthPoints;

	[Header("Combat")]
	[SerializeField]
	protected int attackDamage = 1;

	[Header("VFX")]
	[SerializeField]
	protected bool enableHurtEffect = true;

	[SerializeField]
	protected string hurtEffectName = "HurtParticles";

	[SerializeField]
	protected bool enableDeathEffect = true;

	[SerializeField]
	protected string deathEffectName = "DeathParticles";

	[Header("Hit Flash")]
	[SerializeField]
	protected bool enableHitFlash = true;

	[SerializeField]
	protected Color flashColor = Color.red;

	[SerializeField]
	protected float flashDuration = 0.1f;

	[Header("Hitstop")]
	[SerializeField]
	protected bool enableHitstop = true;

	[SerializeField]
	protected float hitstopDuration = 0.05f;

	[Header("Knockback")]
	[SerializeField]
	protected bool enableKnockback = true;

	[SerializeField]
	protected float knockbackForce = 5f;

	[SerializeField]
	protected float knockbackDuration = 0.2f;

	protected Coroutine knockbackCoroutine;

	protected Coroutine currentFlashCoroutine;

	protected Animator animator;

	protected SpriteRenderer spriteRenderer;

	protected Rigidbody2D rb;

	protected IVFXService vfxService;

	private Color baseColor;

	public bool isBeingKnockedBack { get; protected set; }

	public bool IsAlive => currentHealthPoints > 0;

	public int GetCurrentHealth => currentHealthPoints;

	public int MaxHealth => maxHealthPoints;

	protected virtual void Awake()
	{
		animator = GetComponent<Animator>();
		spriteRenderer = GetComponent<SpriteRenderer>();
		rb = GetComponent<Rigidbody2D>();
		currentHealthPoints = maxHealthPoints;
		if (spriteRenderer != null)
		{
			baseColor = spriteRenderer.color;
		}
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
	}

	public virtual bool TakeDamage(int damageAmount)
	{
		if (!IsAlive)
		{
			return false;
		}
		currentHealthPoints -= damageAmount;
		currentHealthPoints = Mathf.Max(0, currentHealthPoints);
		if (currentHealthPoints <= 0)
		{
			Die();
		}
		else
		{
			OnHit();
		}
		return true;
	}

	public virtual bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce)
	{
		if (!IsAlive)
		{
			return false;
		}
		currentHealthPoints -= damageAmount;
		currentHealthPoints = Mathf.Max(0, currentHealthPoints);
		if (currentHealthPoints <= 0)
		{
			Die();
		}
		else
		{
			OnHit(knockbackDirection, knockbackForce);
		}
		return true;
	}

	public virtual bool CanReceiveDamage()
	{
		return IsAlive;
	}

	protected virtual void OnHit()
	{
		OnHit(Vector2.zero, 0f);
	}

	protected virtual void OnHit(Vector2 knockbackDirection, float knockbackForce)
	{
		SFXManager.Instance?.Play("E_Hurt", base.transform.position);
		if (enableHurtEffect)
		{
			vfxService?.PlayEffect(hurtEffectName, base.transform.position);
		}
		if (enableHitFlash)
		{
			if (currentFlashCoroutine != null)
			{
				StopCoroutine(currentFlashCoroutine);
			}
			currentFlashCoroutine = StartCoroutine(FlashRoutine());
		}
		if (enableKnockback && knockbackDirection != Vector2.zero && rb != null)
		{
			ApplyKnockback(knockbackDirection, knockbackForce);
		}
	}

	protected virtual void ApplyKnockback(Vector2 direction, float force)
	{
		if (!(rb == null))
		{
			if (knockbackCoroutine != null)
			{
				StopCoroutine(knockbackCoroutine);
			}
			isBeingKnockedBack = true;
			rb.constraints = RigidbodyConstraints2D.FreezeRotation;
			rb.linearVelocity = Vector2.zero;
			rb.AddForce(direction.normalized * force, ForceMode2D.Impulse);
			knockbackCoroutine = StartCoroutine(KnockbackRecovery());
		}
	}

	protected IEnumerator KnockbackRecovery()
	{
		yield return new WaitForSeconds(knockbackDuration);
		if (rb != null)
		{
			rb.constraints = RigidbodyConstraints2D.FreezeRotation;
		}
		isBeingKnockedBack = false;
		knockbackCoroutine = null;
		OnKnockbackEnd();
	}

	protected virtual void OnKnockbackEnd()
	{
	}

	protected virtual void Die()
	{
		SFXManager.Instance?.Play("E_Death", base.transform.position);
		if (enableDeathEffect)
		{
			vfxService?.PlayEffect(deathEffectName, base.transform.position);
		}
	}

	public virtual void Heal(int amount)
	{
		currentHealthPoints = Mathf.Min(currentHealthPoints + amount, maxHealthPoints);
	}

	protected virtual void Flip(bool isFacingRight)
	{
		Vector3 localScale = base.transform.localScale;
		localScale.x = Mathf.Abs(localScale.x) * (float)(isFacingRight ? 1 : (-1));
		base.transform.localScale = localScale;
	}

	protected IEnumerator FlashRoutine()
	{
		if (!(spriteRenderer == null))
		{
			spriteRenderer.color = flashColor;
			float elapsed = 0f;
			while (elapsed < flashDuration)
			{
				elapsed += Time.deltaTime;
				spriteRenderer.color = Color.Lerp(flashColor, baseColor, elapsed / flashDuration);
				yield return null;
			}
			spriteRenderer.color = baseColor;
			currentFlashCoroutine = null;
		}
	}
}

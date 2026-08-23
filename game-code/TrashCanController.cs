using UnityEngine;

public class TrashCanController : EnemyController
{
	[Header("Trash Can Invincibility")]
	[SerializeField]
	private bool isInvincibleToPlayerAttacks = true;

	[SerializeField]
	private bool showInvincibilityDebug = true;

	private PlayerCombatController playerCombat;

	protected override void Awake()
	{
		base.Awake();
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (gameObject != null)
		{
			playerCombat = gameObject.GetComponent<PlayerCombatController>();
		}
	}

	public override bool TakeDamage(int damageAmount)
	{
		if (isInvincibleToPlayerAttacks && IsPlayerCurrentlyAttacking())
		{
			if (showInvincibilityDebug)
			{
				Debug.Log("[TrashCan] Blocked normal player attack! Use parried rockets instead.");
			}
			return false;
		}
		return base.TakeDamage(damageAmount);
	}

	public override bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce)
	{
		if (isInvincibleToPlayerAttacks && IsPlayerCurrentlyAttacking())
		{
			if (showInvincibilityDebug)
			{
				Debug.Log("[TrashCan] Blocked normal player attack! Use parried rockets instead.");
			}
			return false;
		}
		return base.TakeDamage(damageAmount, knockbackDirection, knockbackForce);
	}

	public override bool CanReceiveDamage()
	{
		if (isInvincibleToPlayerAttacks)
		{
			return false;
		}
		return base.CanReceiveDamage();
	}

	private bool IsPlayerCurrentlyAttacking()
	{
		if (playerCombat == null)
		{
			return false;
		}
		return playerCombat.IsAttacking;
	}
}

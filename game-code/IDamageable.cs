using UnityEngine;

public interface IDamageable
{
	int GetCurrentHealth { get; }

	bool IsAlive { get; }

	bool TakeDamage(int damageAmount);

	bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce);

	bool CanReceiveDamage();
}

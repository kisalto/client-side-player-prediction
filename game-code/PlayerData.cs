using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Player/Player Data")]
public class PlayerData : ScriptableObject
{
	[Header("Movement")]
	public float moveSpeed = 5f;

	public float groundAcceleration = 30f;

	public float groundDeceleration = 40f;

	public float airAcceleration = 20f;

	public float airDeceleration = 15f;

	public float jumpForce = 15f;

	public float lowJumpMultiplier = 2f;

	public float gravity = 2.5f;

	public float fallMultiplier = 3f;

	public float movementThreshold = 0.01f;

	public float blockMovementMultiplier = 0.5f;

	public float coyoteTime = 0.15f;

	public float climbSpeed = 3f;

	public float maxFallSpeed = -20f;

	[Header("Dash")]
	public float dashSpeed = 20f;

	public float dashDuration = 0.2f;

	public float dashCooldown = 1f;

	[Header("Health")]
	public int maxHealth = 3;

	public float hurtStateDuration = 0.4f;

	[Header("Hurt Shake")]
	public float hurtShakeStrength = 0.08f;

	public float hurtShakeDuration = 0.3f;

	[Header("Deflect Shake")]
	[Tooltip("Strength of the sprite shake when hitting a non-attackable target.")]
	public float deflectShakeStrength = 0.06f;

	[Tooltip("Duration of the sprite shake when hitting a non-attackable target.")]
	public float deflectShakeDuration = 0.15f;

	[Header("Combat")]
	public int attackDamage = 1;

	public int parryDamage = 3;

	public float attackDuration = 0.3f;

	public float parryDuration = 0.2f;

	public float parryHitstopDuration = 0.1f;

	public float invincibilityDuration = 1.5f;

	public float knockbackForce = 5f;

	public float pogoBounceForce = 12f;

	[Header("Ground Detection")]
	public float groundCheckDistance = 0.1f;

	public LayerMask groundLayer;

	[Header("Combat Detection")]
	public Vector2 attackBoxSize = new Vector2(1.5f, 1f);

	public float attackRange = 0.5f;

	public Vector2 parryBoxSize = new Vector2(1.5f, 1f);

	public float parryRange = 0.5f;

	[Header("Block Up Detection")]
	public Vector2 blockUpBoxSize = new Vector2(1f, 0.5f);

	public Vector2 blockUpOffset = new Vector2(0f, 1f);

	public LayerMask enemyLayer;

	public LayerMask projectileLayer;

	[Header("Hit Effects")]
	public bool enableHitstop = true;

	public float hitstopDuration = 0.05f;

	[Header("Parry Knockback")]
	public float meleeParryKnockback = 2f;

	public float rangedParryKnockback = 5f;

	[Tooltip("Multiplier applied to knockback when the player parries upward. Keep very small (e.g. 0.1).")]
	public float upParryKnockbackMultiplier = 0.1f;

	[Header("Visual Effects")]
	public float blinkInterval = 0.1f;

	public float cameraShakeAmplitude = 1.5f;

	public float cameraShakeFrequency = 2f;

	public float cameraShakeDuration = 0.3f;

	[Header("Parry Time Effects")]
	public bool enableParrySlowMotion = true;

	public float parrySlowMotionScale = 0.2f;

	public float parrySlowMotionDuration = 0.15f;

	[Header("Parry Grace Window")]
	public float autoParryGraceWindow = 0.15f;
}

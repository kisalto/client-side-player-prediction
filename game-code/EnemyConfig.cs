using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Config", menuName = "Enemy/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
	[Header("Enemy Type")]
	public string enemyName = "Basic Enemy";

	public EnemyType enemyType;

	[Header("Health")]
	public int maxHealth = 3;

	[Header("Movement")]
	public float moveSpeed = 2f;

	public float chaseSpeedMultiplier = 1.5f;

	public float patrolDistance = 3f;

	public float patrolTurnBuffer = 0.2f;

	[Header("Edge Detection")]
	public bool detectEdges = true;

	public float edgeDetectionDistance = 0.5f;

	public float edgeCheckOffset = 0.5f;

	public LayerMask groundLayer;

	[Header("Detection")]
	public float detectionRange = 5f;

	public float losePlayerRange = 8f;

	public LayerMask playerLayer;

	public bool useDirectionalDetection = true;

	public float detectionAngle = 120f;

	[Header("Combat - Melee")]
	public float meleeAttackRange = 1.5f;

	public int meleeAttackDamage = 1;

	public float meleeAttackCooldown = 1.5f;

	public float meleeAttackDuration = 0.5f;

	[Header("Combat - Ranged")]
	public float rangedAttackRange = 6f;

	public int rangedAttackDamage = 1;

	public float rangedAttackCooldown = 2f;

	public GameObject projectilePrefab;

	public float projectileSpeed = 8f;

	[Header("VFX")]
	public bool enableHurtEffect = true;

	public string hurtEffectName = "FX_Hurt";

	public bool enableDeathEffect = true;

	public string deathEffectName = "FX_Gore";

	public bool enableAttackEffect = true;

	public string attackEffectName = "FX_Slash";

	[Header("Behavior")]
	public bool canPatrol = true;

	public bool canChase = true;

	public bool returnsToPatrol = true;

	public float returnToPatrolDelay = 3f;

	public bool startFacingRight = true;

	[Header("Return Behavior")]
	public float returnTimeoutDuration = 10f;

	public float stuckCheckInterval = 1f;

	public float minimumProgressDistance = 0.3f;

	[Header("Trash Can Specific")]
	public bool isTrashCanEnemy;

	public float trashCanApproachRange = 3f;

	public float trashCanAttackCooldown = 3f;

	public LayerMask hiddenLayer;

	public LayerMask normalEnemyLayer;
}

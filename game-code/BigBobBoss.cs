using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class BigBobBoss : Actor
{
	public enum BossState
	{
		Idle,
		Shooting,
		Reloading,
		Stunned,
		Screaming,
		Teleporting,
		Dead
	}

	[Header("UI")]
	[SerializeField]
	private BossUIController bossUI;

	[Header("Barrage")]
	[SerializeField]
	private BossBarrageController bossBarrage;

	[Header("Death")]
	[SerializeField]
	private BossDeathSequence bossDeathSequence;

	[Header("Boss Settings")]
	[SerializeField]
	private int maxHealth = 200;

	[SerializeField]
	private int damagePerHit = 5;

	[SerializeField]
	private int phase2HealthThreshold = 100;

	[Header("Attack Settings")]
	[SerializeField]
	private float attackCooldown = 1f;

	[SerializeField]
	private float shootDelay = 0.5f;

	[SerializeField]
	private int maxBulletsPerMagazine = 10;

	[SerializeField]
	private float reloadDuration = 3f;

	[Header("Bullet Settings")]
	[SerializeField]
	private string bulletPoolTag = "BossBullet";

	[SerializeField]
	private Transform bulletSpawnPoint;

	[Header("Stun Settings")]
	[SerializeField]
	private float stunDuration = 5f;

	[Header("Scream Settings")]
	[SerializeField]
	private float screamDuration = 1.5f;

	[SerializeField]
	private float reloadScreamDuration = 1f;

	[SerializeField]
	private float screamShakeAmplitude = 0.8f;

	[SerializeField]
	private float screamShakeFrequency = 3f;

	[SerializeField]
	private float playerKnockbackHorizontalForce = 12f;

	[SerializeField]
	private float playerKnockbackVerticalForce = 10f;

	[SerializeField]
	private float phase2PlayerKnockbackHorizontalForce = 20f;

	[SerializeField]
	private float phase2PlayerKnockbackVerticalForce = 16f;

	[SerializeField]
	private Color phase1ScreamTint = new Color(0.4f, 1f, 1f, 1f);

	[Tooltip("Tint applied to the scream particle in Phase 2 (orange).")]
	[SerializeField]
	private Color phase2ScreamTint = new Color(1f, 0.6f, 0f, 1f);

	[SerializeField]
	private ParticleSystem screamEffect;

	[Tooltip("Distance at which knockback reaches zero. At distance 0 the full knockback force is applied.")]
	[SerializeField]
	private float screamKnockbackFalloffDistance = 15f;

	[Header("Appear Knockback")]
	[Tooltip("Duration of the camera + sprite shake when the boss reappears from a portal.")]
	[SerializeField]
	private float appearKnockbackDuration = 0.4f;

	[Tooltip("Horizontal force applied to push the player away when the boss reappears.")]
	[SerializeField]
	private float appearKnockbackHorizontalForce = 14f;

	[Tooltip("Vertical force applied to push the player away when the boss reappears.")]
	[SerializeField]
	private float appearKnockbackVerticalForce = 8f;

	[Header("Sprite Shake")]
	[SerializeField]
	private float spriteShakeIntensity = 0.15f;

	[SerializeField]
	private float spriteShakeSpeed = 50f;

	[Header("Phase 2 Settings")]
	[SerializeField]
	private Transform teleportPositionLeft;

	[SerializeField]
	private Transform teleportPositionRight;

	[Tooltip("Reference to the already-placed alcohol item in the scene. Will be enabled during phase 2.")]
	[SerializeField]
	private GameObject alcoholItemInScene;

	[SerializeField]
	private GameObject phase2DialoguePrefab;

	[Header("Barrage Camera")]
	[SerializeField]
	private CinemachineCamera barrageCamera;

	[Header("Parry Bullet Clear")]
	[SerializeField]
	private bool clearBulletsOnParriedHit = true;

	[Header("Layers")]
	[SerializeField]
	private LayerMask playerLayer;

	private BossState currentState;

	private int currentPhase = 1;

	private bool isAttackable = true;

	private bool bossStarted;

	private bool hasFightEverStarted;

	private bool onRight = true;

	private bool hasSpawnedAlcohol;

	private bool hasShownPhase2Dialogue;

	private bool isInDeathSequence;

	private int stunnedSoundHandle = -1;

	private int shootSoundHandle = -1;

	private int reloadSoundHandle = -1;

	private bool isFacingRight;

	private int bulletsFired;

	private float attackTimer;

	private float stateTimer;

	private bool canShoot;

	private Transform playerTransform;

	private PlayerMovementController playerMovement;

	private BoxCollider2D bossCollider;

	private bool _teleportInAnimationComplete;

	private bool _teleportOutAnimationComplete;

	private bool _phase1TeleportInAnimationComplete;

	private bool _phase1TeleportOutAnimationComplete;

	private bool _phaseTransitionAnimationComplete;

	private const int ANIM_STATE_IDLE = 1;

	private const int ANIM_STATE_SHOOT = 2;

	private const int ANIM_STATE_RELOAD = 3;

	private const int ANIM_STATE_STUNNED = 4;

	private const int ANIM_STATE_ARMORED = 5;

	private const int ANIM_STATE_ARMORED_SHOOT = 6;

	private const int ANIM_STATE_ARMORED_RELOAD = 7;

	private const int ANIM_STATE_ARMORED_STUNNED = 8;

	private const int ANIM_STATE_TELEPORT_IN = 9;

	private const int ANIM_STATE_TELEPORT_OUT = 10;

	private const int ANIM_STATE_SCREAM = 11;

	private const int ANIM_STATE_ARMORED_SCREAM = 12;

	private const int ANIM_STATE_PHASE1_TELEPORT_IN = 13;

	private const int ANIM_STATE_PHASE1_TELEPORT_OUT = 14;

	private const int ANIM_STATE_PHASE_TRANSITION = 15;

	private const int ANIM_STATE_DEAD = 16;

	private const float TELEPORT_ANIM_FALLBACK_DURATION = 2f;

	private const float TELEPORT_OUT_DURATION = 2f;

	public bool IsBossActive
	{
		get
		{
			if (!bossStarted || !base.IsAlive)
			{
				return isInDeathSequence;
			}
			return true;
		}
	}

	public bool IsBossSequenceActive
	{
		get
		{
			if (!hasFightEverStarted || !base.IsAlive)
			{
				return isInDeathSequence;
			}
			return true;
		}
	}

	public static event Action OnBossDied;

	protected override void Awake()
	{
		base.Awake();
		maxHealthPoints = maxHealth;
		currentHealthPoints = maxHealth;
		bossCollider = GetComponent<BoxCollider2D>();
		isFacingRight = base.transform.localScale.x > 0f;
		isAttackable = true;
	}

	private void Start()
	{
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (gameObject != null)
		{
			playerTransform = gameObject.transform;
			playerMovement = gameObject.GetComponent<PlayerMovementController>();
		}
		bossBarrage?.Initialize(playerTransform, playerMovement);
	}

	private void Update()
	{
		if (bossStarted && base.IsAlive)
		{
			UpdateFacing();
			UpdateState();
		}
	}

	private void UpdateFacing()
	{
		if (!(playerTransform == null) && currentState != BossState.Stunned && currentState != BossState.Screaming && currentState != BossState.Teleporting)
		{
			FaceArenaCenter();
		}
	}

	private void FaceArenaCenter()
	{
		if (!(teleportPositionLeft == null) && !(teleportPositionRight == null))
		{
			float num = (teleportPositionLeft.position.x + teleportPositionRight.position.x) * 0.5f;
			bool flag = base.transform.position.x < num;
			if (flag != isFacingRight)
			{
				isFacingRight = flag;
				Vector3 localScale = base.transform.localScale;
				localScale.x = Mathf.Abs(localScale.x) * (float)((!isFacingRight) ? 1 : (-1));
				base.transform.localScale = localScale;
			}
		}
	}

	private void UpdateState()
	{
		stateTimer += Time.deltaTime;
		switch (currentState)
		{
		case BossState.Idle:
			UpdateIdleState();
			break;
		case BossState.Shooting:
			UpdateShootingState();
			break;
		case BossState.Reloading:
			UpdateReloadingState();
			break;
		case BossState.Stunned:
			UpdateStunnedState();
			break;
		case BossState.Screaming:
		case BossState.Teleporting:
			break;
		}
	}

	private void UpdateIdleState()
	{
		ChangeState(BossState.Shooting);
	}

	private void UpdateShootingState()
	{
		if (attackTimer > 0f)
		{
			attackTimer -= Time.deltaTime;
		}
		else if (bulletsFired >= maxBulletsPerMagazine)
		{
			ChangeState(BossState.Reloading);
		}
		else if (!canShoot)
		{
			canShoot = true;
			bulletsFired++;
			attackTimer = attackCooldown;
			UpdateAnimation();
		}
	}

	private void UpdateReloadingState()
	{
		if (stateTimer >= reloadDuration)
		{
			bulletsFired = 0;
			currentState = BossState.Screaming;
			stateTimer = 0f;
			StopLoopedSound(ref shootSoundHandle);
			StopLoopedSound(ref reloadSoundHandle);
			StopLoopedSound(ref stunnedSoundHandle);
			int value = ((currentPhase == 1) ? 1 : 5);
			animator?.SetInteger("State", value);
			StartCoroutine(ScreamSequence(reloadScreamDuration));
		}
	}

	private void UpdateStunnedState()
	{
		if (stateTimer >= stunDuration)
		{
			ChangeState(BossState.Screaming);
			StartCoroutine(ScreamSequence());
		}
	}

	private IEnumerator ScreamSequence(float duration = -1f, bool teleportAfter = true)
	{
		float num = ((duration > 0f) ? duration : screamDuration);
		CameraShakeManager.Instance?.ShakeCamera(screamShakeAmplitude, screamShakeFrequency, num);
		StartCoroutine(ShakeSpriteRoutine(num));
		SFXManager.Instance?.Play("B_Scream", base.transform.position);
		PlayScreamEffect();
		if (playerTransform != null && playerMovement != null)
		{
			float num2 = ((playerTransform.position.x > base.transform.position.x) ? 1f : (-1f));
			float num3 = Vector2.Distance(base.transform.position, playerTransform.position);
			float num4 = Mathf.Clamp01(1f - num3 / screamKnockbackFalloffDistance);
			float num5 = ((currentPhase == 2) ? phase2PlayerKnockbackHorizontalForce : playerKnockbackHorizontalForce);
			float num6 = ((currentPhase == 2) ? phase2PlayerKnockbackVerticalForce : playerKnockbackVerticalForce);
			playerMovement.SetVelocity(new Vector2(num2 * num5 * num4, num6 * num4));
		}
		yield return new WaitForSeconds(num);
		if (screamEffect != null)
		{
			screamEffect.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
		}
		if (teleportAfter)
		{
			isAttackable = false;
			ChangeState(BossState.Teleporting);
			StartCoroutine(TeleportSequence());
		}
		else
		{
			isAttackable = currentPhase == 1;
			ChangeState(BossState.Idle);
		}
	}

	private void PlayScreamEffect()
	{
		if (!(screamEffect == null))
		{
			ParticleSystem.MainModule main = screamEffect.main;
			main.startColor = ((currentPhase == 1) ? phase1ScreamTint : phase2ScreamTint);
			screamEffect.Play();
		}
	}

	public void ApplyAppearKnockback()
	{
		if (!(playerTransform == null) && !(playerMovement == null))
		{
			float num = ((teleportPositionLeft != null && teleportPositionRight != null) ? ((teleportPositionLeft.position.x + teleportPositionRight.position.x) * 0.5f) : base.transform.position.x);
			float num2 = ((playerTransform.position.x > num) ? (-1f) : 1f);
			float num3 = Vector2.Distance(base.transform.position, playerTransform.position);
			float num4 = Mathf.Clamp01(1f - num3 / screamKnockbackFalloffDistance);
			playerMovement.SetVelocity(new Vector2(num2 * appearKnockbackHorizontalForce * num4, appearKnockbackVerticalForce * num4));
		}
	}

	public void ApplyAppearKnockbackWithShake()
	{
		ApplyAppearKnockback();
		CameraShakeManager.Instance?.ShakeCamera(screamShakeAmplitude, screamShakeFrequency, appearKnockbackDuration);
		StartCoroutine(ShakeSpriteRoutine(appearKnockbackDuration));
	}

	private void ChangeState(BossState newState)
	{
		StopLoopedSound(ref shootSoundHandle);
		StopLoopedSound(ref reloadSoundHandle);
		StopLoopedSound(ref stunnedSoundHandle);
		if (newState == BossState.Shooting)
		{
			shootSoundHandle = SFXManager.Instance?.PlayLooped("B_Shoot", base.transform.position) ?? (-1);
		}
		if (newState == BossState.Reloading)
		{
			reloadSoundHandle = SFXManager.Instance?.PlayLooped("B_Reload", base.transform.position) ?? (-1);
		}
		if (newState == BossState.Stunned)
		{
			stunnedSoundHandle = SFXManager.Instance?.PlayLooped("B_Stunned", base.transform.position) ?? (-1);
		}
		currentState = newState;
		stateTimer = 0f;
		if (newState == BossState.Shooting)
		{
			canShoot = false;
		}
		UpdateAnimation();
	}

	private void UpdateAnimation()
	{
		if (!(animator == null))
		{
			animator.SetInteger("State", GetAnimationState());
		}
	}

	private int GetAnimationState()
	{
		if (currentPhase == 1)
		{
			return currentState switch
			{
				BossState.Idle => 1, 
				BossState.Shooting => 2, 
				BossState.Reloading => 3, 
				BossState.Stunned => 4, 
				BossState.Screaming => 11, 
				BossState.Teleporting => 13, 
				_ => 1, 
			};
		}
		return currentState switch
		{
			BossState.Idle => 5, 
			BossState.Shooting => 6, 
			BossState.Reloading => 7, 
			BossState.Stunned => 8, 
			BossState.Screaming => 12, 
			BossState.Teleporting => 9, 
			_ => 5, 
		};
	}

	public void OnTeleportInAnimationComplete()
	{
		_teleportInAnimationComplete = true;
		if (spriteRenderer != null)
		{
			spriteRenderer.enabled = false;
		}
	}

	public void OnTeleportOutAnimationComplete()
	{
		_teleportOutAnimationComplete = true;
	}

	public void OnPhase1TeleportInAnimationComplete()
	{
		_phase1TeleportInAnimationComplete = true;
		if (spriteRenderer != null)
		{
			spriteRenderer.enabled = false;
		}
	}

	public void OnPhase1TeleportOutAnimationComplete()
	{
		_phase1TeleportOutAnimationComplete = true;
	}

	public void OnPhaseTransitionAnimationComplete()
	{
		_phaseTransitionAnimationComplete = true;
	}

	private void EnableBarrageCamera()
	{
		if (barrageCamera != null)
		{
			barrageCamera.gameObject.SetActive(value: true);
		}
	}

	private void DisableBarrageCamera()
	{
		if (barrageCamera != null)
		{
			barrageCamera.gameObject.SetActive(value: false);
		}
	}

	private IEnumerator TeleportSequence()
	{
		bool isPhase1 = currentPhase == 1;
		if (isPhase1)
		{
			_phase1TeleportInAnimationComplete = false;
			animator?.SetInteger("State", 13);
		}
		else
		{
			_teleportInAnimationComplete = false;
			animator?.SetInteger("State", 9);
		}
		SFXManager.Instance?.Play("B_TeleportIn", base.transform.position);
		float teleportInFallbackTimer = 0f;
		yield return new WaitUntil(delegate
		{
			teleportInFallbackTimer += Time.deltaTime;
			return (isPhase1 ? _phase1TeleportInAnimationComplete : _teleportInAnimationComplete) || teleportInFallbackTimer >= 2f;
		});
		if (spriteRenderer != null && spriteRenderer.enabled)
		{
			spriteRenderer.enabled = false;
		}
		if (bossCollider != null)
		{
			bossCollider.enabled = false;
		}
		if ((bool)bossUI)
		{
			yield return bossUI.FadeOut();
		}
		EnableBarrageCamera();
		if ((bool)bossBarrage)
		{
			float healthRatio = Mathf.Clamp01((float)currentHealthPoints / (float)maxHealthPoints);
			yield return bossBarrage.RunRandomBarrage(currentPhase, healthRatio);
		}
		if (!isPhase1)
		{
			TeleportToOppositePosition();
		}
		FaceArenaCenter();
		DisableBarrageCamera();
		if (isPhase1)
		{
			_phase1TeleportOutAnimationComplete = false;
			animator?.SetInteger("State", 14);
		}
		else
		{
			_teleportOutAnimationComplete = false;
			animator?.SetInteger("State", 10);
		}
		SFXManager.Instance?.Play("B_TeleportOut", base.transform.position);
		if (spriteRenderer != null)
		{
			spriteRenderer.enabled = true;
		}
		float teleportOutFallbackTimer = 0f;
		yield return new WaitUntil(delegate
		{
			teleportOutFallbackTimer += Time.deltaTime;
			return (isPhase1 ? _phase1TeleportOutAnimationComplete : _teleportOutAnimationComplete) || teleportOutFallbackTimer >= 2f;
		});
		animator?.SetInteger("State", isPhase1 ? 1 : 5);
		yield return new WaitForSeconds(appearKnockbackDuration);
		if (bossCollider != null)
		{
			bossCollider.enabled = true;
		}
		if ((bool)bossUI)
		{
			yield return bossUI.FadeIn();
		}
		isAttackable = isPhase1;
		ChangeState(BossState.Idle);
	}

	private void TeleportToOppositePosition()
	{
		if (!(teleportPositionLeft == null) && !(teleportPositionRight == null))
		{
			base.transform.position = (onRight ? teleportPositionLeft.position : teleportPositionRight.position);
			onRight = !onRight;
		}
	}

	public void FireBullet()
	{
		if (bulletSpawnPoint == null || playerTransform == null || currentState != BossState.Shooting)
		{
			return;
		}
		GameObject gameObject = ObjectPool.Instance.SpawnFromPool(bulletPoolTag, bulletSpawnPoint.position, Quaternion.identity);
		if (gameObject != null)
		{
			BossBullet component = gameObject.GetComponent<BossBullet>();
			if (component != null)
			{
				Vector2 direction = (playerTransform.position - bulletSpawnPoint.position).normalized;
				component.Initialize(direction);
			}
		}
		CameraShakeManager.Instance.ShakeCamera(0.15f, 1f, 0.1f);
		canShoot = false;
	}

	public void OnParriedBulletHit()
	{
		if (clearBulletsOnParriedHit)
		{
			ClearActiveBossBullets();
		}
		if (currentPhase == 1)
		{
			if (enableHurtEffect)
			{
				vfxService?.PlayEffect(hurtEffectName, base.transform.position);
			}
			TakeDamage(1);
			ChangeState(BossState.Stunned);
			return;
		}
		if (currentState != BossState.Stunned)
		{
			vfxService?.PlayEffect("ArmourBreak", base.transform.position);
			SFXManager.Instance?.Play("B_ArmourBreak", base.transform.position);
		}
		isAttackable = true;
		TakeDamage(1);
		ChangeState(BossState.Stunned);
	}

	public override bool TakeDamage(int damageAmount)
	{
		if (!isAttackable)
		{
			return false;
		}
		if (currentPhase == 2 && !isAttackable)
		{
			return false;
		}
		return base.TakeDamage(damageAmount);
	}

	public override bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce)
	{
		if (!isAttackable)
		{
			return false;
		}
		if (currentPhase == 2 && !isAttackable)
		{
			return false;
		}
		return base.TakeDamage(damageAmount, knockbackDirection, knockbackForce);
	}

	public override bool CanReceiveDamage()
	{
		if (!isAttackable)
		{
			return false;
		}
		return base.CanReceiveDamage();
	}

	protected override void OnHit(Vector2 knockbackDirection, float knockbackForce)
	{
		if (isAttackable && (currentPhase != 2 || isAttackable))
		{
			base.OnHit(Vector2.zero, 0f);
			bossUI?.UpdateHealth(currentHealthPoints, maxHealthPoints);
			if (currentHealthPoints <= phase2HealthThreshold && currentPhase == 1)
			{
				EnterPhase2();
			}
		}
	}

	private void EnterPhase2()
	{
		isAttackable = false;
		bossStarted = false;
		StopLoopedSound(ref stunnedSoundHandle);
		StopLoopedSound(ref shootSoundHandle);
		StopLoopedSound(ref reloadSoundHandle);
		SFXManager.Instance?.Play("B_Phase2", base.transform.position);
		animator?.SetInteger("State", 1);
		bossUI?.FadeOut();
		if (phase2DialoguePrefab != null && !hasShownPhase2Dialogue)
		{
			GameObject obj = UnityEngine.Object.Instantiate(phase2DialoguePrefab);
			hasShownPhase2Dialogue = true;
			BossDialogue component = obj.GetComponent<BossDialogue>();
			if (component != null)
			{
				component.Initialize(delegate
				{
					StartCoroutine(PhaseTransitionThenResume());
				});
			}
			else
			{
				StartCoroutine(PhaseTransitionThenResume());
			}
		}
		else
		{
			StartCoroutine(PhaseTransitionThenResume());
		}
	}

	private IEnumerator PhaseTransitionThenResume()
	{
		bossUI?.ResumeTimerAndPlayerUI();
		if (bossUI != null)
		{
			yield return bossUI.FadeIn();
		}
		_phaseTransitionAnimationComplete = false;
		animator?.SetInteger("State", 15);
		yield return new WaitUntil(() => _phaseTransitionAnimationComplete);
		currentPhase = 2;
		currentState = BossState.Screaming;
		stateTimer = 0f;
		StopLoopedSound(ref shootSoundHandle);
		StopLoopedSound(ref reloadSoundHandle);
		StopLoopedSound(ref stunnedSoundHandle);
		animator?.SetInteger("State", 5);
		yield return StartCoroutine(ScreamSequence(screamDuration, teleportAfter: false));
		if (alcoholItemInScene != null && !hasSpawnedAlcohol)
		{
			alcoholItemInScene.SetActive(value: true);
			hasSpawnedAlcohol = true;
		}
		if ((bool)bossUI)
		{
			yield return bossUI.FadeIn();
		}
		bossStarted = true;
		UpdateAnimation();
		ChangeState(BossState.Idle);
	}

	public void StartBossFight()
	{
		bossStarted = true;
		hasFightEverStarted = true;
		bossUI?.Show(currentHealthPoints, maxHealthPoints);
		ChangeState(BossState.Screaming);
		StartCoroutine(ScreamSequence(-1f, teleportAfter: false));
	}

	protected override void Die()
	{
		StopAllCoroutines();
		StopLoopedSound(ref shootSoundHandle);
		StopLoopedSound(ref reloadSoundHandle);
		StopLoopedSound(ref stunnedSoundHandle);
		SFXManager.Instance?.StopAll();
		SFXManager.Instance?.Play("B_Defeated", base.transform.position);
		ChangeState(BossState.Dead);
		if (spriteRenderer != null)
		{
			spriteRenderer.enabled = true;
		}
		animator?.SetInteger("State", 8);
		if (bossCollider != null)
		{
			bossCollider.enabled = false;
		}
		isAttackable = false;
		bossStarted = false;
		isInDeathSequence = true;
		OnBossDied?.Invoke();
		MusicManager.Instance?.StopBossMusic();
		LevelStatsManager.Instance?.RegisterEnemyKill();
		bossUI?.Hide();
		if ((bool)bossDeathSequence)
		{
			bossDeathSequence.Run(animator, 16, delegate(float duration)
			{
				StartCoroutine(ShakeSpriteRoutine(duration));
			}, delegate
			{
				isInDeathSequence = false;
			});
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (bulletSpawnPoint != null)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(bulletSpawnPoint.position, 0.5f);
		}
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(base.transform.position, screamKnockbackFalloffDistance);
	}

	private void PlayIdleSound()
	{
	}

	private void ReloadSound()
	{
	}

	private void StopLoopedSound(ref int handle)
	{
		if (handle >= 0)
		{
			SFXManager.Instance?.Stop(handle);
			handle = -1;
		}
	}

	private IEnumerator ShakeSpriteRoutine(float duration)
	{
		Vector3 originalLocalPos = spriteRenderer.transform.localPosition;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			float x = Mathf.Sin(elapsed * spriteShakeSpeed) * spriteShakeIntensity;
			spriteRenderer.transform.localPosition = originalLocalPos + new Vector3(x, 0f, 0f);
			elapsed += Time.deltaTime;
			yield return null;
		}
		spriteRenderer.transform.localPosition = originalLocalPos;
	}

	private void ClearActiveBossBullets()
	{
		if (ObjectPool.Instance == null)
		{
			return;
		}
		ObjectPool.Instance.ForEachActive(bulletPoolTag, delegate(GameObject obj)
		{
			BossBullet component = obj.GetComponent<BossBullet>();
			if (component != null && component.IsAttacking)
			{
				obj.SetActive(value: false);
			}
		});
	}

	public void OnDialogueStarted()
	{
		bossUI?.OnDialogueStarted();
	}
}

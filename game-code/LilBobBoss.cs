using System;
using System.Collections;
using UnityEngine;

public class LilBobBoss : Actor
{
	public enum LilBobState
	{
		Idle,
		Firing,
		Dead
	}

	[Header("Lil' Bob Settings")]
	[SerializeField]
	private int bossMaxHealth = 100;

	[Header("Rocket Volley")]
	[Tooltip("Pool tag for the SineRocket projectile.")]
	[SerializeField]
	private string rocketPoolTag = "SineRocket";

	[SerializeField]
	private Transform rocketSpawnPoint;

	[SerializeField]
	private int rocketsPerVolley = 5;

	[SerializeField]
	private float timeBetweenRockets = 0.3f;

	[Tooltip("Spread angle in degrees applied randomly to each rocket.")]
	[SerializeField]
	private float volleySpreadAngle = 15f;

	[Header("Rocket Arc (match SineRocket prefab values)")]
	[SerializeField]
	private float gizmoHorizontalSpeed = 6f;

	[SerializeField]
	private float gizmoLaunchSpeed = 12f;

	[SerializeField]
	private float gizmoGravity = 15f;

	[SerializeField]
	private float gizmoLaunchSpeedVariance = 3f;

	[Header("Timing")]
	[Tooltip("Pause between volleys.")]
	[SerializeField]
	private float idleDurationMin = 0.5f;

	[SerializeField]
	private float idleDurationMax = 1f;

	[SerializeField]
	private float delayBeforeFirstAttack = 1.5f;

	[Header("UI")]
	[SerializeField]
	private GameObject bossUIPanel;

	[Header("Debug")]
	[SerializeField]
	private bool autoStartFight;

	[SerializeField]
	private bool showGizmos = true;

	[SerializeField]
	private int gizmoArcSteps = 60;

	[SerializeField]
	private float gizmoTimeStep = 0.05f;

	private LilBobState currentState;

	private Transform playerTransform;

	private bool bossStarted;

	private bool isFacingRight = true;

	protected override void Awake()
	{
		base.Awake();
		maxHealthPoints = bossMaxHealth;
		currentHealthPoints = maxHealthPoints;
	}

	private void Start()
	{
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (gameObject != null)
		{
			playerTransform = gameObject.transform;
		}
		if (bossUIPanel != null)
		{
			bossUIPanel.SetActive(value: false);
		}
		if (autoStartFight)
		{
			StartFight();
		}
	}

	public void StartFight()
	{
		bossStarted = true;
		if (bossUIPanel != null)
		{
			bossUIPanel.SetActive(value: true);
		}
		StartCoroutine(BossLoop());
	}

	private void Update()
	{
		if (bossStarted && currentState != LilBobState.Dead)
		{
			FacePlayer();
		}
	}

	private IEnumerator BossLoop()
	{
		yield return new WaitForSeconds(delayBeforeFirstAttack);
		while (currentState != LilBobState.Dead)
		{
			yield return StartCoroutine(FireVolley());
			if (currentState == LilBobState.Dead)
			{
				break;
			}
			ChangeState(LilBobState.Idle);
			float seconds = UnityEngine.Random.Range(idleDurationMin, idleDurationMax);
			yield return new WaitForSeconds(seconds);
		}
	}

	private IEnumerator FireVolley()
	{
		ChangeState(LilBobState.Firing);
		Vector3 spawnPos = ((rocketSpawnPoint != null) ? rocketSpawnPoint.position : base.transform.position);
		for (int i = 0; i < rocketsPerVolley; i++)
		{
			if (currentState == LilBobState.Dead)
			{
				break;
			}
			if (playerTransform == null)
			{
				break;
			}
			Vector2 normalized = ((Vector2)playerTransform.position - (Vector2)spawnPos).normalized;
			float f = UnityEngine.Random.Range(0f - volleySpreadAngle, volleySpreadAngle) * (MathF.PI / 180f);
			Vector2 direction = new Vector2(normalized.x * Mathf.Cos(f) - normalized.y * Mathf.Sin(f), normalized.x * Mathf.Sin(f) + normalized.y * Mathf.Cos(f));
			GameObject gameObject = ObjectPool.Instance?.SpawnFromPool(rocketPoolTag, spawnPos, Quaternion.identity);
			if (gameObject != null)
			{
				gameObject.GetComponent<SineRocket>()?.Initialize(direction, base.gameObject);
			}
			SFXManager.Instance?.Play("ITEM_RocketLaunch", spawnPos);
			yield return new WaitForSeconds(timeBetweenRockets);
		}
	}

	protected override void Die()
	{
		if (currentState != LilBobState.Dead)
		{
			StopAllCoroutines();
			ChangeState(LilBobState.Dead);
			bossStarted = false;
			SFXManager.Instance?.StopAll();
			SFXManager.Instance?.Play("B_Defeated", base.transform.position);
			if (bossUIPanel != null)
			{
				bossUIPanel.SetActive(value: false);
			}
			LevelStatsManager.Instance?.RegisterEnemyKill();
			base.Die();
			Debug.Log("[LilBobBoss] Lil' Bob has been defeated!");
		}
	}

	private void ChangeState(LilBobState newState)
	{
		currentState = newState;
	}

	private void FacePlayer()
	{
		if (!(playerTransform == null))
		{
			bool flag = playerTransform.position.x > base.transform.position.x;
			if (flag != isFacingRight)
			{
				isFacingRight = flag;
				Flip(isFacingRight);
			}
		}
	}
}

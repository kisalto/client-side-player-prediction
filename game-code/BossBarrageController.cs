using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossBarrageController : MonoBehaviour
{
	public enum BarrageType
	{
		AtPlayer,
		Predictive,
		MultiWave,
		AtPlayerCluster,
		PredictiveCluster,
		HalfBounds,
		TopAtPlayer,
		TopPredictive,
		TopMultiWave,
		TopAtPlayerCluster,
		TopPredictiveCluster,
		TopHalfBounds,
		InterweavedUpDown
	}

	[Serializable]
	public class BarrageEntry
	{
		public BarrageType type;

		[Tooltip("Which phase this attack is available in. 0 = both phases, 1 = Phase 1 only, 2 = Phase 2 only.")]
		[Range(0f, 2f)]
		public int phase;

		[Tooltip("Minimum health percentage requried for this attack to be eligible (0 = no minimum)")]
		[Range(0f, 1f)]
		public float minHealthPercent;

		[Tooltip("Maximum health percentage at which this attack is eligible (1 = always eligible).")]
		[Range(0f, 1f)]
		public float maxHealthPercent = 1f;

		[Tooltip("Relative spawn weight. Higher = more likely compared to other eligible attacks at the same time.")]
		[Min(0f)]
		public float weight = 10f;
	}

	[Header("Portal Attack")]
	[SerializeField]
	private GameObject portalPrefab;

	[SerializeField]
	private float portalSpawnY = -3f;

	[SerializeField]
	private GameObject topPortalPrefab;

	[SerializeField]
	private float topPortalSpawnY = 5f;

	[SerializeField]
	private int atPlayerPortalCount = 2;

	[SerializeField]
	private int predictivePortalCount = 2;

	[SerializeField]
	private Transform portalSpawnBoundaryLeft;

	[SerializeField]
	private Transform portalSpawnBoundaryRight;

	[Header("Barrage Attacks")]
	[SerializeField]
	private float predictiveLookAheadDistance = 4f;

	[SerializeField]
	private int multiWavePortalCount = 3;

	[SerializeField]
	private float multiWaveSpread = 8f;

	[SerializeField]
	private int clusterSize = 3;

	[SerializeField]
	private float clusterSpacing = 2.5f;

	[SerializeField]
	private int clusterWaveCount = 2;

	[SerializeField]
	private float clusterWaveGapDuration = 1.2f;

	[SerializeField]
	private int halfBoundsPortalCount = 4;

	[SerializeField]
	private int interweavedColumnCount = 6;

	[SerializeField]
	private int interweavedWaveCount = 3;

	[Header("Barrage Config")]
	[SerializeField]
	private List<BarrageEntry> barrageEntries = new List<BarrageEntry>();

	[Header("Portal Tints")]
	[SerializeField]
	private Color phase1PortalTint = new Color(0.4f, 1f, 1f, 1f);

	[SerializeField]
	private Color phase2PortalTint = new Color(1f, 0.627f, 0f, 1f);

	private Transform playerTransform;

	private PlayerMovementController playerMovement;

	private BarrageType lastBarrageType = (BarrageType)(-1);

	private const float PORTAL_GAP_DURATION_MAX = 0.5f;

	private const float PORTAL_GAP_DURATION_MIN = 0.1f;

	private const float MULTI_WAVE_DELAY_MAX = 0.3f;

	private const float MULTI_WAVE_DELAY_MIN = 0.08f;

	private const float REPEAT_PENALTY_MULTIPLIER = 0.3f;

	private int activePhase;

	private float activeHealthRatio;

	public void Initialize(Transform player, PlayerMovementController movement)
	{
		playerTransform = player;
		playerMovement = movement;
	}

	public Coroutine RunRandomBarrage(int phase, float healthRatio)
	{
		activePhase = phase;
		activeHealthRatio = healthRatio;
		return StartCoroutine(RunBarrageRoutine(lastBarrageType = PickBarrageType(phase, healthRatio)));
	}

	public Coroutine RunBarrage(BarrageType type, int phase, float healthRatio)
	{
		activePhase = phase;
		activeHealthRatio = healthRatio;
		lastBarrageType = type;
		return StartCoroutine(RunBarrageRoutine(type));
	}

	private BarrageType PickBarrageType(int phase, float healthPercent)
	{
		List<BarrageEntry> list = new List<BarrageEntry>();
		List<float> list2 = new List<float>();
		float num = 0f;
		foreach (BarrageEntry barrageEntry in barrageEntries)
		{
			bool num2 = barrageEntry.phase == 0 || barrageEntry.phase == phase;
			bool flag = healthPercent >= barrageEntry.minHealthPercent && healthPercent <= barrageEntry.maxHealthPercent;
			if (num2 && flag && !(barrageEntry.weight <= 0f))
			{
				float num3 = ((barrageEntry.type == lastBarrageType) ? (barrageEntry.weight * 0.3f) : barrageEntry.weight);
				list.Add(barrageEntry);
				list2.Add(num3);
				num += num3;
			}
		}
		if (list.Count == 0)
		{
			Debug.LogWarning("[BossBarrageController] No eligible barrage entries. Falling back to AtPlayer.");
			return BarrageType.AtPlayer;
		}
		float num4 = UnityEngine.Random.Range(0f, num);
		for (int i = 0; i < list.Count; i++)
		{
			if (num4 < list2[i])
			{
				return list[i].type;
			}
			num4 -= list2[i];
		}
		return list[list.Count - 1].type;
	}

	private IEnumerator RunBarrageRoutine(BarrageType type)
	{
		switch (type)
		{
		case BarrageType.Predictive:
			yield return StartCoroutine(RunPredictiveBarrage());
			break;
		case BarrageType.MultiWave:
			yield return StartCoroutine(RunMultiWaveBarrage());
			break;
		case BarrageType.AtPlayerCluster:
			yield return StartCoroutine(RunAtPlayerClusterBarrage());
			break;
		case BarrageType.PredictiveCluster:
			yield return StartCoroutine(RunPredictiveClusterBarrage());
			break;
		case BarrageType.HalfBounds:
			yield return StartCoroutine(RunHalfBoundsBarrage());
			break;
		case BarrageType.TopAtPlayer:
			yield return StartCoroutine(RunTopAtPlayerBarrage());
			break;
		case BarrageType.TopPredictive:
			yield return StartCoroutine(RunTopPredictiveBarrage());
			break;
		case BarrageType.TopMultiWave:
			yield return StartCoroutine(RunTopMultiWaveBarrage());
			break;
		case BarrageType.TopAtPlayerCluster:
			yield return StartCoroutine(RunTopAtPlayerClusterBarrage());
			break;
		case BarrageType.TopPredictiveCluster:
			yield return StartCoroutine(RunTopPredictiveClusterBarrage());
			break;
		case BarrageType.TopHalfBounds:
			yield return StartCoroutine(RunTopHalfBoundsBarrage());
			break;
		case BarrageType.InterweavedUpDown:
			yield return StartCoroutine(RunInterweavedUpDownBarrage());
			break;
		default:
			yield return StartCoroutine(RunAtPlayerBarrage());
			break;
		}
	}

	private (float xMin, float xMax) GetPortalSpawnXBounds()
	{
		if (portalSpawnBoundaryLeft == null || portalSpawnBoundaryRight == null)
		{
			Debug.LogWarning("[BossBarrageController] Portal spawn boundary transforms are not assigned.");
			return (xMin: float.MinValue, xMax: float.MaxValue);
		}
		float x = portalSpawnBoundaryLeft.position.x;
		float x2 = portalSpawnBoundaryRight.position.x;
		if (!(x < x2))
		{
			return (xMin: x2, xMax: x);
		}
		return (xMin: x, xMax: x2);
	}

	private GameObject SpawnPortal(float xPosition, bool isMultiWave = false)
	{
		if (!portalPrefab)
		{
			return null;
		}
		(float xMin, float xMax) portalSpawnXBounds = GetPortalSpawnXBounds();
		float item = portalSpawnXBounds.xMin;
		float item2 = portalSpawnXBounds.xMax;
		float x = Mathf.Clamp(xPosition, item, item2);
		GameObject obj = UnityEngine.Object.Instantiate(position: new Vector2(x, portalSpawnY), original: portalPrefab, rotation: Quaternion.identity);
		Color tint = ((activePhase == 1) ? phase1PortalTint : phase2PortalTint);
		BossPortal component = obj.GetComponent<BossPortal>();
		if ((object)component != null)
		{
			component.Initialize(isMultiWave, tint);
			return obj;
		}
		return obj;
	}

	private GameObject SpawnTopPortal(float xPosition)
	{
		if (!topPortalPrefab)
		{
			Debug.LogWarning("[BossBarrageController] topPortalPrefab is not assigned.");
		}
		(float xMin, float xMax) portalSpawnXBounds = GetPortalSpawnXBounds();
		float item = portalSpawnXBounds.xMin;
		float item2 = portalSpawnXBounds.xMax;
		float x = Mathf.Clamp(xPosition, item, item2);
		GameObject obj = UnityEngine.Object.Instantiate(position: new Vector2(x, topPortalSpawnY), original: topPortalPrefab, rotation: Quaternion.identity);
		Color tint = ((activePhase == 1) ? phase1PortalTint : phase2PortalTint);
		TopBossPortal component = obj.GetComponent<TopBossPortal>();
		if ((object)component != null)
		{
			component.Initialize(tint);
			return obj;
		}
		return obj;
	}

	private float GetHealthScaledDuration(float maxDuration, float minDuration)
	{
		return Mathf.Lerp(minDuration, maxDuration, activeHealthRatio);
	}

	private float GetPredictedPlayerX(float playerX, float playerY, float portalY, float bulletSpeed, float openAnimDuration, float shootDelay)
	{
		float num = ((playerMovement != null) ? playerMovement.Velocity.x : 0f);
		if (Mathf.Abs(num) < 0.5f)
		{
			return playerX;
		}
		float num2 = Mathf.Abs(playerY - portalY);
		float num3 = ((bulletSpeed > 0f) ? (num2 / bulletSpeed) : 0f);
		float num4 = openAnimDuration + shootDelay + num3;
		return playerX + num * num4;
	}

	private float GetPlayerX()
	{
		if (!(playerTransform != null))
		{
			return base.transform.position.x;
		}
		return playerTransform.position.x;
	}

	private float GetPlayerY()
	{
		if (!(playerTransform != null))
		{
			return base.transform.position.y;
		}
		return playerTransform.position.y;
	}

	private void SpawnCluster(float centerX, List<GameObject> activePortals)
	{
		float num = (float)(clusterSize - 1) * clusterSpacing;
		float num2 = centerX - num * 0.5f;
		for (int i = 0; i < clusterSize; i++)
		{
			float xPosition = num2 + (float)i * clusterSpacing;
			activePortals.Add(SpawnPortal(xPosition, isMultiWave: true));
		}
	}

	private void SpawnTopCluster(float centerX, List<GameObject> activePortals)
	{
		float num = (float)(clusterSize - 1) * clusterSpacing;
		float num2 = centerX - num * 0.5f;
		for (int i = 0; i < clusterSize; i++)
		{
			float xPosition = num2 + (float)i * clusterSpacing;
			activePortals.Add(SpawnTopPortal(xPosition));
		}
	}

	private IEnumerator WaitForAllPortals(List<GameObject> portals)
	{
		yield return new WaitUntil(delegate
		{
			foreach (GameObject portal in portals)
			{
				if ((bool)portal)
				{
					return false;
				}
			}
			return true;
		});
	}

	private IEnumerator RunAtPlayerBarrage()
	{
		float gapDuration = GetHealthScaledDuration(0.5f, 0.1f);
		for (int i = 0; i < atPlayerPortalCount; i++)
		{
			GameObject portal = SpawnPortal(GetPlayerX());
			yield return new WaitUntil(() => portal == null);
			if (i < atPlayerPortalCount - 1)
			{
				yield return new WaitForSeconds(gapDuration);
			}
		}
	}

	private IEnumerator RunPredictiveBarrage()
	{
		float gapDuration = GetHealthScaledDuration(0.5f, 0.1f);
		for (int i = 0; i < predictivePortalCount; i++)
		{
			float predictedPlayerX = GetPredictedPlayerX(GetPlayerX(), GetPlayerY(), portalSpawnY, 12f, 1.5f, 0f);
			GameObject portal = SpawnPortal(predictedPlayerX);
			yield return new WaitUntil(() => portal == null);
			if (i < predictivePortalCount - 1)
			{
				yield return new WaitForSeconds(gapDuration);
			}
		}
	}

	private IEnumerator RunMultiWaveBarrage()
	{
		(float, float) portalSpawnXBounds = GetPortalSpawnXBounds();
		float xMin = portalSpawnXBounds.Item1;
		float xMax = portalSpawnXBounds.Item2;
		float dynamicDelay = GetHealthScaledDuration(0.3f, 0.08f);
		List<GameObject> activePortals = new List<GameObject>();
		for (int i = 0; i < multiWavePortalCount; i++)
		{
			float t = ((multiWavePortalCount > 1) ? ((float)i / (float)(multiWavePortalCount - 1)) : 0.5f);
			float xPosition = Mathf.Lerp(xMin, xMax, t);
			activePortals.Add(SpawnPortal(xPosition, isMultiWave: true));
			if (i < multiWavePortalCount - 1)
			{
				yield return new WaitForSeconds(dynamicDelay);
			}
		}
		yield return WaitForAllPortals(activePortals);
	}

	private IEnumerator RunAtPlayerClusterBarrage()
	{
		for (int wave = 0; wave < clusterWaveCount; wave++)
		{
			List<GameObject> list = new List<GameObject>();
			SpawnCluster(GetPlayerX(), list);
			yield return WaitForAllPortals(list);
			if (wave < clusterWaveCount - 1)
			{
				yield return new WaitForSeconds(clusterWaveGapDuration);
			}
		}
	}

	private IEnumerator RunPredictiveClusterBarrage()
	{
		for (int wave = 0; wave < clusterWaveCount; wave++)
		{
			float predictedPlayerX = GetPredictedPlayerX(GetPlayerX(), GetPlayerY(), portalSpawnY, 12f, 1.5f, 0f);
			List<GameObject> list = new List<GameObject>();
			SpawnCluster(predictedPlayerX, list);
			yield return WaitForAllPortals(list);
			if (wave < clusterWaveCount - 1)
			{
				yield return new WaitForSeconds(clusterWaveGapDuration);
			}
		}
	}

	private IEnumerator RunHalfBoundsBarrage()
	{
		(float, float) portalSpawnXBounds = GetPortalSpawnXBounds();
		float xMin = portalSpawnXBounds.Item1;
		float xMax = portalSpawnXBounds.Item2;
		float midPoint = (xMin + xMax) * 0.5f;
		float playerX = GetPlayerX();
		bool startOnLeftHalf = playerX <= midPoint;
		for (int wave = 0; wave < 2; wave++)
		{
			bool num = ((wave == 0) ? startOnLeftHalf : (!startOnLeftHalf));
			float a = (num ? xMin : midPoint);
			float b = (num ? midPoint : xMax);
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < halfBoundsPortalCount; i++)
			{
				float t = ((halfBoundsPortalCount > 1) ? ((float)i / (float)(halfBoundsPortalCount - 1)) : 0.5f);
				float xPosition = Mathf.Lerp(a, b, t);
				list.Add(SpawnPortal(xPosition, isMultiWave: true));
			}
			yield return WaitForAllPortals(list);
		}
	}

	private IEnumerator RunTopAtPlayerBarrage()
	{
		float gapDuration = GetHealthScaledDuration(0.5f, 0.1f);
		for (int i = 0; i < atPlayerPortalCount; i++)
		{
			GameObject portal = SpawnTopPortal(GetPlayerX());
			yield return new WaitUntil(() => portal == null);
			if (i < atPlayerPortalCount - 1)
			{
				yield return new WaitForSeconds(gapDuration);
			}
		}
	}

	private IEnumerator RunTopPredictiveBarrage()
	{
		float gapDuration = GetHealthScaledDuration(0.5f, 0.1f);
		for (int i = 0; i < predictivePortalCount; i++)
		{
			float predictedPlayerX = GetPredictedPlayerX(GetPlayerX(), GetPlayerY(), topPortalSpawnY, 14f, 1.5f, 0f);
			GameObject portal = SpawnTopPortal(predictedPlayerX);
			yield return new WaitUntil(() => portal == null);
			if (i < predictivePortalCount - 1)
			{
				yield return new WaitForSeconds(gapDuration);
			}
		}
	}

	private IEnumerator RunTopMultiWaveBarrage()
	{
		(float xMin, float xMax) portalSpawnXBounds = GetPortalSpawnXBounds();
		float item = portalSpawnXBounds.xMin;
		float item2 = portalSpawnXBounds.xMax;
		List<GameObject> list = new List<GameObject>();
		for (int i = 0; i < multiWavePortalCount; i++)
		{
			float t = ((multiWavePortalCount > 1) ? ((float)i / (float)(multiWavePortalCount - 1)) : 0.5f);
			float xPosition = Mathf.Lerp(item, item2, t);
			list.Add(SpawnTopPortal(xPosition));
		}
		yield return WaitForAllPortals(list);
	}

	private IEnumerator RunTopAtPlayerClusterBarrage()
	{
		for (int wave = 0; wave < clusterWaveCount; wave++)
		{
			List<GameObject> list = new List<GameObject>();
			SpawnTopCluster(GetPlayerX(), list);
			yield return WaitForAllPortals(list);
			if (wave < clusterWaveCount - 1)
			{
				yield return new WaitForSeconds(clusterWaveGapDuration);
			}
		}
	}

	private IEnumerator RunTopPredictiveClusterBarrage()
	{
		for (int wave = 0; wave < clusterWaveCount; wave++)
		{
			float predictedPlayerX = GetPredictedPlayerX(GetPlayerX(), GetPlayerY(), topPortalSpawnY, 14f, 1.5f, 0f);
			List<GameObject> list = new List<GameObject>();
			SpawnTopCluster(predictedPlayerX, list);
			yield return WaitForAllPortals(list);
			if (wave < clusterWaveCount - 1)
			{
				yield return new WaitForSeconds(clusterWaveGapDuration);
			}
		}
	}

	private IEnumerator RunTopHalfBoundsBarrage()
	{
		(float, float) portalSpawnXBounds = GetPortalSpawnXBounds();
		float xMin = portalSpawnXBounds.Item1;
		float xMax = portalSpawnXBounds.Item2;
		float midPoint = (xMin + xMax) * 0.5f;
		float playerX = GetPlayerX();
		bool startOnLeftHalf = playerX <= midPoint;
		for (int wave = 0; wave < 2; wave++)
		{
			bool num = ((wave == 0) ? startOnLeftHalf : (!startOnLeftHalf));
			float a = (num ? xMin : midPoint);
			float b = (num ? midPoint : xMax);
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < halfBoundsPortalCount; i++)
			{
				float t = ((halfBoundsPortalCount > 1) ? ((float)i / (float)(halfBoundsPortalCount - 1)) : 0.5f);
				float xPosition = Mathf.Lerp(a, b, t);
				list.Add(SpawnTopPortal(xPosition));
			}
			yield return WaitForAllPortals(list);
		}
	}

	private IEnumerator RunInterweavedUpDownBarrage()
	{
		(float, float) portalSpawnXBounds = GetPortalSpawnXBounds();
		float xMin = portalSpawnXBounds.Item1;
		float num = portalSpawnXBounds.Item2 - xMin;
		float columnSpacing = num / (float)interweavedColumnCount;
		for (int wave = 0; wave < interweavedWaveCount; wave++)
		{
			List<GameObject> list = new List<GameObject>();
			float num2 = ((wave % 2 == 1) ? (xMin + columnSpacing * 0.5f) : xMin);
			for (int i = 0; i < interweavedColumnCount; i++)
			{
				float xPosition = num2 + (float)i * columnSpacing;
				GameObject gameObject = ((i % 2 == 0) ? SpawnPortal(xPosition, isMultiWave: true) : SpawnTopPortal(xPosition));
				if ((bool)gameObject)
				{
					list.Add(gameObject);
				}
			}
			yield return WaitForAllPortals(list);
		}
	}
}

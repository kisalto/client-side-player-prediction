using UnityEngine;
using UnityEngine.Events;

public class LevelStatsManager : MonoBehaviour
{
	[Header("Level Configuration")]
	[SerializeField]
	private LevelStats levelStats;

	[Header("Events")]
	public UnityEvent<int> OnEnemyKilled;

	public UnityEvent<bool> OnSecretCoinCollected;

	public UnityEvent<LevelProgressData> OnLevelComplete;

	private LevelProgressData currentProgress;

	private bool levelActive;

	public static LevelStatsManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
		}
		else
		{
			Instance = this;
		}
	}

	private void Start()
	{
		InitializeLevel();
	}

	private void Update()
	{
		if (levelActive)
		{
			currentProgress.timeElapsed += Time.deltaTime;
		}
	}

	private void InitializeLevel()
	{
		if (levelStats == null)
		{
			Debug.LogError("[LEVEL STATS] No LevelStats ScriptableObject assigned!");
			return;
		}
		currentProgress = new LevelProgressData
		{
			totalEnemies = levelStats.totalEnemies,
			targetTime = levelStats.targetTime,
			enemiesKilled = 0,
			timeElapsed = 0f,
			secretCoinFound = false
		};
		levelActive = true;
	}

	public void RegisterEnemyKill()
	{
		currentProgress.enemiesKilled++;
		OnEnemyKilled?.Invoke(currentProgress.enemiesKilled);
	}

	public void RegisterSecretCoinCollected()
	{
		currentProgress.secretCoinFound = true;
		OnSecretCoinCollected?.Invoke(arg0: true);
	}

	public void CompleteLevel()
	{
		if (levelActive)
		{
			levelActive = false;
			OnLevelComplete?.Invoke(currentProgress);
		}
	}

	public LevelProgressData GetCurrentProgress()
	{
		return currentProgress;
	}

	public string GetNextSceneName()
	{
		if (!(levelStats != null))
		{
			return string.Empty;
		}
		return levelStats.nextSceneName;
	}

	public int GetLevelIndex()
	{
		if (!(levelStats != null))
		{
			return 0;
		}
		return levelStats.levelIndex;
	}
}

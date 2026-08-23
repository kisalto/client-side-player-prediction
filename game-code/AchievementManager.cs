using UnityEngine;
using UnityEngine.SceneManagement;

public class AchievementManager : MonoBehaviour
{
	[SerializeField]
	private AchievementRegistry registry;

	private PlayerCombatController cachedCombat;

	public static AchievementManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		if (registry == null)
		{
			Debug.LogError("[AchievementManager] No AchievementRegistry assigned!");
		}
	}

	private void Start()
	{
		BindPLayerCombat();
	}

	private void OnEnable()
	{
		LiquidCourage.OnDrunk += OnDrunk;
		BigBobBoss.OnBossDied += OnBossDied;
		EnemyController.OnAttackedWhileHiding += OnAttackedWhileHiding;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		LiquidCourage.OnDrunk -= OnDrunk;
		BigBobBoss.OnBossDied -= OnBossDied;
		EnemyController.OnAttackedWhileHiding -= OnAttackedWhileHiding;
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		BindPLayerCombat();
	}

	private void BindPLayerCombat()
	{
		if (cachedCombat != null)
		{
			cachedCombat.OnParrySuccess -= OnParrySuccess;
		}
		cachedCombat = Object.FindAnyObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
		Debug.Log("[AchievementManager] OnSceneLoaded: cachedCombat = " + ((cachedCombat != null) ? cachedCombat.name : "NULL"));
		if (cachedCombat != null)
		{
			cachedCombat.OnParrySuccess += OnParrySuccess;
		}
	}

	private void OnParrySuccess()
	{
		UnlockImmediate("ACH_I_SLAPPED_THY_BLADE");
	}

	private void OnDrunk()
	{
		UnlockImmediate("ACH_DRUNK_IN_TRANSLATION");
	}

	private void OnBossDied()
	{
		UnlockImmediate("ACH_FAMILY_REBOOTION");
	}

	private void OnAttackedWhileHiding()
	{
		UnlockImmediate("ACH_A_FOUL_GOBLET");
	}

	public void EvaluateAll(SaveSlotData slot, int completedLevelIndex)
	{
		if (registry == null || slot == null)
		{
			return;
		}
		foreach (AchievementDefinition definition in registry.Definitions)
		{
			if (!slot.unlockedAchievements.Contains(definition.ApiName) && definition.Evaluate(slot, completedLevelIndex))
			{
				SaveSlotManager.Instance.UnlockAchievement(definition.ApiName);
				Debug.Log("[AchievementManager] Unlocked: " + definition.ApiName + " (" + definition.DisplayName + ")");
			}
		}
	}

	private void UnlockImmediate(string apiName)
	{
		Debug.Log("[AchievementManager] UnlockImmediate called for: " + apiName);
		if (SaveSlotManager.Instance == null)
		{
			Debug.LogWarning("[AchievementManager] SaveSlotManager.Instance is null — cannot unlock " + apiName);
			return;
		}
		SaveSlotData currentSlot = SaveSlotManager.Instance.GetCurrentSlot();
		if (currentSlot == null)
		{
			Debug.LogWarning("[AchievementManager] Current save slot is null — cannot unlock " + apiName);
			return;
		}
		if (currentSlot.unlockedAchievements.Contains(apiName))
		{
			Debug.Log("[AchievementManager] " + apiName + " already unlocked in save data — skipping");
			return;
		}
		SaveSlotManager.Instance.UnlockAchievement(apiName);
		Debug.Log("[AchievementManager] Unlocked (runtime): " + apiName);
	}
}

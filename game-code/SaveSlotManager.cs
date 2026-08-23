using System;
using System.Linq;
using UnityEngine;

public class SaveSlotManager : MonoBehaviour
{
	private const int MaxSaveSlots = 3;

	private const string SlotFilePrefix = "SaveSlot_";

	private const string SlotFileExtension = ".sav";

	private SaveSlotData currentSlot;

	private int currentSlotIndex = -1;

	public static SaveSlotManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
	}

	public bool CreateNewSlot(int slotIndex, string slotName)
	{
		if (slotIndex < 0 || slotIndex >= 3)
		{
			Debug.LogError($"Invalid slot index: {slotIndex}");
			return false;
		}
		return SaveSlot(slotIndex, new SaveSlotData(slotName));
	}

	public bool LoadSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= 3)
		{
			Debug.LogError($"Invalid slot index: {slotIndex}");
			return false;
		}
		string slotFileName = GetSlotFileName(slotIndex);
		string text = SteamCloudManager.Instance.LoadFromCloud(slotFileName);
		if (string.IsNullOrEmpty(text))
		{
			Debug.LogWarning($"No save data found for slot {slotIndex}");
			return false;
		}
		currentSlot = JsonUtility.FromJson<SaveSlotData>(text);
		currentSlotIndex = slotIndex;
		Debug.Log($"Loaded slot {slotIndex}: {currentSlot.slotName}");
		return true;
	}

	public bool SaveCurrentSlot()
	{
		if (currentSlot == null || currentSlotIndex < 0)
		{
			Debug.LogError("No active slot to save!");
			return false;
		}
		currentSlot.LastSaveTime = DateTime.Now;
		return SaveSlot(currentSlotIndex, currentSlot);
	}

	private bool SaveSlot(int slotIndex, SaveSlotData slotData)
	{
		string slotFileName = GetSlotFileName(slotIndex);
		string jsonData = JsonUtility.ToJson(slotData, prettyPrint: true);
		return SteamCloudManager.Instance.SaveToCloud(slotFileName, jsonData);
	}

	public bool DeleteSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= 3)
		{
			return false;
		}
		string slotFileName = GetSlotFileName(slotIndex);
		if (currentSlotIndex == slotIndex)
		{
			currentSlot = null;
			currentSlotIndex = -1;
		}
		return SteamCloudManager.Instance.DeleteFromCloud(slotFileName);
	}

	public void SaveLevelProgress(int levelIndex, LevelProgressData progressData)
	{
		if (currentSlot == null)
		{
			Debug.LogError("No active slot!");
			return;
		}
		LevelSaveData levelSaveData = currentSlot.levelProgress.FirstOrDefault((LevelSaveData l) => l.levelIndex == levelIndex);
		if (levelSaveData == null)
		{
			levelSaveData = new LevelSaveData
			{
				levelIndex = levelIndex
			};
			currentSlot.levelProgress.Add(levelSaveData);
		}
		levelSaveData.isCompleted = true;
		levelSaveData.coinEnemiesKilled = levelSaveData.coinEnemiesKilled || progressData.AllEnemiesDefeated;
		levelSaveData.coinTimerRequirement = levelSaveData.coinTimerRequirement || progressData.TimeRequirementMet;
		levelSaveData.coinBonusFound = levelSaveData.coinBonusFound || progressData.secretCoinFound;
		if (levelSaveData.bestTime == 0f || progressData.timeElapsed < levelSaveData.bestTime)
		{
			levelSaveData.bestTime = progressData.timeElapsed;
		}
		if (progressData.enemiesKilled > levelSaveData.highestEnemiesKilled)
		{
			levelSaveData.highestEnemiesKilled = progressData.enemiesKilled;
		}
		SaveCurrentSlot();
		AchievementManager.Instance?.EvaluateAll(currentSlot, levelIndex);
	}

	public LevelSaveData GetLevelProgress(int levelIndex)
	{
		return currentSlot?.levelProgress.FirstOrDefault((LevelSaveData l) => l.levelIndex == levelIndex);
	}

	public void UnlockAchievement(string achievementId)
	{
		if (currentSlot != null && !currentSlot.unlockedAchievements.Contains(achievementId))
		{
			currentSlot.unlockedAchievements.Add(achievementId);
			SteamCloudManager.Instance.UnlockAchievement(achievementId);
			SaveCurrentSlot();
		}
	}

	public SaveSlotInfo[] GetAllSlotInfo()
	{
		SaveSlotInfo[] array = new SaveSlotInfo[3];
		for (int i = 0; i < 3; i++)
		{
			array[i] = GetSlotInfo(i);
		}
		return array;
	}

	public SaveSlotInfo GetSlotInfo(int slotIndex)
	{
		string slotFileName = GetSlotFileName(slotIndex);
		if (!SteamCloudManager.Instance.FileExistsInCloud(slotFileName))
		{
			return new SaveSlotInfo
			{
				slotIndex = slotIndex,
				isEmpty = true
			};
		}
		SaveSlotData saveSlotData = JsonUtility.FromJson<SaveSlotData>(SteamCloudManager.Instance.LoadFromCloud(slotFileName));
		return new SaveSlotInfo
		{
			slotIndex = slotIndex,
			isEmpty = false,
			slotName = saveSlotData.slotName,
			lastSaveTime = saveSlotData.LastSaveTime,
			completedLevels = saveSlotData.levelProgress.Count((LevelSaveData l) => l.isCompleted),
			totalCoins = saveSlotData.levelProgress.Sum((LevelSaveData l) => l.GetTotalCoins()),
			totalPlayTime = saveSlotData.totalPlayTime
		};
	}

	public SaveSlotData GetCurrentSlot()
	{
		return currentSlot;
	}

	public int GetCurrentSlotIndex()
	{
		return currentSlotIndex;
	}

	[ContextMenu("Reset All Save Slots")]
	private void ResetAllSaveSlots()
	{
		for (int i = 0; i < 3; i++)
		{
			DeleteSlot(i);
		}
		currentSlot = null;
		currentSlotIndex = -1;
		Debug.Log("[SAVE] All save slots deleted.");
	}

	private string GetSlotFileName(int slotIndex)
	{
		return string.Format("{0}{1}{2}", "SaveSlot_", slotIndex, ".sav");
	}
}

using System;
using System.IO;
using System.Text;
using Steamworks;
using UnityEngine;

public class SteamCloudManager : MonoBehaviour
{
	private const uint YOUR_STEAM_APP_ID = 3435120u;

	private bool steamInitialized;

	private const int MAX_CLOUD_FILE_SIZE = 10485760;

	public static SteamCloudManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		InitializeSteam();
	}

	private void InitializeSteam()
	{
		try
		{
			if (SteamAPI.RestartAppIfNecessary(new AppId_t(3435120u)))
			{
				Application.Quit();
				return;
			}
			steamInitialized = SteamAPI.Init();
			if (!steamInitialized)
			{
				Debug.LogError("Steam API initialization failed!");
				return;
			}
			Debug.Log("Steam initialized for user: " + SteamFriends.GetPersonaName());
			if (!SteamRemoteStorage.IsCloudEnabledForAccount())
			{
				Debug.LogWarning("Steam Cloud is disabled for this account!");
			}
			if (!SteamRemoteStorage.IsCloudEnabledForApp())
			{
				Debug.LogWarning("Steam Cloud is disabled for this app!");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Steam initialization exception: " + ex.Message);
			steamInitialized = false;
		}
	}

	private void Update()
	{
		if (steamInitialized)
		{
			SteamAPI.RunCallbacks();
		}
	}

	private void OnDestroy()
	{
		if (steamInitialized)
		{
			SteamAPI.Shutdown();
		}
	}

	public bool SaveToCloud(string fileName, string jsonData)
	{
		if (!steamInitialized)
		{
			Debug.LogWarning("Steam not initialized. Saving locally only.");
			return SaveLocally(fileName, jsonData);
		}
		byte[] bytes = Encoding.UTF8.GetBytes(jsonData);
		if (bytes.Length > 10485760)
		{
			Debug.LogError($"File size ({bytes.Length}) exceeds Steam Cloud limit!");
			return false;
		}
		bool num = SteamRemoteStorage.FileWrite(fileName, bytes, bytes.Length);
		if (num)
		{
			Debug.Log($"Saved {fileName} to Steam Cloud ({bytes.Length} bytes)");
			SaveLocally(fileName, jsonData);
			return num;
		}
		Debug.LogError("Failed to save " + fileName + " to Steam Cloud");
		return num;
	}

	public string LoadFromCloud(string fileName)
	{
		if (!steamInitialized)
		{
			Debug.LogWarning("Steam not initialized. Loading locally only.");
			return LoadLocally(fileName);
		}
		if (!SteamRemoteStorage.FileExists(fileName))
		{
			Debug.Log(fileName + " not found in Steam Cloud. Trying local.");
			return LoadLocally(fileName);
		}
		int fileSize = SteamRemoteStorage.GetFileSize(fileName);
		byte[] array = new byte[fileSize];
		if (SteamRemoteStorage.FileRead(fileName, array, fileSize) != fileSize)
		{
			Debug.LogError("Failed to read complete file from Steam Cloud. Trying local.");
			return LoadLocally(fileName);
		}
		string text = Encoding.UTF8.GetString(array);
		SaveLocally(fileName, text);
		return text;
	}

	public bool DeleteFromCloud(string fileName)
	{
		if (!steamInitialized)
		{
			return DeleteLocally(fileName);
		}
		bool num = SteamRemoteStorage.FileDelete(fileName);
		bool flag = DeleteLocally(fileName);
		return num & flag;
	}

	public bool FileExistsInCloud(string fileName)
	{
		if (!steamInitialized)
		{
			return FileExistsLocally(fileName);
		}
		return SteamRemoteStorage.FileExists(fileName);
	}

	private bool SaveLocally(string fileName, string jsonData)
	{
		string path = Path.Combine(Application.persistentDataPath, fileName);
		try
		{
			File.WriteAllText(path, jsonData);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to save locally: " + ex.Message);
			return false;
		}
	}

	private string LoadLocally(string fileName)
	{
		string path = Path.Combine(Application.persistentDataPath, fileName);
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			return File.ReadAllText(path);
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to load locally: " + ex.Message);
			return null;
		}
	}

	private bool DeleteLocally(string fileName)
	{
		string path = Path.Combine(Application.persistentDataPath, fileName);
		if (!File.Exists(path))
		{
			return true;
		}
		try
		{
			File.Delete(path);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to delete locally: " + ex.Message);
			return false;
		}
	}

	private bool FileExistsLocally(string fileName)
	{
		return File.Exists(Path.Combine(Application.persistentDataPath, fileName));
	}

	public void UnlockAchievement(string achievementId)
	{
		if (!steamInitialized)
		{
			Debug.LogWarning("Steam not initialized. Cannot unlock achievement.");
		}
		else if (SteamUserStats.SetAchievement(achievementId))
		{
			SteamUserStats.StoreStats();
			Debug.Log("Achievement unlocked: " + achievementId);
		}
	}

	public bool IsAchievementUnlocked(string achievementId)
	{
		if (!steamInitialized)
		{
			return false;
		}
		SteamUserStats.GetAchievement(achievementId, out var pbAchieved);
		return pbAchieved;
	}

	[ContextMenu("Reset All Achievements")]
	private void ResetAllAchievements()
	{
		if (!steamInitialized)
		{
			Debug.LogWarning("[Steam] Cannot reset achievements — Steam not initialized.");
			return;
		}
		SteamUserStats.ResetAllStats(bAchievementsToo: true);
		SteamUserStats.StoreStats();
		Debug.Log("[Steam] All achievements and stats reset.");
	}
}

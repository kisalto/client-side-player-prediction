using System;
using System.Collections.Generic;

[Serializable]
public class SaveSlotData
{
	public string slotName;

	public string lastSaveTimeISO;

	public int totalPlayTime;

	public List<LevelSaveData> levelProgress = new List<LevelSaveData>();

	public List<string> unlockedAchievements = new List<string>();

	public DateTime LastSaveTime
	{
		get
		{
			if (!DateTime.TryParse(lastSaveTimeISO, out var result))
			{
				return DateTime.MinValue;
			}
			return result;
		}
		set
		{
			lastSaveTimeISO = value.ToString("o");
		}
	}

	public SaveSlotData(string name)
	{
		slotName = name;
		LastSaveTime = DateTime.Now;
		totalPlayTime = 0;
	}
}

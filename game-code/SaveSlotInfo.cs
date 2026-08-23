using System;

[Serializable]
public class SaveSlotInfo
{
	public int slotIndex;

	public bool isEmpty;

	public string slotName;

	public DateTime lastSaveTime;

	public int completedLevels;

	public int totalCoins;

	public int totalPlayTime;
}

using System;

[Serializable]
public class LevelSaveData
{
	public int levelIndex;

	public bool isCompleted;

	public bool coinEnemiesKilled;

	public bool coinTimerRequirement;

	public bool coinBonusFound;

	public float bestTime;

	public int highestEnemiesKilled;

	public int GetTotalCoins()
	{
		int num = 0;
		if (coinEnemiesKilled)
		{
			num++;
		}
		if (coinTimerRequirement)
		{
			num++;
		}
		if (coinBonusFound)
		{
			num++;
		}
		return num;
	}
}

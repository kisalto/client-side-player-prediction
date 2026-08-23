using System;

[Serializable]
public class LevelProgressData
{
	public int enemiesKilled;

	public float timeElapsed;

	public bool secretCoinFound;

	public int totalEnemies;

	public float targetTime;

	public bool AllEnemiesDefeated => enemiesKilled >= totalEnemies;

	public bool TimeRequirementMet => timeElapsed <= targetTime;

	public int GetCoinsEarned()
	{
		int num = 0;
		if (AllEnemiesDefeated)
		{
			num++;
		}
		if (TimeRequirementMet)
		{
			num++;
		}
		if (secretCoinFound)
		{
			num++;
		}
		return num;
	}
}

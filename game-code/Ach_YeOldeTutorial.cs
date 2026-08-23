using UnityEngine;

[CreateAssetMenu(fileName = "Ach_YeOldeTutorial", menuName = "Achievements/Ye Olde Tutorial")]
public class Ach_YeOldeTutorial : AchievementDefinition
{
	private const int TargetLevelIndex = 0;

	public override bool Evaluate(SaveSlotData slot, int completedLevelIndex)
	{
		if (completedLevelIndex != 0)
		{
			return false;
		}
		return slot.levelProgress.Find((LevelSaveData l) => l.levelIndex == 0)?.isCompleted ?? false;
	}
}

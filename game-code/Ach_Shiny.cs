using UnityEngine;

[CreateAssetMenu(fileName = "Ach_Shiny", menuName = "Achievements/Ooh Shiny")]
public class Ach_Shiny : AchievementDefinition
{
	public override bool Evaluate(SaveSlotData slot, int completedLevelIndex)
	{
		return slot.levelProgress.Exists((LevelSaveData l) => l.coinBonusFound);
	}
}

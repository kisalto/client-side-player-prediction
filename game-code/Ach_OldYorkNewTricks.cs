using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Ach_OldYorkNewTricks", menuName = "Achievements/Old York New Tricks")]
public class Ach_OldYorkNewTricks : AchievementDefinition
{
	[Tooltip("Level indices that make up Old York. Matches Chapter1_Lv1–5 (indices 0–4).")]
	[SerializeField]
	private List<int> oldYorkLevelIndices = new List<int> { 0, 1, 2, 3, 4 };

	public override bool Evaluate(SaveSlotData slot, int completedLevelIndex)
	{
		foreach (int index in oldYorkLevelIndices)
		{
			LevelSaveData levelSaveData = slot.levelProgress.Find((LevelSaveData l) => l.levelIndex == index);
			if (levelSaveData == null || !levelSaveData.isCompleted)
			{
				return false;
			}
		}
		return true;
	}
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AchievementRegistry", menuName = "Achievements/Registry")]
public class AchievementRegistry : ScriptableObject
{
	[SerializeField]
	private List<AchievementDefinition> definitions = new List<AchievementDefinition>();

	public IReadOnlyList<AchievementDefinition> Definitions => definitions;
}

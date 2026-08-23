using UnityEngine;

[CreateAssetMenu(fileName = "LevelStats", menuName = "Game/Level Stats")]
public class LevelStats : ScriptableObject
{
	[Header("Level Information")]
	public string levelName;

	public int levelIndex;

	[Header("Target Requirements")]
	public int totalEnemies;

	public float targetTime;

	public bool hasSecretCoin = true;

	[Header("Scene Reference")]
	public string nextSceneName;
}

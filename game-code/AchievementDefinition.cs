using UnityEngine;

public abstract class AchievementDefinition : ScriptableObject
{
	[Header("Steam")]
	[Tooltip("Must match the API Name set in the Steamworks partner dashboard exactly.")]
	[SerializeField]
	private string apiName;

	[Header("Display")]
	[SerializeField]
	private string displayName;

	[SerializeField]
	[TextArea]
	private string description;

	public string ApiName => apiName;

	public string DisplayName => displayName;

	public string Description => description;

	public abstract bool Evaluate(SaveSlotData slot, int completedLevelIndex);
}

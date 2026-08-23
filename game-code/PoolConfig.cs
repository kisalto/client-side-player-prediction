using UnityEngine;

[CreateAssetMenu(fileName = "New Pool Config", menuName = "Pooling/Pool Configuration")]
public class PoolConfig : ScriptableObject
{
	[Header("Pool Identification")]
	[Tooltip("Unique name for this pool. Use this tag when spawning objects.")]
	public string poolTag;

	[Header("Pool Settings")]
	[Tooltip("The prefab to pool (the original object we'll make copies of)")]
	public GameObject prefab;

	[Tooltip("How many objects to create at start")]
	[Range(1f, 100f)]
	public int initialSize = 10;

	[Header("Optional Settings")]
	[Tooltip("Should this pool automatically grow if we run out of objects?")]
	public bool canGrow = true;

	[Tooltip("Maximum size this pool can grow to (0 = unlimited)")]
	public int maxSize;
}

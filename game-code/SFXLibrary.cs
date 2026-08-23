using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Audio/SFX Library")]
public class SFXLibrary : ScriptableObject
{
	[SerializeField]
	private List<SFXEntry> entries = new List<SFXEntry>();

	private Dictionary<string, SFXEntry> lookup;

	public void Initialize()
	{
		lookup = new Dictionary<string, SFXEntry>(entries.Count);
		foreach (SFXEntry entry in entries)
		{
			if (!string.IsNullOrEmpty(entry.id) && !lookup.TryAdd(entry.id, entry))
			{
				Debug.LogWarning("[SFXLibrary] Duplicate SFX key '" + entry.id + "' found. Skipping duplicate.");
			}
		}
	}

	public SFXEntry Get(string id)
	{
		if (lookup == null)
		{
			Initialize();
		}
		if (lookup.TryGetValue(id, out var value))
		{
			return value;
		}
		return null;
	}
}

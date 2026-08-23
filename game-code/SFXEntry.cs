using System;
using UnityEngine;

[Serializable]
public class SFXEntry
{
	[Tooltip("Unique key matching a constant in SFXKeys.")]
	public string id;

	[Tooltip("The audio clip to play. Leave empty to skip playback gracefully.")]
	public AudioClip clip;

	[Range(0f, 1f)]
	[Tooltip("Base volume for this sound.")]
	public float volume = 1f;

	[Tooltip("Min random pitch variation.")]
	public float minPitch = 1f;

	[Tooltip("Max random pitch variation.")]
	public float maxPitch = 1f;

	[Range(0f, 1f)]
	[Tooltip("0 = fully 2D (UI/dialogue). 1 = fully 3D spatial.")]
	public float spatialBlend = 1f;

	[Tooltip("Max audible distance for 3D sounds.")]
	public float maxDistance = 25f;

	[Tooltip("If true, the sound loops until explicitly stopped.")]
	public bool loop;
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelCompleteBlurEffect : MonoBehaviour
{
	[Header("Volume Settings")]
	[SerializeField]
	private Volume blurVolume;

	[SerializeField]
	private float blurDuration = 1.5f;

	[SerializeField]
	private AnimationCurve blurCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private void Awake()
	{
		if (blurVolume == null)
		{
			blurVolume = Object.FindObjectOfType<Volume>();
		}
		if (blurVolume != null)
		{
			blurVolume.weight = 0f;
		}
	}

	public IEnumerator BlurScreen()
	{
		if (!(blurVolume == null))
		{
			float elapsed = 0f;
			while (elapsed < blurDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float time = elapsed / blurDuration;
				blurVolume.weight = blurCurve.Evaluate(time);
				yield return null;
			}
			blurVolume.weight = 1f;
		}
	}

	public IEnumerator UnblurScreen()
	{
		if (!(blurVolume == null))
		{
			float elapsed = 0f;
			while (elapsed < blurDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float time = elapsed / blurDuration;
				blurVolume.weight = 1f - blurCurve.Evaluate(time);
				yield return null;
			}
			blurVolume.weight = 0f;
		}
	}

	private void OnDestroy()
	{
		if (blurVolume != null)
		{
			blurVolume.weight = 0f;
		}
	}
}

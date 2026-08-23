using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CoinRewardDisplay : MonoBehaviour
{
	[Header("Coin Images")]
	[SerializeField]
	private Image[] coinImages;

	[Header("Sprites")]
	[SerializeField]
	private Sprite coinTransparentSprite;

	[SerializeField]
	private Sprite coinFullSprite;

	[Header("Animation Settings")]
	[SerializeField]
	private float delayBetweenCoins = 0.5f;

	[SerializeField]
	private float coinPopDuration = 0.3f;

	[SerializeField]
	private AnimationCurve coinPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private void Start()
	{
		InitializeCoins();
	}

	private void InitializeCoins()
	{
		Image[] array = coinImages;
		foreach (Image image in array)
		{
			if (image != null)
			{
				image.sprite = coinTransparentSprite;
				image.color = new Color(1f, 1f, 1f, 0.3f);
			}
		}
	}

	public IEnumerator AnimateCoins(bool[] coinStates)
	{
		for (int i = 0; i < coinImages.Length && i < coinStates.Length; i++)
		{
			if (coinStates[i])
			{
				yield return StartCoroutine(PopCoin(i));
				yield return new WaitForSecondsRealtime(delayBetweenCoins);
			}
			else
			{
				yield return new WaitForSecondsRealtime(delayBetweenCoins);
			}
		}
	}

	private IEnumerator PopCoin(int index)
	{
		if (index < 0 || index >= coinImages.Length)
		{
			yield break;
		}
		Image coinImage = coinImages[index];
		if (!(coinImage == null))
		{
			coinImage.sprite = coinFullSprite;
			Vector3 originalScale = coinImage.transform.localScale;
			float elapsed = 0f;
			while (elapsed < coinPopDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float num = elapsed / coinPopDuration;
				float num2 = coinPopCurve.Evaluate(num);
				coinImage.transform.localScale = originalScale * (1f + num2 * 0.3f);
				coinImage.color = Color.Lerp(new Color(1f, 1f, 1f, 0.3f), Color.white, num);
				yield return null;
			}
			coinImage.transform.localScale = originalScale;
			coinImage.color = Color.white;
		}
	}
}

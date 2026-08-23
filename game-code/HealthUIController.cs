using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : MonoBehaviour
{
	[Header("Heart Sprites")]
	[SerializeField]
	private Sprite fullHeartSprite;

	[SerializeField]
	private Sprite emptyHeartSprite;

	[Header("Heart Images")]
	[SerializeField]
	private Image[] heartImages;

	[Header("Player Reference")]
	[SerializeField]
	private PlayerHealthController playerHealth;

	[Header("Animation Settings")]
	[SerializeField]
	private float animationDuration = 0.3f;

	[SerializeField]
	private float scaleMultiplier = 1.3f;

	private int previousHealth;

	private void Start()
	{
		if (playerHealth != null)
		{
			previousHealth = playerHealth.CurrentHealth;
			playerHealth.OnHealthChanged += UpdateHearts;
			UpdateHearts();
		}
	}

	private void OnDestroy()
	{
		if (playerHealth != null)
		{
			playerHealth.OnHealthChanged -= UpdateHearts;
		}
	}

	private void UpdateHearts()
	{
		int currentHealth = playerHealth.CurrentHealth;
		if (currentHealth < previousHealth)
		{
			int num = currentHealth;
			if (num >= 0 && num < heartImages.Length)
			{
				StartCoroutine(HeartDamageAnimation(heartImages[num].transform));
			}
		}
		else if (currentHealth > previousHealth)
		{
			int num2 = currentHealth - 1;
			if (num2 >= 0 && num2 < heartImages.Length)
			{
				StartCoroutine(HeartHealAnimation(heartImages[num2].transform));
			}
		}
		for (int i = 0; i < heartImages.Length; i++)
		{
			if (i < currentHealth)
			{
				heartImages[i].sprite = fullHeartSprite;
			}
			else
			{
				heartImages[i].sprite = emptyHeartSprite;
			}
		}
		previousHealth = currentHealth;
	}

	private IEnumerator HeartDamageAnimation(Transform heartTransform)
	{
		Vector3 originalScale = heartTransform.localScale;
		float elapsed = 0f;
		while (elapsed < animationDuration)
		{
			elapsed += Time.deltaTime;
			float num = elapsed / animationDuration;
			float num2 = 1f + Mathf.Sin(num * MathF.PI) * (scaleMultiplier - 1f);
			heartTransform.localScale = originalScale * num2;
			yield return null;
		}
		heartTransform.localScale = originalScale;
	}

	private IEnumerator HeartHealAnimation(Transform heartTransform)
	{
		Vector3 originalScale = heartTransform.localScale;
		float elapsed = 0f;
		while (elapsed < animationDuration)
		{
			elapsed += Time.deltaTime;
			float num = elapsed / animationDuration;
			float num2 = 1f + Mathf.Sin(num * MathF.PI) * 0.2f;
			heartTransform.localScale = originalScale * num2;
			yield return null;
		}
		heartTransform.localScale = originalScale;
	}
}

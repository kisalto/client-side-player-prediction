using System.Collections;
using UnityEngine;

public class AfterImageEffect : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float spawnInterval = 0.05f;

	[SerializeField]
	private GameObject afterImagePrefab;

	private SpriteRenderer playerSpriteRenderer;

	private Coroutine afterImageCoroutine;

	private bool isActive;

	private void Awake()
	{
		playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
	}

	public void StartAfterImages()
	{
		if (!isActive)
		{
			isActive = true;
			if (afterImageCoroutine != null)
			{
				StopCoroutine(afterImageCoroutine);
			}
			afterImageCoroutine = StartCoroutine(SpawnAfterImages());
		}
	}

	public void StopAfterImages()
	{
		isActive = false;
		if (afterImageCoroutine != null)
		{
			StopCoroutine(afterImageCoroutine);
			afterImageCoroutine = null;
		}
	}

	private IEnumerator SpawnAfterImages()
	{
		while (isActive)
		{
			SpawnAfterImage();
			yield return new WaitForSeconds(spawnInterval);
		}
	}

	private void SpawnAfterImage()
	{
		if (!(afterImagePrefab == null) && !(playerSpriteRenderer == null))
		{
			AfterImage component = Object.Instantiate(afterImagePrefab, base.transform.position, Quaternion.identity).GetComponent<AfterImage>();
			if (component != null)
			{
				component.Initialize(playerSpriteRenderer.sprite, base.transform.position, base.transform.localScale, playerSpriteRenderer.sortingOrder);
			}
		}
	}

	private void OnDestroy()
	{
		StopAfterImages();
	}
}

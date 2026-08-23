using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockDeathTransition : MonoBehaviour
{
	private const float CoverageMargin = 1.3f;

	[Header("Block Settings")]
	[SerializeField]
	private int gridWidth = 40;

	[SerializeField]
	private int gridHeight = 30;

	[SerializeField]
	private float blockOverlapMultiplier = 1.05f;

	[SerializeField]
	private Color blockColor = Color.black;

	[SerializeField]
	private string sortingLayerName = "DeathOverlay";

	[SerializeField]
	private int sortingOrder;

	[Header("Animation")]
	[SerializeField]
	private float transitionDuration = 2f;

	private List<SpriteRenderer> blockPool = new List<SpriteRenderer>();

	private List<Vector2Int> snakePath = new List<Vector2Int>();

	private GameObject blocksContainer;

	private Camera mainCamera;

	private bool isPlaying;

	private Sprite blockSprite;

	public bool IsPlaying => isPlaying;

	public float TransitionDuration => transitionDuration;

	private void Awake()
	{
		mainCamera = Camera.main;
		CreateBlockSprite();
		CreateBlockPool();
		GenerateSnakePath();
	}

	private void CreateBlockSprite()
	{
		Texture2D texture2D = new Texture2D(32, 32);
		Color[] array = new Color[1024];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Color.white;
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		texture2D.filterMode = FilterMode.Point;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		blockSprite = Sprite.Create(texture2D, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f, 0u, SpriteMeshType.FullRect);
	}

	private void CreateBlockPool()
	{
		blocksContainer = new GameObject("DeathBlocks");
		blocksContainer.transform.SetParent(base.transform);
		int num = gridWidth * gridHeight;
		for (int i = 0; i < num; i++)
		{
			GameObject obj = new GameObject($"Block_{i}");
			obj.transform.SetParent(blocksContainer.transform);
			SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = blockSprite;
			spriteRenderer.color = blockColor;
			spriteRenderer.sortingLayerName = sortingLayerName;
			spriteRenderer.sortingOrder = sortingOrder;
			spriteRenderer.enabled = false;
			spriteRenderer.drawMode = SpriteDrawMode.Simple;
			blockPool.Add(spriteRenderer);
		}
		blocksContainer.SetActive(value: false);
	}

	private void GenerateSnakePath()
	{
		bool[,] array = new bool[gridWidth, gridHeight];
		snakePath.Clear();
		int num = 0;
		int num2 = gridWidth * gridHeight;
		while (num2 > 0)
		{
			List<Vector2Int> list = new List<Vector2Int>();
			for (int i = num; i < gridWidth - num; i++)
			{
				if (!array[i, num])
				{
					list.Add(new Vector2Int(i, num));
					array[i, num] = true;
					num2--;
				}
			}
			for (int j = num + 1; j < gridHeight - num; j++)
			{
				if (!array[gridWidth - 1 - num, j])
				{
					list.Add(new Vector2Int(gridWidth - 1 - num, j));
					array[gridWidth - 1 - num, j] = true;
					num2--;
				}
			}
			if (gridHeight - 1 - num > num)
			{
				for (int num3 = gridWidth - 2 - num; num3 >= num; num3--)
				{
					if (!array[num3, gridHeight - 1 - num])
					{
						list.Add(new Vector2Int(num3, gridHeight - 1 - num));
						array[num3, gridHeight - 1 - num] = true;
						num2--;
					}
				}
			}
			if (gridWidth - 1 - num > num)
			{
				for (int num4 = gridHeight - 2 - num; num4 > num; num4--)
				{
					if (!array[num, num4])
					{
						list.Add(new Vector2Int(num, num4));
						array[num, num4] = true;
						num2--;
					}
				}
			}
			snakePath.AddRange(list);
			num++;
		}
	}

	public IEnumerator PlayTransition(Vector3 centerPosition, float duration = -1f)
	{
		if (!isPlaying)
		{
			float duration2 = ((duration > 0f) ? duration : transitionDuration);
			yield return PlayTransitionCoroutine(centerPosition, duration2);
		}
	}

	private IEnumerator PlayTransitionCoroutine(Vector3 centerPosition, float duration)
	{
		isPlaying = true;
		blocksContainer.SetActive(value: true);
		foreach (SpriteRenderer item in blockPool)
		{
			item.enabled = false;
		}
		Vector3 position = mainCamera.transform.position;
		float num = mainCamera.orthographicSize * 2f;
		float num2 = num * mainCamera.aspect;
		float num3 = Mathf.Abs(centerPosition.x - position.x);
		float num4 = Mathf.Abs(centerPosition.y - position.y);
		float num5 = num2 * 1.3f + num3 * 2f;
		float num6 = num * 1.3f + num4 * 2f;
		Vector3 bottomLeft = centerPosition - new Vector3(num5 / 2f, num6 / 2f, 0f);
		float cellWidth = num5 / (float)gridWidth;
		float cellHeight = num6 / (float)gridHeight;
		float blockScaleX = cellWidth * blockOverlapMultiplier;
		float blockScaleY = cellHeight * blockOverlapMultiplier;
		int totalBlocks = snakePath.Count;
		float elapsed = 0f;
		int lastBlockIndex = -1;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			int num7 = Mathf.FloorToInt(Mathf.Clamp01(elapsed / duration) * (float)totalBlocks);
			for (int i = lastBlockIndex + 1; i <= num7 && i < totalBlocks && i < blockPool.Count; i++)
			{
				Vector2Int vector2Int = snakePath[i];
				SpriteRenderer spriteRenderer = blockPool[i];
				Vector3 position2 = bottomLeft + new Vector3((float)vector2Int.x * cellWidth + cellWidth / 2f, (float)vector2Int.y * cellHeight + cellHeight / 2f, 1f);
				spriteRenderer.transform.position = position2;
				spriteRenderer.transform.localScale = new Vector3(blockScaleX, blockScaleY, 1f);
				spriteRenderer.enabled = true;
				lastBlockIndex = i;
			}
			yield return null;
		}
		for (int j = lastBlockIndex + 1; j < totalBlocks && j < blockPool.Count; j++)
		{
			Vector2Int vector2Int2 = snakePath[j];
			SpriteRenderer spriteRenderer2 = blockPool[j];
			Vector3 position3 = bottomLeft + new Vector3((float)vector2Int2.x * cellWidth + cellWidth / 2f, (float)vector2Int2.y * cellHeight + cellHeight / 2f, 1f);
			spriteRenderer2.transform.position = position3;
			spriteRenderer2.transform.localScale = new Vector3(blockScaleX, blockScaleY, 1f);
			spriteRenderer2.enabled = true;
		}
		isPlaying = false;
	}

	public void ResetTransition()
	{
		if (blocksContainer != null)
		{
			blocksContainer.SetActive(value: false);
		}
		foreach (SpriteRenderer item in blockPool)
		{
			item.enabled = false;
		}
		isPlaying = false;
	}

	private void OnDestroy()
	{
		if (blocksContainer != null)
		{
			Object.Destroy(blocksContainer);
		}
		if (blockSprite != null && blockSprite.texture != null)
		{
			Object.Destroy(blockSprite.texture);
		}
	}
}

using UnityEngine;

public class AfterImage : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float lifetime = 0.5f;

	[SerializeField]
	private float fadeSpeed = 2f;

	[SerializeField]
	private Color tintColor = new Color(1f, 1f, 1f, 0.7f);

	private SpriteRenderer spriteRenderer;

	private float timer;

	private Color currentColor;

	private void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
	}

	public void Initialize(Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder)
	{
		spriteRenderer.sprite = sprite;
		base.transform.position = position;
		base.transform.localScale = scale;
		spriteRenderer.sortingOrder = sortingOrder - 1;
		currentColor = tintColor;
		spriteRenderer.color = currentColor;
		timer = 0f;
	}

	private void Update()
	{
		timer += Time.deltaTime;
		currentColor.a = Mathf.Lerp(tintColor.a, 0f, timer / lifetime * fadeSpeed);
		spriteRenderer.color = currentColor;
		if (timer >= lifetime)
		{
			Object.Destroy(base.gameObject);
		}
	}
}

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BulletOutlineController : MonoBehaviour
{
	private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

	[Header("Outline Colors")]
	[SerializeField]
	private Color attackingColor = Color.red;

	[SerializeField]
	private Color parriedColor = Color.green;

	private SpriteRenderer spriteRenderer;

	private MaterialPropertyBlock propertyBlock;

	private void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		propertyBlock = new MaterialPropertyBlock();
	}

	public void SetAttacking()
	{
		SetOutlineColor(attackingColor);
	}

	public void SetParried()
	{
		SetOutlineColor(parriedColor);
	}

	private void SetOutlineColor(Color color)
	{
		spriteRenderer.GetPropertyBlock(propertyBlock);
		propertyBlock.SetColor(OutlineColorId, color);
		spriteRenderer.SetPropertyBlock(propertyBlock);
	}
}

using UnityEngine;

public class EnemyTopBounce : MonoBehaviour
{
	[Header("Bounce Settings")]
	[SerializeField]
	private float horizontalBounceForce = 5f;

	[SerializeField]
	private float upwardBounceForce = 3f;

	[SerializeField]
	private LayerMask playerLayer;

	[Header("Detection Settings")]
	[SerializeField]
	private float topDetectionHeight = 0.5f;

	[SerializeField]
	private float detectionWidth = 0.8f;

	private BoxCollider2D enemyCollider;

	private void Awake()
	{
		enemyCollider = GetComponent<BoxCollider2D>();
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if (((1 << collision.gameObject.layer) & (int)playerLayer) != 0 && IsPlayerOnTop(collision))
		{
			BouncePlayerAway(collision);
		}
	}

	private void OnCollisionStay2D(Collision2D collision)
	{
		if (((1 << collision.gameObject.layer) & (int)playerLayer) != 0 && IsPlayerOnTop(collision))
		{
			BouncePlayerAway(collision);
		}
	}

	private bool IsPlayerOnTop(Collision2D collision)
	{
		ContactPoint2D[] contacts = collision.contacts;
		foreach (ContactPoint2D contactPoint2D in contacts)
		{
			if (contactPoint2D.normal.y < -0.5f)
			{
				return true;
			}
		}
		return false;
	}

	private void BouncePlayerAway(Collision2D collision)
	{
		SFXManager.Instance?.Play("E_TopBounce", base.transform.position);
		PlayerMovementController component = collision.gameObject.GetComponent<PlayerMovementController>();
		if (!(component == null))
		{
			Vector2 vector = collision.transform.position;
			Vector2 vector2 = base.transform.position;
			float num = Mathf.Sign(vector.x - vector2.x);
			if (num == 0f)
			{
				num = 1f;
			}
			Vector2 velocity = new Vector2(num * horizontalBounceForce, upwardBounceForce);
			component.SetVelocity(velocity);
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (enemyCollider == null)
		{
			enemyCollider = GetComponent<BoxCollider2D>();
		}
		if (!(enemyCollider == null))
		{
			Gizmos.color = Color.magenta;
			Vector3 center = base.transform.position + Vector3.up * (enemyCollider.size.y / 2f + enemyCollider.offset.y);
			Vector3 size = new Vector3(detectionWidth, topDetectionHeight, 1f);
			Gizmos.DrawWireCube(center, size);
		}
	}
}

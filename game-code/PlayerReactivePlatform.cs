using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerReactivePlatform : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField]
	private float lowerDistance = 0.1f;

	[SerializeField]
	private float moveSpeed = 5f;

	private Vector2 originalPosition;

	private Vector2 loweredPosition;

	private bool playerOnPlatform;

	private Rigidbody2D platformRigidbody;

	private Rigidbody2D playerRigidbody;

	private const float ARRIVAL_THRESHOLD = 0.01f;

	private void Awake()
	{
		platformRigidbody = GetComponent<Rigidbody2D>();
		platformRigidbody.bodyType = RigidbodyType2D.Kinematic;
		platformRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
		platformRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
		originalPosition = platformRigidbody.position;
		loweredPosition = originalPosition - new Vector2(0f, lowerDistance);
	}

	private void FixedUpdate()
	{
		Vector2 target = (playerOnPlatform ? loweredPosition : originalPosition);
		Vector2 position = platformRigidbody.position;
		Vector2 vector = Vector2.MoveTowards(position, target, moveSpeed * Time.fixedDeltaTime);
		platformRigidbody.MovePosition(vector);
		Vector2 vector2 = vector - position;
		if (playerOnPlatform && playerRigidbody != null && vector2 != Vector2.zero)
		{
			playerRigidbody.position += vector2;
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Player"))
		{
			playerRigidbody = other.GetComponent<Rigidbody2D>();
			playerOnPlatform = true;
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (other.CompareTag("Player"))
		{
			playerOnPlatform = false;
			playerRigidbody = null;
		}
	}
}

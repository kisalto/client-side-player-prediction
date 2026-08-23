using UnityEngine;

public class CameraController : MonoBehaviour
{
	[Header("Target")]
	public Transform player;

	[Header("Follow Settings")]
	public float smoothSpeed = 5f;

	public Vector2 offset = Vector2.zero;

	[Header("Dead Zone")]
	public Vector2 deadZoneSize = new Vector2(2f, 1f);

	[Header("Look Ahead")]
	public float lookAheadDistance = 2f;

	public float lookAheadSmoothing = 3f;

	[Header("Bounds")]
	public bool useBounds = true;

	public Vector2 minBounds;

	public Vector2 maxBounds;

	private Vector2 currentVelocity;

	private float currentLookAhead;

	private int lastFacingDirection = 1;

	private bool isFrozen;

	public void Freeze()
	{
		isFrozen = true;
	}

	private void LateUpdate()
	{
		if (!(player == null) && !isFrozen)
		{
			Vector3 b = CalculateTargetPosition();
			if (useBounds)
			{
				b.x = Mathf.Clamp(b.x, minBounds.x, maxBounds.x);
				b.y = Mathf.Clamp(b.y, minBounds.y, maxBounds.y);
			}
			b.z = base.transform.position.z;
			base.transform.position = Vector3.Lerp(base.transform.position, b, smoothSpeed * Time.deltaTime);
		}
	}

	private Vector3 CalculateTargetPosition()
	{
		Vector3 position = player.position;
		Vector3 position2 = base.transform.position;
		Vector3 result = position + (Vector3)offset;
		float num = position2.x - deadZoneSize.x / 2f;
		float num2 = position2.x + deadZoneSize.x / 2f;
		float num3 = position2.y - deadZoneSize.y / 2f;
		float num4 = position2.y + deadZoneSize.y / 2f;
		if (position.x > num && position.x < num2)
		{
			result.x = position2.x;
		}
		if (position.y > num3 && position.y < num4)
		{
			result.y = position2.y;
		}
		int playerFacingDirection = GetPlayerFacingDirection();
		if (playerFacingDirection != 0)
		{
			lastFacingDirection = playerFacingDirection;
		}
		currentLookAhead = Mathf.Lerp(currentLookAhead, (float)lastFacingDirection * lookAheadDistance, lookAheadSmoothing * Time.deltaTime);
		result.x += currentLookAhead;
		return result;
	}

	private int GetPlayerFacingDirection()
	{
		if (player.localScale.x > 0f)
		{
			return 1;
		}
		if (player.localScale.x < 0f)
		{
			return -1;
		}
		return 0;
	}
}

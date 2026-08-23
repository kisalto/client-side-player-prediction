using UnityEngine;

public class OneWayPlatform : MonoBehaviour
{
	private PlatformEffector2D platformEffector;

	private void Awake()
	{
		platformEffector = GetComponent<PlatformEffector2D>();
	}
}

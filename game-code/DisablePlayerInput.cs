using UnityEngine;

public class DisablePlayerInput : MonoBehaviour
{
	private void Awake()
	{
		PlayerInputReader component = GetComponent<PlayerInputReader>();
		if (component != null)
		{
			component.enabled = false;
		}
	}
}

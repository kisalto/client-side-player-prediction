using Unity.Cinemachine;
using UnityEngine;

public class CinemachineBlockOffset : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private PlayerCombatController playerCombat;

	[SerializeField]
	private Transform playerTransform;

	[Header("Block Look Ahead Settings")]
	[SerializeField]
	private float blockLookAheadDistance = 2f;

	[SerializeField]
	private float transitionSpeed = 3f;

	private CinemachinePositionComposer positionComposer;

	private Vector3 defaultOffset;

	private Vector3 targetOffset;

	private bool wasBlocking;

	private void Awake()
	{
		positionComposer = GetComponent<CinemachinePositionComposer>();
		if (positionComposer != null)
		{
			defaultOffset = positionComposer.TargetOffset;
		}
	}

	private void Start()
	{
		if (playerCombat == null)
		{
			playerCombat = Object.FindFirstObjectByType<PlayerCombatController>();
		}
		if (playerTransform == null && playerCombat != null)
		{
			playerTransform = playerCombat.transform;
		}
	}

	private void LateUpdate()
	{
		if (!(positionComposer == null) && !(playerCombat == null) && !(playerTransform == null))
		{
			UpdateBlockOffset();
		}
	}

	private void UpdateBlockOffset()
	{
		if (playerCombat.IsBlocking)
		{
			int playerFacingDirection = GetPlayerFacingDirection();
			targetOffset = defaultOffset + new Vector3((float)playerFacingDirection * blockLookAheadDistance, 0f, 0f);
			wasBlocking = true;
		}
		else
		{
			targetOffset = defaultOffset;
			if (wasBlocking)
			{
				wasBlocking = false;
			}
		}
		positionComposer.TargetOffset = Vector3.Lerp(positionComposer.TargetOffset, targetOffset, transitionSpeed * Time.deltaTime);
	}

	private int GetPlayerFacingDirection()
	{
		if (playerTransform.localScale.x > 0f)
		{
			return 1;
		}
		if (playerTransform.localScale.x < 0f)
		{
			return -1;
		}
		return 1;
	}

	private void OnValidate()
	{
		if (positionComposer == null)
		{
			positionComposer = GetComponent<CinemachinePositionComposer>();
		}
		if (positionComposer != null && Application.isPlaying)
		{
			defaultOffset = positionComposer.TargetOffset;
		}
	}
}

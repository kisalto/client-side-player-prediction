using System.Collections;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
	public enum EasingType
	{
		Linear,
		EaseInOutQuad,
		EaseInOutCubic,
		EaseInOutQuart,
		EaseOutBack
	}

	[Header("Camera Settings")]
	[SerializeField]
	private Camera mainCamera;

	[SerializeField]
	private Transform mainMenuCameraPosition;

	[SerializeField]
	private Transform startCameraPosition;

	[SerializeField]
	private Transform optionsCameraPosition;

	[SerializeField]
	private float moveSpeed = 5f;

	[Header("Easing Settings")]
	[SerializeField]
	private EasingType easingType = EasingType.EaseInOutCubic;

	[Header("Save Slot")]
	[SerializeField]
	private SaveSlotMenuController saveSlotMenuController;

	[Header("Options")]
	[SerializeField]
	private OptionsMenuController optionsMenuController;

	[Header("Initial Position")]
	[SerializeField]
	private Transform initialTransform;

	[Header("Demo Mode")]
	[SerializeField]
	private string demoCutsceneSceneName = "SCENE_Cutscene";

	[SerializeField]
	private string demoStartSceneName = "SCENE_Ch1_Lv1";

	private bool isMoving;

	private void Start()
	{
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}
		if (PlayerPrefs.GetInt("ReturnToOptions", 0) == 1)
		{
			PlayerPrefs.DeleteKey("ReturnToOptions");
			SnapToOptions();
		}
	}

	private void SnapToOptions()
	{
		if (!(optionsCameraPosition == null))
		{
			mainCamera.transform.position = optionsCameraPosition.position;
			optionsMenuController?.PrepareVisibility();
			optionsMenuController?.Activate();
		}
	}

	public void OnStartButtonPressed()
	{
		CutscenePlayer.PlayThenLoad(demoCutsceneSceneName, demoStartSceneName);
	}

	public void OnOptionsButtonPressed()
	{
		if (!isMoving && optionsCameraPosition != null)
		{
			optionsMenuController?.PrepareVisibility();
			StartCoroutine(MoveCameraToPosition(optionsCameraPosition.position));
		}
	}

	public void OnBackToMainMenu()
	{
		Transform transform = ((initialTransform != null) ? initialTransform : mainMenuCameraPosition);
		if (!isMoving && transform != null)
		{
			StartCoroutine(MoveCameraToPosition(transform.position));
		}
	}

	private IEnumerator MoveCameraToPosition(Vector3 targetPosition)
	{
		isMoving = true;
		Vector3 startPosition = mainCamera.transform.position;
		float num = Vector3.Distance(startPosition, targetPosition);
		float duration = num / moveSpeed;
		float elapsedTime = 0f;
		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			float t = Mathf.Clamp01(elapsedTime / duration);
			mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, ApplyEasing(t, easingType));
			yield return null;
		}
		mainCamera.transform.position = targetPosition;
		isMoving = false;
		if (startCameraPosition != null && targetPosition == startCameraPosition.position)
		{
			saveSlotMenuController?.Activate();
		}
		else if (optionsCameraPosition != null && targetPosition == optionsCameraPosition.position)
		{
			optionsMenuController?.Activate();
		}
	}

	private float ApplyEasing(float t, EasingType type)
	{
		switch (type)
		{
		case EasingType.Linear:
			return t;
		case EasingType.EaseInOutQuad:
			if (!(t < 0.5f))
			{
				return 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
			}
			return 2f * t * t;
		case EasingType.EaseInOutCubic:
			if (!(t < 0.5f))
			{
				return 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
			}
			return 4f * t * t * t;
		case EasingType.EaseInOutQuart:
			if (!(t < 0.5f))
			{
				return 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;
			}
			return 8f * t * t * t * t;
		case EasingType.EaseOutBack:
			return 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
		default:
			return t;
		}
	}

	public bool IsMoving()
	{
		return isMoving;
	}
}

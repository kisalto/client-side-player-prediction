using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class CutscenePlayer : MonoBehaviour
{
	private const string GameSceneKey = "CutsceneTargetScene";

	private const string ReturnSceneKey = "CutsceneReturnScene";

	private const float NormalPlaybackSpeed = 1f;

	public const string HasWatchedKey = "HasWatchedCutscene";

	[Header("Video")]
	[SerializeField]
	private VideoPlayer videoPlayer;

	[Header("Scene Transition")]
	[SerializeField]
	private string fallbackGameScene = "SCENE_Ch1_Lv1";

	[Header("Skip Settings")]
	[Tooltip("Maximum playback speed reached when the skip button is held fully.")]
	[SerializeField]
	private float maxPlaybackSpeed = 8f;

	[Tooltip("How long (seconds) it takes to ramp from normal to max playback speed.")]
	[SerializeField]
	private float rampDuration = 1.5f;

	[Header("Progress Bar")]
	[Tooltip("Horizontal filled Image at the bottom of the video showing playback progress. Fill Method must be Horizontal.")]
	[SerializeField]
	private Image skipProgressImage;

	[Header("Skip Prompt UI")]
	[Tooltip("TextMeshPro label that shows the skip hint.")]
	[SerializeField]
	private TextMeshProUGUI skipPromptText;

	[SerializeField]
	private string keyboardPrompt = "Hold  E  to skip";

	[SerializeField]
	private string gamepadPrompt = "Hold  X  to skip";

	private string targetScene;

	private string returnScene;

	private bool transitioned;

	private float skipHeldTime;

	private void Awake()
	{
		targetScene = PlayerPrefs.GetString("CutsceneTargetScene", fallbackGameScene);
		returnScene = PlayerPrefs.GetString("CutsceneReturnScene", string.Empty);
		if (videoPlayer == null)
		{
			Debug.LogError("[CutscenePlayer] No VideoPlayer assigned!");
			return;
		}
		videoPlayer.loopPointReached += OnVideoFinished;
		videoPlayer.Prepare();
	}

	private IEnumerator Start()
	{
		yield return new WaitUntil(() => videoPlayer.isPrepared);
		MusicManager.Instance?.ForcePlayMain();
		videoPlayer.Play();
	}

	private void OnDestroy()
	{
		if (videoPlayer != null)
		{
			videoPlayer.loopPointReached -= OnVideoFinished;
		}
	}

	private void Update()
	{
		UpdateSkipPrompt();
		HandleSkipInput();
		UpdateProgressBar();
	}

	private void UpdateSkipPrompt()
	{
		if (!(skipPromptText == null))
		{
			skipPromptText.text = (IsControllerActive() ? gamepadPrompt : keyboardPrompt);
		}
	}

	private bool IsControllerActive()
	{
		InputDevice lastUsedDevice = GetLastUsedDevice();
		if (lastUsedDevice != null)
		{
			return lastUsedDevice is Gamepad;
		}
		if (Gamepad.current != null)
		{
			return Keyboard.current == null;
		}
		return false;
	}

	private InputDevice GetLastUsedDevice()
	{
		double num = double.MinValue;
		InputDevice result = null;
		foreach (InputDevice device in InputSystem.devices)
		{
			if (device.lastUpdateTime > num)
			{
				num = device.lastUpdateTime;
				result = device;
			}
		}
		return result;
	}

	private void UpdateProgressBar()
	{
		if (!(skipProgressImage == null) && !(videoPlayer == null) && !(videoPlayer.length <= 0.0))
		{
			skipProgressImage.fillAmount = (float)(videoPlayer.time / videoPlayer.length);
		}
	}

	private void HandleSkipInput()
	{
		if (!transitioned)
		{
			if (IsSkipHeld())
			{
				skipHeldTime += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(skipHeldTime / rampDuration);
				float playbackSpeed = Mathf.Lerp(1f, maxPlaybackSpeed, t);
				videoPlayer.playbackSpeed = playbackSpeed;
			}
			else
			{
				skipHeldTime = Mathf.Max(0f, skipHeldTime - Time.unscaledDeltaTime * 2f);
				float t2 = Mathf.Clamp01(skipHeldTime / rampDuration);
				videoPlayer.playbackSpeed = Mathf.Lerp(1f, maxPlaybackSpeed, t2);
			}
		}
	}

	private static bool IsSkipHeld()
	{
		if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
		{
			return true;
		}
		if (Gamepad.current != null && Gamepad.current.buttonWest.isPressed)
		{
			return true;
		}
		return false;
	}

	private void OnVideoFinished(VideoPlayer vp)
	{
		TransitionOut();
	}

	private void TransitionOut()
	{
		if (!transitioned)
		{
			transitioned = true;
			PlayerPrefs.SetInt("HasWatchedCutscene", 1);
			PlayerPrefs.Save();
			videoPlayer?.Stop();
			PlayerPrefs.DeleteKey("CutsceneTargetScene");
			PlayerPrefs.DeleteKey("CutsceneReturnScene");
			if (!string.IsNullOrEmpty(returnScene))
			{
				ScreenFader.Instance?.SetNextFadeInColor(Color.black);
				SceneManager.LoadScene(returnScene);
			}
			else
			{
				ScreenFader.Instance?.SetNextFadeInColor(Color.white);
				SceneManager.LoadScene(targetScene);
			}
		}
	}

	public static void PlayThenLoad(string cutsceneScene, string gameScene)
	{
		PlayerPrefs.SetString("CutsceneTargetScene", gameScene);
		PlayerPrefs.DeleteKey("CutsceneReturnScene");
		ScreenFader.Instance.LoadScene(cutsceneScene);
	}

	public static void RewatchThenReturn(string cutsceneScene, string returnScene)
	{
		PlayerPrefs.SetString("CutsceneReturnScene", returnScene);
		PlayerPrefs.DeleteKey("CutsceneTargetScene");
		ScreenFader.Instance.LoadScene(cutsceneScene);
	}

	public static bool HasBeenWatched()
	{
		return PlayerPrefs.GetInt("HasWatchedCutscene", 0) == 1;
	}
}

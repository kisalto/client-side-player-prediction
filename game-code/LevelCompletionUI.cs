using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelCompletionUI : MonoBehaviour
{
	[Header("Panel")]
	[SerializeField]
	private GameObject completionPanel;

	[SerializeField]
	private NewspaperThrowEffect throwEffect;

	[Header("Stats Text")]
	[Tooltip("Expects a single TMP field. Player value is colored, target remains grey.")]
	[SerializeField]
	private TextMeshProUGUI enemiesText;

	[SerializeField]
	private TextMeshProUGUI timeText;

	[SerializeField]
	private TextMeshProUGUI secretCoinText;

	[Header("Stat Colors")]
	[SerializeField]
	private Color metColor = new Color(0.4f, 0.9f, 0.4f);

	[SerializeField]
	private Color failedColor = new Color(0.9f, 0.3f, 0.3f);

	[Header("Coin Reward")]
	[SerializeField]
	private CoinRewardDisplay coinRewardDisplay;

	[Header("Buttons")]
	[SerializeField]
	private Button continueButton;

	[SerializeField]
	private Button levelSelectButton;

	[SerializeField]
	private Button retryButton;

	[SerializeField]
	private Button exitButton;

	[SerializeField]
	private LevelCompleteNavigationController navigationController;

	[Header("Demo Mode")]
	[SerializeField]
	private bool isDemoMode;

	[Header("Scene Names")]
	[Tooltip("Scene the Level Select button loads. Must match exactly the name in Build Settings.")]
	[SerializeField]
	private string levelSelectSceneName = "LevelSelect";

	[Tooltip("Scene the Exit button loads. Must match exactly the name in Build Settings.")]
	[SerializeField]
	private string mainMenuSceneName = "MainMenu";

	[Header("Effects")]
	[SerializeField]
	private LevelCompleteBlurEffect blurEffect;

	private Canvas thisCanvas;

	private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

	private void Awake()
	{
		isDemoMode = true;
		thisCanvas = GetComponent<Canvas>();
		if (completionPanel != null)
		{
			completionPanel.SetActive(value: false);
		}
	}

	private void Start()
	{
		if (LevelStatsManager.Instance != null)
		{
			LevelStatsManager.Instance.OnLevelComplete.AddListener(OnLevelComplete);
		}
		if (isDemoMode)
		{
			if (continueButton != null)
			{
				continueButton.gameObject.SetActive(value: false);
			}
			if (levelSelectButton != null)
			{
				levelSelectButton.gameObject.SetActive(value: false);
			}
			if (retryButton != null)
			{
				retryButton.onClick.AddListener(OnRetryClicked);
			}
			if (exitButton != null)
			{
				exitButton.onClick.AddListener(OnExitClicked);
			}
		}
		else
		{
			if (continueButton != null)
			{
				continueButton.onClick.AddListener(OnContinueClicked);
			}
			if (levelSelectButton != null)
			{
				levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
			}
			if (retryButton != null)
			{
				retryButton.onClick.AddListener(OnRetryClicked);
			}
			if (exitButton != null)
			{
				exitButton.onClick.AddListener(OnExitClicked);
			}
		}
	}

	private void OnDestroy()
	{
		if (LevelStatsManager.Instance != null)
		{
			LevelStatsManager.Instance.OnLevelComplete.RemoveListener(OnLevelComplete);
		}
	}

	private void OnLevelComplete(LevelProgressData progress)
	{
		StartCoroutine(ShowCompletionSequence(progress));
	}

	private IEnumerator ShowCompletionSequence(LevelProgressData progress)
	{
		HideAllOtherCanvases();
		if (blurEffect != null)
		{
			yield return StartCoroutine(blurEffect.BlurScreen());
		}
		PopulateStats(progress);
		if (completionPanel != null)
		{
			completionPanel.SetActive(value: true);
		}
		if (throwEffect != null)
		{
			yield return StartCoroutine(throwEffect.PlayThrowAnimation());
		}
		if (coinRewardDisplay != null)
		{
			bool flag = progress.enemiesKilled >= progress.totalEnemies;
			bool flag2 = progress.timeElapsed <= progress.targetTime;
			bool secretCoinFound = progress.secretCoinFound;
			yield return StartCoroutine(coinRewardDisplay.AnimateCoins(new bool[3] { flag, flag2, secretCoinFound }));
		}
		navigationController?.EnableInput();
	}

	private void OnContinueClicked()
	{
		string nextSceneName = LevelStatsManager.Instance.GetNextSceneName();
		if (string.IsNullOrEmpty(nextSceneName))
		{
			Debug.LogWarning("[LEVEL COMPLETE] No next scene set on LevelStats. Falling back to Level Select.");
			nextSceneName = levelSelectSceneName;
		}
		ScreenFader.Instance.LoadScene(nextSceneName);
	}

	private void OnLevelSelectClicked()
	{
		LevelSelectController.JustCompletedLevelIndex = ((LevelStatsManager.Instance != null) ? LevelStatsManager.Instance.GetLevelIndex() : (-1));
		ScreenFader.Instance.LoadScene(levelSelectSceneName);
	}

	private void OnRetryClicked()
	{
		ScreenFader.Instance.LoadScene(SceneManager.GetActiveScene().name);
	}

	private void OnExitClicked()
	{
		ScreenFader.Instance.LoadScene(mainMenuSceneName);
	}

	private void PopulateStats(LevelProgressData progress)
	{
		bool met = progress.enemiesKilled >= progress.totalEnemies;
		bool met2 = progress.timeElapsed <= progress.targetTime;
		bool secretCoinFound = progress.secretCoinFound;
		if (enemiesText != null)
		{
			string text = ColoredText($"{progress.enemiesKilled}", met);
			string text2 = $"{progress.totalEnemies}";
			enemiesText.text = text + " / " + text2;
		}
		if (timeText != null)
		{
			string text3 = ColoredText(FormatTime(progress.timeElapsed), met2);
			string text4 = FormatTime(progress.targetTime);
			timeText.text = text3 + " / " + text4;
		}
		if (secretCoinText != null)
		{
			secretCoinText.text = ColoredText(secretCoinFound ? "Found" : "Not Found", secretCoinFound);
		}
	}

	private string ColoredText(string text, bool met)
	{
		string text2 = ColorUtility.ToHtmlStringRGB(met ? metColor : failedColor);
		return "<color=#" + text2 + ">" + text + "</color>";
	}

	private void HideAllOtherCanvases()
	{
		hiddenCanvases.Clear();
		Canvas[] array = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		foreach (Canvas canvas in array)
		{
			if (!(canvas == thisCanvas) && !canvas.transform.IsChildOf(base.transform) && !(canvas.GetComponentInParent<ScreenFader>() != null) && (!(canvas.transform.parent != null) || !(canvas.transform.parent.GetComponentInParent<Canvas>() != null)) && canvas.gameObject.activeSelf)
			{
				canvas.gameObject.SetActive(value: false);
				hiddenCanvases.Add(canvas);
			}
		}
	}

	private string FormatTime(float timeInSeconds)
	{
		int num = Mathf.FloorToInt(timeInSeconds / 60f);
		int num2 = Mathf.FloorToInt(timeInSeconds % 60f);
		return $"{num:00}:{num2:00}";
	}
}

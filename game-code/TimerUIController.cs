using TMPro;
using UnityEngine;

public class TimerUIController : MonoBehaviour
{
	[Header("Timer Display")]
	[SerializeField]
	private TextMeshProUGUI timerText;

	[Header("Timer Settings")]
	[SerializeField]
	private bool countUp = true;

	[SerializeField]
	private float startTime;

	[Header("Display Format")]
	[SerializeField]
	private bool showMilliseconds = true;

	private float currentTime;

	private bool isRunning;

	private void Start()
	{
		currentTime = startTime;
		isRunning = true;
	}

	private void Update()
	{
		if (!isRunning)
		{
			return;
		}
		if (countUp)
		{
			currentTime += Time.deltaTime;
		}
		else
		{
			currentTime -= Time.deltaTime;
			if (currentTime <= 0f)
			{
				currentTime = 0f;
				isRunning = false;
			}
		}
		UpdateTimerDisplay();
	}

	private void UpdateTimerDisplay()
	{
		int num = Mathf.FloorToInt(currentTime / 60f);
		int num2 = Mathf.FloorToInt(currentTime % 60f);
		int num3 = Mathf.FloorToInt(currentTime * 100f % 100f);
		if (showMilliseconds)
		{
			timerText.text = $"{num:00}:{num2:00}:{num3:00}";
		}
		else
		{
			timerText.text = $"{num:00}:{num2:00}";
		}
	}

	public void PauseTimer()
	{
		isRunning = false;
	}

	public void ResumeTimer()
	{
		isRunning = true;
	}

	public void ResetTimer()
	{
		currentTime = startTime;
		isRunning = true;
	}

	public float GetCurrentTime()
	{
		return currentTime;
	}
}

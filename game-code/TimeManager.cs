using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
	private static TimeManager instance;

	private Coroutine slowMotionCoroutine;

	private float originalTimeScale = 1f;

	public static TimeManager Instance
	{
		get
		{
			if (instance == null)
			{
				GameObject obj = new GameObject("TimeManager");
				instance = obj.AddComponent<TimeManager>();
				Object.DontDestroyOnLoad(obj);
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
	}

	public void SlowTime(float slowFactor, float duration)
	{
		if (slowMotionCoroutine != null)
		{
			StopCoroutine(slowMotionCoroutine);
		}
		slowMotionCoroutine = StartCoroutine(SlowMotionCoroutine(slowFactor, duration));
	}

	private IEnumerator SlowMotionCoroutine(float slowFactor, float duration)
	{
		Time.timeScale = slowFactor;
		Time.fixedDeltaTime = 0.02f * slowFactor;
		yield return new WaitForSecondsRealtime(duration);
		Time.timeScale = originalTimeScale;
		Time.fixedDeltaTime = 0.02f * originalTimeScale;
	}

	public void SetTimeScale(float scale)
	{
		originalTimeScale = scale;
		Time.timeScale = scale;
		Time.fixedDeltaTime = 0.02f * scale;
	}

	public void ResetTimeScale()
	{
		Time.timeScale = originalTimeScale;
		Time.fixedDeltaTime = 0.02f * originalTimeScale;
	}

	private void OnDestroy()
	{
		ResetTimeScale();
	}
}

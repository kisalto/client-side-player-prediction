using System.Collections;
using UnityEngine;

public class HitstopManager : MonoBehaviour
{
	private static HitstopManager instance;

	public static HitstopManager Instance
	{
		get
		{
			if (instance == null)
			{
				GameObject obj = new GameObject("HitstopManager");
				instance = obj.AddComponent<HitstopManager>();
				Object.DontDestroyOnLoad(obj);
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			Object.DontDestroyOnLoad(base.gameObject);
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	public void ApplyHitstop(float duration)
	{
		StartCoroutine(HitstopRoutine(duration));
	}

	private IEnumerator HitstopRoutine(float duration)
	{
		float originalTimeScale = Time.timeScale;
		Time.timeScale = 0f;
		yield return new WaitForSecondsRealtime(duration);
		Time.timeScale = originalTimeScale;
	}
}

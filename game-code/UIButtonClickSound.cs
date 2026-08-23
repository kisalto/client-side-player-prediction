using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIButtonClickSound : MonoBehaviour
{
	private static UIButtonClickSound instance;

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		RegisterAllButtons();
	}

	public void RegisterAllButtons()
	{
		Button[] array = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (Button obj in array)
		{
			obj.onClick.RemoveListener(PlayClickSound);
			obj.onClick.AddListener(PlayClickSound);
		}
	}

	private void PlayClickSound()
	{
		SFXManager.Instance?.Play2D("UI_ButtonPress");
	}
}

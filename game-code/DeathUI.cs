using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathUI : MonoBehaviour
{
	public void OnRestartButtonClicked()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			Time.timeScale = 1f;
			ScreenFader.Instance.LoadScene(SceneManager.GetActiveScene().name);
		}
	}

	public void OnQuitButtonClicked()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			Time.timeScale = 1f;
			ScreenFader.Instance.LoadScene("LevelSelect");
		}
	}
}

using UnityEngine;

public class BossTrigger : MonoBehaviour
{
	[SerializeField]
	private BigBobBoss boss;

	[SerializeField]
	private GameObject dialoguePrefab;

	private bool hasTriggered;

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!hasTriggered && ((1 << other.gameObject.layer) & LayerMask.GetMask("Player")) != 0)
		{
			TriggerBossFight();
		}
	}

	private void TriggerBossFight()
	{
		hasTriggered = true;
		if (boss != null)
		{
			boss.OnDialogueStarted();
		}
		if (dialoguePrefab != null)
		{
			BossDialogue component = Object.Instantiate(dialoguePrefab).GetComponent<BossDialogue>();
			if (component != null)
			{
				component.Initialize(this);
			}
		}
		else
		{
			StartBoss();
		}
	}

	public void StartBoss()
	{
		if (boss != null)
		{
			MusicManager.Instance?.StartBossMusic();
			boss.StartBossFight();
		}
		Object.Destroy(base.gameObject);
	}
}

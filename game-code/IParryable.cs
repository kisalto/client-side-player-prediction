public interface IParryable
{
	bool IsAttacking { get; }

	void OnParried();

	float GetParryKnockback();
}

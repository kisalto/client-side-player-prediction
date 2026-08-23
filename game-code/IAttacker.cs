public interface IAttacker
{
	bool CanAttack { get; }

	int AttackDamage { get; }

	void Attack();
}

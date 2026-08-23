using UnityEngine;

public interface IMoveable
{
	float MoveSpeed { get; set; }

	void Move(Vector2 direction);

	void Stop();
}

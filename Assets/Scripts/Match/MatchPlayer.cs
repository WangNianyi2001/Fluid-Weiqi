using UnityEngine;
using System;

public abstract class MatchPlayer : MonoBehaviour
{
	protected Match Match { get; private set; }
	public int PlayerIndex { get; private set; }

	public abstract bool IsAlive { get; }
	public virtual bool CanReceiveLocalInput => false;
	public event Action OnMadeMove;

	public virtual void Initialize(Match match, int playerIndex)
	{
		Match = match;
		PlayerIndex = playerIndex;
	}

	public abstract void SetMoveRight(bool canMove);

	public virtual void Dispose()
	{
		if(this)
			Destroy(this);
	}

	protected void NotifyMadeMove()
	{
		OnMadeMove?.Invoke();
	}
}

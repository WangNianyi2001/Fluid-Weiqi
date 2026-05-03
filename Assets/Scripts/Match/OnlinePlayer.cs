using UnityEngine;

public enum OnlinePlayerRole
{
	RemoteToLocal,
	LocalToRemote,
}

public class OnlinePlayer : MatchPlayer
{
	bool isConnected = true;
	OnlinePlayerRole role;
	PlayerLocator playerLocator;
	bool waitingForRemoteAction;

	public override bool IsAlive => isConnected;

	public void Initialize(Match match, int playerIndex, OnlinePlayerRole role, PlayerLocator locator)
	{
		base.Initialize(match, playerIndex);
		this.role = role;
		this.playerLocator = locator;
	}

	public override void RequestMove(BoardState state)
	{
		waitingForRemoteAction = role == OnlinePlayerRole.RemoteToLocal;

		if(!isConnected && waitingForRemoteAction)
		{
			Match.ReceivePass();
			NotifyMadeMove();
			return;
		}
	}

	public override void CancelMove()
	{
		waitingForRemoteAction = false;
	}

	public void SetConnectionState(bool alive)
	{
		bool wasConnected = isConnected;
		isConnected = alive;

		if(wasConnected && !isConnected && waitingForRemoteAction)
		{
			Match.ReceivePass();
			waitingForRemoteAction = false;
			NotifyMadeMove();
		}
	}

	public bool TryHandleRemoteRequest(MatchActionRequest request)
	{
		if(role != OnlinePlayerRole.RemoteToLocal)
			return false;
		if(!waitingForRemoteAction)
			return false;
		if(!playerLocator.IsValid || playerLocator != request.playerLocator)
			return false;

		bool succeed;
		bool shouldNotify = true;
		switch(request.actionType)
		{
			case MatchActionType.Place:
				succeed = Match.ReceivePlace(request.position);
				break;
			case MatchActionType.Pass:
				Match.ReceivePass();
				succeed = true;
				break;
			case MatchActionType.Remove:
				Match.ReceiveRemove(request.position);
				succeed = true;
				shouldNotify = false;
				break;
			default:
				succeed = false;
				break;
		}

		if(succeed && shouldNotify)
		{
			waitingForRemoteAction = false;
			NotifyMadeMove();
		}

		return succeed;
	}
}
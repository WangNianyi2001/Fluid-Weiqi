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
	bool receivingLocalMove;
	MatchInput input;

	public override bool IsAlive => isConnected;
	public override bool CanReceiveLocalInput => role == OnlinePlayerRole.LocalToRemote && isConnected;

	public void Initialize(Match match, int playerIndex, OnlinePlayerRole role, PlayerLocator locator)
	{
		base.Initialize(match, playerIndex);
		this.role = role;
		this.playerLocator = locator;

		if(role == OnlinePlayerRole.LocalToRemote)
		{
			input = MatchInput.GetOrCreate(match);
			input.OnCursorEnter += OnCursorEnter;
			input.OnCursorMove += OnCursorMove;
			input.OnCursorExit += OnCursorExit;
			input.OnPlace += OnPlace;
			input.OnRemove += OnRemove;
			input.OnPass += OnPass;
		}
	}

	public override void RequestMove(BoardState state)
	{
		waitingForRemoteAction = role == OnlinePlayerRole.RemoteToLocal;
		receivingLocalMove = role == OnlinePlayerRole.LocalToRemote;

		if(receivingLocalMove)
		{
			if(!isConnected)
				Match.ReceiveCursorExit();
			return;
		}

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
		receivingLocalMove = false;
		Match.ReceiveCursorExit();
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

		if(receivingLocalMove && !alive)
			Match.ReceiveCursorExit();
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
				shouldNotify = !(Match is TrainingMatch);
				break;
			case MatchActionType.Pass:
				Match.ReceivePass();
				succeed = true;
				shouldNotify = true;
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

	protected void OnDestroy()
	{
		if(input == null)
			return;

		input.OnCursorEnter -= OnCursorEnter;
		input.OnCursorMove -= OnCursorMove;
		input.OnCursorExit -= OnCursorExit;
		input.OnPlace -= OnPlace;
		input.OnRemove -= OnRemove;
		input.OnPass -= OnPass;
	}

	void OnCursorEnter(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorEnter(position);
	}

	void OnCursorMove(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorMove(position);
	}

	void OnCursorExit()
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorExit();
	}

	void OnPlace(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Place, position))
		{
			if(!(Match is TrainingMatch))
				receivingLocalMove = false;
		}
	}

	void OnRemove(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Remove, position))
		{
			receivingLocalMove = false;
		}
	}

	void OnPass()
	{
		if(!receivingLocalMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Pass, Vector2.zero))
		{
			receivingLocalMove = false;
		}
	}
}
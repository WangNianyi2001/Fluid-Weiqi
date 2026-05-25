using UnityEngine;
using System.Collections;

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
	Coroutine autoPassCoroutine;
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
			input.OnPrimaryDown += OnPrimaryDown;
			input.OnPrimaryUp += OnPrimaryUp;
			input.OnPlace += OnPlace;
			input.OnRemove += OnRemove;
			input.OnPass += OnPass;
		}
	}

	public override void SetMoveRight(bool canMove)
	{
		if(role == OnlinePlayerRole.RemoteToLocal)
		{
			if(canMove == waitingForRemoteAction)
				return;

			waitingForRemoteAction = canMove;

			if(!canMove)
			{
				if(autoPassCoroutine != null)
				{
					StopCoroutine(autoPassCoroutine);
					autoPassCoroutine = null;
				}
				return;
			}

			if(!isConnected)
				autoPassCoroutine = StartCoroutine(AutoPassAsync());
		}
		else
		{
			if(canMove == receivingLocalMove)
				return;

			receivingLocalMove = canMove;
			if(!canMove || !isConnected)
				EndBrushStrokeSfx();

			if(!canMove || !isConnected)
				Match.ReceiveCursorExit(PlayerIndex);
		}
	}

	public override void Dispose()
	{
		if(autoPassCoroutine != null)
		{
			StopCoroutine(autoPassCoroutine);
			autoPassCoroutine = null;
		}
		base.Dispose();
	}

	public void SetConnectionState(bool alive)
	{
		bool wasConnected = isConnected;
		isConnected = alive;

		if(wasConnected && !isConnected && waitingForRemoteAction && autoPassCoroutine == null)
			autoPassCoroutine = StartCoroutine(AutoPassAsync());

		if(receivingLocalMove && !alive)
			EndBrushStrokeSfx();

		if(receivingLocalMove && !alive)
			Match.ReceiveCursorExit(PlayerIndex);
	}

	IEnumerator AutoPassAsync()
	{
		yield return null;
		autoPassCoroutine = null;
		if(!waitingForRemoteAction || Match.IsEnded)
			yield break;
		Match.ReceivePass(PlayerIndex);
		waitingForRemoteAction = false;
		NotifyMadeMove();
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
				succeed = Match.ReceivePlace(PlayerIndex, request.position, request.strength);
				shouldNotify = !(Match is TrainingMatch);
				break;
			case MatchActionType.Pass:
				Match.ReceivePass(PlayerIndex);
				succeed = true;
				shouldNotify = true;
				break;
			case MatchActionType.Remove:
				Match.ReceiveRemove(PlayerIndex, request.position);
				succeed = true;
				shouldNotify = false;
				break;
				case MatchActionType.RequestScoring:
					Match.ReceiveRequestScoring(PlayerIndex);
					succeed = true;
					shouldNotify = false;
					break;
				case MatchActionType.Resign:
					Match.ReceiveResign(PlayerIndex);
					succeed = true;
					shouldNotify = false;
					break;
			default:
				succeed = false;
				break;
		}

		if(succeed && shouldNotify)
		{
			if(!Match.UseContinuousPlacement)
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
		input.OnPrimaryDown -= OnPrimaryDown;
		input.OnPrimaryUp -= OnPrimaryUp;
		input.OnPlace -= OnPlace;
		input.OnRemove -= OnRemove;
		input.OnPass -= OnPass;
	}

	void OnPrimaryDown()
	{
		if(!receivingLocalMove || !Match.UseContinuousPlacement)
			return;
		AudioManager.Instance?.BeginBrushStroke();
	}

	void OnPrimaryUp()
	{
		if(!Match.UseContinuousPlacement)
			return;
		EndBrushStrokeSfx();
	}

	void EndBrushStrokeSfx()
	{
		AudioManager.Instance?.EndBrushStroke();
	}

	void OnCursorEnter(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorEnter(PlayerIndex, position);
	}

	void OnCursorMove(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorMove(PlayerIndex, position);
	}

	void OnCursorExit()
	{
		if(!receivingLocalMove)
			return;
		Match.ReceiveCursorExit(PlayerIndex);
	}

	void OnPlace(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		float placementStrength = Match.PlacementStrengthPerPlacement;
		if(Match.TryApplyPredictedActionAndSendRequest(PlayerIndex, MatchActionType.Place, position, placementStrength))
		{
			if(!(Match is TrainingMatch) && !Match.UseContinuousPlacement)
				receivingLocalMove = false;
		}
	}

	void OnRemove(Vector2 position)
	{
		if(!receivingLocalMove)
			return;
		if(Match.TryApplyPredictedActionAndSendRequest(PlayerIndex, MatchActionType.Remove, position))
		{
			if(!Match.UseContinuousPlacement)
				receivingLocalMove = false;
		}
	}

	void OnPass()
	{
		if(!receivingLocalMove)
			return;
		if(Match.TryApplyPredictedActionAndSendRequest(PlayerIndex, MatchActionType.Pass, Vector2.zero))
		{
			if(!Match.UseContinuousPlacement)
				receivingLocalMove = false;
		}
	}
}
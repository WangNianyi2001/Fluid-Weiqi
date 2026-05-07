using UnityEngine;

public class LocalPlayer : MatchPlayer
{
	MatchInput input;
	bool receivingMove;

	public override bool IsAlive => true;
	public override bool CanReceiveLocalInput => true;

	public override void Initialize(Match match, int playerIndex)
	{
		base.Initialize(match, playerIndex);

		input = MatchInput.GetOrCreate(match);

		input.OnCursorEnter += OnCursorEnter;
		input.OnCursorMove += OnCursorMove;
		input.OnCursorExit += OnCursorExit;
		// OnPlace/OnRemove/OnPass are subscribed only while it's this player's turn,
		// so at most one LocalPlayer is subscribed at any given time.
	}

	public override void RequestMove(BoardState state)
	{
		receivingMove = true;
		input.OnPlace += OnPlace;
		input.OnRemove += OnRemove;
		input.OnPass += OnPass;
	}

	public override void CancelMove()
	{
		receivingMove = false;
		input.OnPlace -= OnPlace;
		input.OnRemove -= OnRemove;
		input.OnPass -= OnPass;
		Match.ReceiveCursorExit();
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
		if(!receivingMove)
			return;
		Match.ReceiveCursorEnter(position);
	}

	void OnCursorMove(Vector2 position)
	{
		if(!receivingMove)
			return;
		Match.ReceiveCursorMove(position);
	}

	void OnCursorExit()
	{
		if(!receivingMove)
			return;
		Match.ReceiveCursorExit();
	}

	void OnPlace(Vector2 position)
	{
		if(!receivingMove)
			return;

		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Place, position))
		{
			CancelMove();
			return;
		}

		bool succeed = Match.ReceivePlace(position);
		if(succeed)
		{
			CancelMove();
			NotifyMadeMove();
		}
	}

	void OnRemove(Vector2 position)
	{
		if(!receivingMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Remove, position))
		{
			CancelMove();
			return;
		}
		Match.ReceiveRemove(position);
	}

	void OnPass()
	{
		if(!receivingMove)
			return;

		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Pass, Vector2.zero))
		{
			CancelMove();
			return;
		}

		CancelMove();
		Match.ReceivePass();
		NotifyMadeMove();
	}
}

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
		input.OnPlace += OnPlace;
		input.OnRemove += OnRemove;
		input.OnPass += OnPass;
	}

	public override void RequestMove(BoardState state)
	{
		receivingMove = true;
	}

	public override void CancelMove()
	{
		receivingMove = false;
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
			receivingMove = false;
			return;
		}

		bool succeed = Match.ReceivePlace(position);
		if(succeed)
			NotifyMadeMove();
	}

	void OnRemove(Vector2 position)
	{
		if(!receivingMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Remove, position))
		{
			receivingMove = false;
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
			receivingMove = false;
			return;
		}

		Match.ReceivePass();
		NotifyMadeMove();
	}
}

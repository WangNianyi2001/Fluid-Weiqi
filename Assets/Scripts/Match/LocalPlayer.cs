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
		input.OnPrimaryDown += OnPrimaryDown;
		input.OnPrimaryUp += OnPrimaryUp;
		// OnPlace/OnRemove/OnPass are subscribed only while it's this player's turn,
		// so at most one LocalPlayer is subscribed at any given time.
	}

	public override void SetMoveRight(bool canMove)
	{
		if(canMove == receivingMove)
			return;

		receivingMove = canMove;
		if(canMove)
		{
			input.OnPlace += OnPlace;
			input.OnRemove += OnRemove;
			input.OnPass += OnPass;
		}
		else
		{
			input.OnPlace -= OnPlace;
			input.OnRemove -= OnRemove;
			input.OnPass -= OnPass;
			EndBrushStrokeSfx();
			Match.ReceiveCursorExit();
		}
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
		if(!receivingMove || !Match.UseContinuousPlacement)
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

		float placementStrength = Match.PlacementStrengthPerPlacement;

		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Place, position, placementStrength))
		{
			if(!Match.UseContinuousPlacement)
				SetMoveRight(false);
			return;
		}

		bool succeed = Match.ReceivePlace(PlayerIndex, position, placementStrength);
		if(succeed)
		{
			if(!Match.UseContinuousPlacement)
				SetMoveRight(false);
			NotifyMadeMove();
		}
	}

	void OnRemove(Vector2 position)
	{
		if(!receivingMove)
			return;
		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Remove, position))
		{
			SetMoveRight(false);
			return;
		}
		Match.ReceiveRemove(PlayerIndex, position);
	}

	void OnPass()
	{
		if(!receivingMove)
			return;

		if(Match.TrySendPlayerActionRequest(PlayerIndex, MatchActionType.Pass, Vector2.zero))
		{
			if(!Match.UseContinuousPlacement)
				SetMoveRight(false);
			return;
		}

		if(!Match.UseContinuousPlacement)
			SetMoveRight(false);
		Match.ReceivePass(PlayerIndex);
		NotifyMadeMove();
	}
}

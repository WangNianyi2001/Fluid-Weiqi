using UnityEngine;
using System;
using System.Collections.Generic;

public enum BoardShape
{
	Square,
	Sphere,
}

[System.Serializable]
public struct MatchRule
{
	public string modeId;
	public int boardSize;
	public float stoneHardness;
	public BoardShape boardShape;
	public bool useShrinking;
	public float shrinkSpeed;
}

public struct PlayerInfo
{
	public string name;
	public Color color;
}

public abstract class Match : MonoBehaviour
{
	public static Match Current { get; private set; }
	public static Match Get<T>() where T : Match
		=> Current as T;
	public MatchRule Rule { get; set; }

	readonly List<MatchPlayer> players = new();
	bool isEnded;
	public bool IsEnded => isEnded;
	int activePlayerIndex = -1;
	float activePlacementStrength = 1f;
	bool[] playerPassStates;
	int turnSeq;
	int nextActionSeq = 1;
	int pendingAuthorityActionSeq;
	IMatchTransport matchTransport;

	#region Unity life cycle
	protected void Awake()
	{
		Current = this;
		MatchInput.GetOrCreate(this);
	}

	protected void Start()
	{
		InitializePlayers();
		CurrentPlayerIndex = 0;
		BeginMoveWindow();
		InitializeNetworkTransport();
	}

	protected void OnDestroy()
	{
		if(matchTransport != null)
		{
			matchTransport.OnActionRequestReceived -= OnNetworkActionRequest;
			matchTransport.OnActionResultReceived -= OnNetworkActionResult;
			matchTransport.OnConnectionStateChanged -= OnNetworkConnectionStateChanged;
		}
		players.Clear();
	}
	#endregion

	#region Board
	protected Action onStateChanged;
	public event Action OnStateChanged
	{
		add => onStateChanged += value;
		remove => onStateChanged -= value;
	}

	protected bool LastPlacementSucceed { get; set; } = false;

	protected Action onEnd;
	public event Action OnEnd
	{
		add => onEnd += value;
		remove => onEnd -= value;
	}

	protected void EndMatch()
	{
		if(isEnded)
			return;

		isEnded = true;
		CancelAllPlayers();
		onEnd?.Invoke();
	}
	#endregion

	#region Input
	public bool InputEnabled
	{
		get => !isEnded && players.Count > 0;
		set
		{
			if(!value)
			{
				CancelAllPlayers();
				return;
			}

			if(!isEnded)
				BeginMoveWindow();
		}
	}

	public void ReceiveCursorEnter(Vector2 position)
	{
		OnCursorEnter(position);
	}

	public void ReceiveCursorMove(Vector2 position)
	{
		OnCursorMove(position);
	}

	public void ReceiveCursorExit()
	{
		OnCursorExit();
	}

	public bool ReceivePlace(Vector2 position)
	{
		return ReceivePlace(CurrentPlayerIndex, position, PlacementStrengthPerPlacement);
	}

	public bool ReceivePlace(int playerIndex, Vector2 position)
	{
		return ReceivePlace(playerIndex, position, PlacementStrengthPerPlacement);
	}

	public bool ReceivePlace(int playerIndex, Vector2 position, float strength)
	{
		ExecuteAsPlayer(playerIndex, Mathf.Max(0.0001f, strength), () => OnPlace(position));
		return LastPlacementSucceed;
	}

	public void ReceiveRemove(Vector2 position)
	{
		ReceiveRemove(CurrentPlayerIndex, position);
	}

	public void ReceiveRemove(int playerIndex, Vector2 position)
	{
		ExecuteAsPlayer(playerIndex, 1f, () => OnRemove(position));
	}

	public void ReceivePass()
	{
		ReceivePass(CurrentPlayerIndex);
	}

	public void ReceivePass(int playerIndex)
	{
		ExecuteAsPlayer(playerIndex, 1f, OnPass);
	}

	protected virtual void OnCursorEnter(Vector2 position)
	{
		TryPreviewStone(position);
	}

	protected virtual void OnCursorMove(Vector2 position)
	{
		TryPreviewStone(position);
	}

	protected virtual void OnCursorExit()
	{
		Board.Current.ClearPreview();
	}

	protected int ActivePlayerIndex => activePlayerIndex >= 0 ? activePlayerIndex : CurrentPlayerIndex;

	void ExecuteAsPlayer(int playerIndex, float placementStrength, Action action)
	{
		if(action == null)
			return;

		int previous = activePlayerIndex;
		float previousStrength = activePlacementStrength;
		activePlayerIndex = Mathf.Clamp(playerIndex, 0, Mathf.Max(0, PlayerCount - 1));
		activePlacementStrength = placementStrength;
		action();
		activePlacementStrength = previousStrength;
		activePlayerIndex = previous;
	}

	protected virtual void OnPlace(Vector2 position)
	{
		LastPlacementSucceed = false;

		Board board = Board.Current;
		if(board == null)
			return;

		BoardState currentState = board.State;
		AnalyzeState(currentState);

		LastPlacementSucceed = BoardUtility.TryPlaceStoneStandard(
			board.Caches, currentState, ActivePlayerIndex, position, out BoardState nextState, activePlacementStrength);
		if(!LastPlacementSucceed)
			return;

		if(AudioManager.Instance != null)
			AudioManager.Instance.PlayPlaceStoneSound();

		int capturedStoneCount = CountCapturedStones(currentState, nextState, ActivePlayerIndex);
		if(capturedStoneCount > 0 && AudioManager.Instance != null)
			AudioManager.Instance.PlayCaptureSound();

		// SetState already refreshes board visuals with the latest state.
		board.SetState(nextState);
		board.ClearPreview(false);
		onStateChanged?.Invoke();
	}

	protected virtual void OnRemove(Vector2 position) { }

	protected virtual void OnPass() { }

	bool TryPreviewStone(Vector2 position)
	{
		Board board = Board.Current;
		if(board == null)
			return false;

		BoardState state = board.State;

		if(IsOccupiedAtAbsolutePosition(board, state, position))
		{
			board.ClearPreview();
			return false;
		}

		if(position.x < 0 || position.x >= state.Size || position.y < 0 || position.y >= state.Size)
		{
			if(!(board.Caches.topology == BoardUtility.BoardTopology.Sphere && position.y >= 0 && position.y < state.Size))
			{
				board.ClearPreview();
				return false;
			}
		}

		BoardState previewState = new(state);
		previewState.AddStone(currentPlayerIndex, position);
		board.ShowPreview(previewState);
		return true;
	}

	bool IsOccupiedAtAbsolutePosition(Board board, BoardState renderState, Vector2 position)
	{
		AnalyzeState(renderState);
		if(board == null || board.Caches == null)
			return false;
		return BoardUtility.IsOccupiedAtAbsolutePosition(board.Caches, renderState, position);
	}

	void AnalyzeState(BoardState renderState)
	{
		Board board = Board.Current;
		if(renderState == null || board == null || board.Caches == null || !board.Caches.isInitialized)
			return;

		BoardUtility.RenderForGameplayQuery(board.Caches, renderState);
	}
	#endregion

	#region Players
	public IReadOnlyList<PlayerInfo> PlayerInfos { get; set; }
	public int PlayerCount => PlayerInfos.Count;

	int currentPlayerIndex = 0;
	public int CurrentPlayerIndex
	{
		get => currentPlayerIndex % PlayerCount;
		protected set
		{
			int playerCount = Mathf.Max(1, PlayerCount);
			currentPlayerIndex = ((value % playerCount) + playerCount) % playerCount;
			onCurrentPlayerChanged?.Invoke(currentPlayerIndex);
		}
	}

	public bool IsCurrentPlayerLocallyControllable
	{
		get
		{
			if(isEnded || players.Count == 0)
				return false;
			int safeIndex = Mathf.Clamp(CurrentPlayerIndex, 0, players.Count - 1);
			return players[safeIndex].CanReceiveLocalInput;
		}
	}

	protected int TurnSequence => turnSeq;
	public virtual bool UseContinuousPlacement => false;
	public virtual float ContinuousPlacementFrequencyPerSecond => 0f;
	public virtual float ContinuousPlacementMaxWeightPerSecond => 1f;
	public float PlacementStrengthPerPlacement
	{
		get
		{
			if(!UseContinuousPlacement)
				return 1f;

			float frequency = Mathf.Max(1f, ContinuousPlacementFrequencyPerSecond);
			float maxWeightPerSecond = Mathf.Max(0.0001f, ContinuousPlacementMaxWeightPerSecond);
			return maxWeightPerSecond / frequency;
		}
	}

	public virtual int GetCurrentTurnNumber()
	{
		return -1;
	}

	protected Action<int> onCurrentPlayerChanged;
	public event Action<int> OnCurrentPlayerChanged
	{
		add => onCurrentPlayerChanged += value;
		remove => onCurrentPlayerChanged -= value;
	}

	protected Action<int, bool> onPlayerPassStateChanged;
	public event Action<int, bool> OnPlayerPassStateChanged
	{
		add => onPlayerPassStateChanged += value;
		remove => onPlayerPassStateChanged -= value;
	}

	protected void SetPlayerPassState(int playerIndex, bool passed)
	{
		if(playerPassStates == null || playerPassStates.Length != PlayerCount)
			playerPassStates = new bool[PlayerCount];
		if(playerIndex >= 0 && playerIndex < playerPassStates.Length)
			playerPassStates[playerIndex] = passed;
		onPlayerPassStateChanged?.Invoke(playerIndex, passed);
	}

	protected void StepPlayerIndex()
	{
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % PlayerCount;
	}

	protected void IncrementTurnSequence()
	{
		turnSeq += 1;
	}

	protected int RuntimePlayerCount => players.Count;

	protected void SetPlayerMoveRight(int playerIndex, bool canMove)
	{
		if(playerIndex < 0 || playerIndex >= players.Count)
			return;
		players[playerIndex]?.SetMoveRight(canMove);
	}

	void InitializePlayers()
	{
		players.Clear();
		playerPassStates = new bool[PlayerCount];

		for(int i = 0; i < PlayerCount; ++i)
		{
			MatchPlayer player = CreateRuntimePlayer(i);
			if(player == null)
				throw new MissingReferenceException($"Failed to create player runtime for index {i}.");

			int playerIndex = i;
			player.OnMadeMove += () => OnPlayerMadeMove(playerIndex);
			players.Add(player);
		}
	}

	LocalPlayer CreateLocalPlayerFallback(int playerIndex)
	{
		LocalPlayer fallback = gameObject.AddComponent<LocalPlayer>();
		fallback.Initialize(this, playerIndex);
		return fallback;
	}

	OnlinePlayer CreateOnlinePlayer(int playerIndex, OnlinePlayerRole role, PlayerLocator locator)
	{
		OnlinePlayer online = gameObject.AddComponent<OnlinePlayer>();
		online.Initialize(this, playerIndex, role, locator);
		return online;
	}

	MatchPlayer CreateRuntimePlayer(int playerIndex)
	{
		if(Lobby.Current == null || Lobby.Current.Players == null || playerIndex < 0 || playerIndex >= Lobby.Current.Players.Count)
			return CreateLocalPlayerFallback(playerIndex);

		Lobby lobby = Lobby.Current;
		PlayerDescriptor descriptor = lobby.Players[playerIndex];
		bool ownedByLocal = lobby.IsOwnedByLocal(descriptor);
		PlayerType playerType = descriptor.type;
		switch(playerType)
		{
			case PlayerType.Local:
				if(!ownedByLocal)
					return CreateOnlinePlayer(playerIndex, OnlinePlayerRole.RemoteToLocal, descriptor.locator);

				LocalPlayer local = gameObject.AddComponent<LocalPlayer>();
				local.Initialize(this, playerIndex);
				return local;
			case PlayerType.Ai:
				if(!ownedByLocal)
				{
					Debug.LogWarning($"AI slot {playerIndex} is not owned by this client, fallback to OnlinePlayer proxy.");
					return CreateOnlinePlayer(playerIndex, OnlinePlayerRole.RemoteToLocal, descriptor.locator);
				}

				if(GameManager.Instance == null)
				{
					Debug.LogWarning($"GameManager is missing, fallback to LocalPlayer for AI slot {playerIndex}.");
					return CreateLocalPlayerFallback(playerIndex);
				}

				AiConfig aiConfig = null;
				if(!string.IsNullOrWhiteSpace(descriptor.aiId))
					GameManager.Instance.TryGetAiConfig(descriptor.aiId, out aiConfig);

				if(aiConfig == null)
					aiConfig = GameManager.Instance.FindFirstAiForMode(Rule.modeId);

				if(aiConfig == null)
				{
					Debug.LogWarning($"No AI config available for mode '{Rule.modeId}', fallback to LocalPlayer at index {playerIndex}.");
					return CreateLocalPlayerFallback(playerIndex);
				}

				if(!aiConfig.SupportsMode(Rule.modeId))
				{
					Debug.LogWarning($"AI '{aiConfig.AiName}' ({aiConfig.AiId}) does not support mode '{Rule.modeId}', fallback to LocalPlayer at index {playerIndex}.");
					return CreateLocalPlayerFallback(playerIndex);
				}

				return aiConfig.CreatePlayer(this, playerIndex, Rule);
			case PlayerType.Online:
				return CreateOnlinePlayer(playerIndex, ownedByLocal ? OnlinePlayerRole.LocalToRemote : OnlinePlayerRole.RemoteToLocal, descriptor.locator);
			default:
				return CreateLocalPlayerFallback(playerIndex);
		}
	}

	void OnPlayerMadeMove(int playerIndex)
	{
		if(!CanPlayerMakeMoveNow(playerIndex))
			return;

		OnPlayerMoveAccepted(playerIndex);

		// Try shrinking the board if enabled
		if(!isEnded && Rule.useShrinking)
		{
			Board board = Board.Current;
			if(board != null)
			{
				float shrinkDeltaMargin = GetShrinkDeltaMarginForAcceptedMove();
				if(shrinkDeltaMargin > 0f)
				{
					BoardState shrunkState = board.TryShrink(board.State, shrinkDeltaMargin);
					if(shrunkState == null)
					{
						EndMatch();
						if(ShouldBroadcastAuthorityResult())
							BroadcastAuthorityResult(true, null, playerIndex, pendingAuthorityActionSeq);
						pendingAuthorityActionSeq = 0;
						return;
					}
					board.SetState(shrunkState);
				}
			}
		}

		if(ShouldBroadcastAuthorityResult())
			BroadcastAuthorityResult(true, null, playerIndex, pendingAuthorityActionSeq);

		pendingAuthorityActionSeq = 0;

		if(!isEnded)
			BeginMoveWindow();
	}

	/// <summary>
	/// Defines whether a player is currently allowed to make a move.
	/// Default behavior is strict turn-based: only CurrentPlayerIndex can act.
	/// </summary>
	protected virtual bool CanPlayerMakeMoveNow(int playerIndex)
	{
		return playerIndex == CurrentPlayerIndex;
	}

	/// <summary>
	/// Called once after a valid move has been accepted from a player.
	/// Default behavior increments turn sequence and advances to next player.
	/// </summary>
	protected virtual void OnPlayerMoveAccepted(int playerIndex)
	{
		IncrementTurnSequence();
		if(!isEnded)
			StepPlayerIndex();
	}

	/// <summary>
	/// Returns the board-margin shrink amount to apply after an accepted move.
	/// Default behavior keeps the legacy per-move interpretation.
	/// </summary>
	protected virtual float GetShrinkDeltaMarginForAcceptedMove()
	{
		return Mathf.Max(0f, Rule.shrinkSpeed);
	}

	/// <summary>
	/// Grants move rights for the next move window.
	/// Default behavior cancels all players and grants only the current player.
	/// </summary>
	protected virtual void BeginMoveWindow()
	{
		if(isEnded || players.Count == 0)
			return;

		CancelAllPlayers();
		SetPlayerPassState(CurrentPlayerIndex, false);

		int safeIndex = Mathf.Clamp(CurrentPlayerIndex, 0, players.Count - 1);
		if(!IsCurrentPlayerLocallyControllable)
			Board.Current?.ClearPreview();

		SetPlayerMoveRight(safeIndex, true);
	}

	bool ShouldBroadcastAuthorityResult()
	{
		return Lobby.Current != null && Lobby.Current.IsOnline && Lobby.Current.IsHost && matchTransport != null;
	}

	void BroadcastAuthorityResult(bool accepted, string reason, int playerIndex, int actionSeq)
	{
		if(!ShouldBroadcastAuthorityResult())
			return;

		MatchActionResult result = new MatchActionResult
		{
			accepted = accepted,
			reason = reason,
			playerIndex = playerIndex,
			actionSeq = actionSeq,
			boardSnapshot = NetworkSnapshotUtility.BuildBoardSnapshot(Board.Current?.State),
			flowSnapshot = new MatchFlowSnapshot
			{
				currentPlayerIndex = CurrentPlayerIndex,
				turnSeq = turnSeq,
				isEnded = isEnded,
				passStates = playerPassStates != null ? (bool[])playerPassStates.Clone() : null,
			},
		};
		matchTransport.BroadcastActionResult(result);
	}

	void InitializeNetworkTransport()
	{
		if(Lobby.Current == null || !Lobby.Current.IsOnline)
			return;
		if(GameManager.Instance == null)
			return;

		matchTransport = GameManager.Instance.MatchTransport;
		if(matchTransport == null)
			return;

		matchTransport.OnConnectionStateChanged -= OnNetworkConnectionStateChanged;
		matchTransport.OnConnectionStateChanged += OnNetworkConnectionStateChanged;

		if(Lobby.Current.IsHost)
		{
			matchTransport.OnActionRequestReceived -= OnNetworkActionRequest;
			matchTransport.OnActionRequestReceived += OnNetworkActionRequest;
		}
		else
		{
			matchTransport.OnActionResultReceived -= OnNetworkActionResult;
			matchTransport.OnActionResultReceived += OnNetworkActionResult;
		}
	}

	void OnNetworkConnectionStateChanged(NetworkConnectionState state)
	{
		for(int i = 0; i < players.Count; ++i)
		{
			if(players[i] is OnlinePlayer onlinePlayer)
				onlinePlayer.SetConnectionState(state == NetworkConnectionState.Connected || state == NetworkConnectionState.Degraded);
		}
	}

	void OnNetworkActionRequest(MatchActionRequest request)
	{
		if(request == null || Lobby.Current == null || !Lobby.Current.IsHost)
			return;

		int playerIndex = Lobby.Current.Players.FindIndex(p => p != null && p.locator == request.playerLocator);
		if(playerIndex < 0)
		{
			BroadcastAuthorityResult(false, "unknown-player", -1, request.actionSeq);
			return;
		}

		if(!CanPlayerMakeMoveNow(playerIndex))
		{
			BroadcastAuthorityResult(false, "not-current-player", playerIndex, request.actionSeq);
			return;
		}

		if(request.turnSeq != turnSeq)
		{
			BroadcastAuthorityResult(false, "turn-seq-mismatch", playerIndex, request.actionSeq);
			return;
		}

		if(!(players[playerIndex] is OnlinePlayer onlinePlayer))
		{
			BroadcastAuthorityResult(false, "player-is-not-online-proxy", playerIndex, request.actionSeq);
			return;
		}

		pendingAuthorityActionSeq = request.actionSeq;
		bool handled = onlinePlayer.TryHandleRemoteRequest(request);
		if(!handled)
		{
			pendingAuthorityActionSeq = 0;
			BroadcastAuthorityResult(false, "invalid-action", playerIndex, request.actionSeq);
			return;
		}

		if(request.actionType == MatchActionType.Remove)
		{
			BroadcastAuthorityResult(true, null, playerIndex, request.actionSeq);
			pendingAuthorityActionSeq = 0;
		}
	}

	void OnNetworkActionResult(MatchActionResult result)
	{
		if(result == null)
			return;

		if(result.accepted)
		{
			BoardState syncedState = NetworkSnapshotUtility.ToBoardState(result.boardSnapshot);
			if(Board.Current != null && syncedState != null)
			{
				Board.Current.SetState(syncedState);
				onStateChanged?.Invoke();
			}

			if(result.flowSnapshot != null)
			{
				turnSeq = result.flowSnapshot.turnSeq;
				CurrentPlayerIndex = result.flowSnapshot.currentPlayerIndex;
				bool[] passStates = result.flowSnapshot.passStates;
				if(passStates != null)
				{
					for(int i = 0; i < PlayerCount; ++i)
						SetPlayerPassState(i, i < passStates.Length && passStates[i]);
				}

				if(result.flowSnapshot.isEnded && !isEnded)
				{
					isEnded = true;
					CancelAllPlayers();
					onEnd?.Invoke();
				}
				else if(!result.flowSnapshot.isEnded && !isEnded)
				{
					BeginMoveWindow();
				}
			}
		}
		else
		{
			BeginMoveWindow();
		}
	}

	public bool TrySendPlayerActionRequest(int playerIndex, MatchActionType actionType, Vector2 position, float strength = 1f)
	{
		if(Lobby.Current == null || !Lobby.Current.IsOnline || Lobby.Current.IsHost)
			return false;
		if(GameManager.Instance == null || GameManager.Instance.MatchTransport == null)
			return false;
		if(playerIndex < 0 || playerIndex >= Lobby.Current.Players.Count)
			return false;

		MatchActionRequest request = new MatchActionRequest
		{
			playerLocator = Lobby.Current.Players[playerIndex].locator,
			playerIndex = playerIndex,
			actionType = actionType,
			position = position,
			strength = strength,
			turnSeq = turnSeq,
			actionSeq = nextActionSeq++,
		};
		GameManager.Instance.MatchTransport.SendActionRequest(request);
		return true;
	}

	protected void CancelAllPlayers()
	{
		for(int i = 0; i < players.Count; ++i)
			SetPlayerMoveRight(i, false);
	}

	int CountCapturedStones(BoardState oldState, BoardState newState, int placedPlayer)
	{
		int oldOpponentTotal = CountOpponentStones(oldState, placedPlayer);
		int newOpponentTotal = CountOpponentStones(newState, placedPlayer);
		return Mathf.Max(0, oldOpponentTotal - newOpponentTotal);
	}

	int CountOpponentStones(BoardState state, int excludedPlayer)
	{
		int count = 0;
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			if(player == excludedPlayer)
				continue;

			var stones = state.GetStones(player);
			count += stones.Count;
		}
		return count;
	}
	#endregion
}

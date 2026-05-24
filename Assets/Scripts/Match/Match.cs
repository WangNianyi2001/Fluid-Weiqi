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
	readonly Dictionary<int, bool> playerPassStates = new();
	readonly Dictionary<int, bool> playerMoveRights = new();
	int turnSeq;
	int nextActionSeq = 1;
	int pendingAuthorityActionSeq;
	PlayerLocator pendingAuthoritySourceLocator;
	MatchActionType pendingAuthorityActionType;
	Vector2 pendingAuthorityActionPosition;
	float pendingAuthorityActionStrength = 1f;
	bool hasPendingAuthorityAction;

	MatchActionType lastAcceptedActionType;
	Vector2 lastAcceptedActionPosition;
	float lastAcceptedActionStrength = 1f;
	bool hasLastAcceptedAction;

	readonly HashSet<int> pendingLocalActionSeqs = new();
	readonly List<PendingLocalAction> pendingLocalActions = new();
	BoardStateSnapshot lastResolvedBoardSnapshot;
	MatchFlowSnapshot lastResolvedFlowSnapshot;
	IMatchTransport matchTransport;

	const int MaxPendingLocalActions = 64;

	struct PendingLocalAction
	{
		public int actionSeq;
		public int playerIndex;
		public MatchActionType actionType;
		public Vector2 position;
		public float strength;
	}

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
		CaptureResolvedSnapshotFromCurrentState();
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

	public void ReceiveCursorEnter(int playerIndex, Vector2 position)
	{
		ExecuteAsPlayer(playerIndex, PlacementStrengthPerPlacement, () => OnCursorEnter(position));
	}

	public void ReceiveCursorMove(Vector2 position)
	{
		OnCursorMove(position);
	}

	public void ReceiveCursorMove(int playerIndex, Vector2 position)
	{
		ExecuteAsPlayer(playerIndex, PlacementStrengthPerPlacement, () => OnCursorMove(position));
	}

	public void ReceiveCursorExit()
	{
		OnCursorExit();
	}

	public void ReceiveCursorExit(int playerIndex)
	{
		ExecuteAsPlayer(playerIndex, PlacementStrengthPerPlacement, OnCursorExit);
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
		RecordAcceptedAction(MatchActionType.Place, ActivePlayerIndex, position, activePlacementStrength);
		onStateChanged?.Invoke();
	}

	protected virtual void OnRemove(Vector2 position)
	{
		RecordAcceptedAction(MatchActionType.Remove, ActivePlayerIndex, position, 1f);
	}

	protected virtual void OnPass()
	{
		RecordAcceptedAction(MatchActionType.Pass, ActivePlayerIndex, Vector2.zero, 1f);
	}

	void RecordAcceptedAction(MatchActionType actionType, int playerIndex, Vector2 position, float strength)
	{
		hasLastAcceptedAction = true;
		lastAcceptedActionType = actionType;
		lastAcceptedActionPosition = position;
		lastAcceptedActionStrength = Mathf.Max(0.0001f, strength);
	}

	bool TryPreviewStone(Vector2 position)
	{
		Board board = Board.Current;
		if(board == null)
			return false;

		BoardState state = board.State;
		int previewPlayerIndex = ActivePlayerIndex;

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
		previewState.AddStone(previewPlayerIndex, position, activePlacementStrength);
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

	public bool IsPlayerLocallyControllable(int playerIndex)
	{
		if(isEnded || players.Count == 0)
			return false;
		if(playerIndex < 0 || playerIndex >= players.Count)
			return false;
		return players[playerIndex].CanReceiveLocalInput;
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

	protected Action onPlayerPassStateChanged;
	public event Action OnPlayerPassStateChanged
	{
		add => onPlayerPassStateChanged += value;
		remove => onPlayerPassStateChanged -= value;
	}

	protected Action onPlayerMoveRightChanged;
	public event Action OnPlayerMoveRightChanged
	{
		add => onPlayerMoveRightChanged += value;
		remove => onPlayerMoveRightChanged -= value;
	}

	public IReadOnlyDictionary<int, bool> PlayerMoveRights => playerMoveRights;
	public IReadOnlyDictionary<int, bool> PlayerPassStates => playerPassStates;

	protected void SetPlayerPassState(int playerIndex, bool passed)
	{
		if(playerIndex < 0 || playerIndex >= PlayerCount)
			return;

		bool previous = playerPassStates.TryGetValue(playerIndex, out bool existing) && existing;
		bool changed = previous != passed;
		playerPassStates[playerIndex] = passed;
		if(changed)
			onPlayerPassStateChanged?.Invoke();
	}

	protected void StepPlayerIndex()
	{
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % PlayerCount;
	}

	protected void IncrementTurnSequence()
	{
		turnSeq += 1;
	}

	public bool CanPlayerMove(int playerIndex)
	{
		if(playerIndex < 0)
			return false;
		return playerMoveRights.TryGetValue(playerIndex, out bool canMove) && canMove;
	}

	protected int RuntimePlayerCount => players.Count;

	protected void SetPlayerMoveRight(int playerIndex, bool canMove)
	{
		if(playerIndex < 0 || playerIndex >= players.Count)
			return;

		bool previous = playerMoveRights.TryGetValue(playerIndex, out bool existing) && existing;
		bool changed = previous != canMove;
		playerMoveRights[playerIndex] = canMove;
		players[playerIndex]?.SetMoveRight(canMove);
		if(changed)
			onPlayerMoveRightChanged?.Invoke();
	}

	void InitializePlayers()
	{
		players.Clear();
		playerPassStates.Clear();
		playerMoveRights.Clear();

		for(int i = 0; i < PlayerCount; ++i)
		{
			playerPassStates[i] = false;
			playerMoveRights[i] = false;
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
							BroadcastAcceptedAuthorityMessages(playerIndex);
						ClearPendingAuthorityContext();
						hasLastAcceptedAction = false;
						return;
					}
					board.SetState(shrunkState);
				}
			}
		}

		if(ShouldBroadcastAuthorityResult())
			BroadcastAcceptedAuthorityMessages(playerIndex);

		ClearPendingAuthorityContext();
		hasLastAcceptedAction = false;

		if(!isEnded && !UseContinuousPlacement)
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

	void SendAuthorityResult(MatchActionResult result, PlayerLocator targetPlayerLocator)
	{
		if(!ShouldBroadcastAuthorityResult())
			return;
		if(!targetPlayerLocator.IsValid)
			return;

		matchTransport.SendActionResult(result, targetPlayerLocator);
	}

	void BroadcastAuthorityResult(MatchActionResult result, PlayerLocator excludedPlayerLocator)
	{
		if(!ShouldBroadcastAuthorityResult())
			return;

		matchTransport.BroadcastActionResult(result, excludedPlayerLocator);
	}

	MatchFlowSnapshot BuildFlowSnapshot()
	{
		return new MatchFlowSnapshot
		{
			currentPlayerIndex = CurrentPlayerIndex,
			turnSeq = turnSeq,
			isEnded = isEnded,
			passStates = BuildPassStateArray(),
		};
	}

	MatchActionResult CreateSnapshotResult(MatchResultKind resultKind, bool accepted, string reason, int playerIndex, int actionSeq)
	{
		return new MatchActionResult
		{
			resultKind = resultKind,
			accepted = accepted,
			reason = reason,
			playerIndex = playerIndex,
			actionSeq = actionSeq,
			boardSnapshot = NetworkSnapshotUtility.BuildBoardSnapshot(Board.Current?.State),
			flowSnapshot = BuildFlowSnapshot(),
		};
	}

	MatchActionResult CreateDeltaResult(int playerIndex)
	{
		MatchActionResult result = new MatchActionResult
		{
			resultKind = MatchResultKind.DeltaPush,
			accepted = true,
			reason = null,
			playerIndex = playerIndex,
			actionSeq = pendingAuthorityActionSeq,
			sourcePlayerLocator = pendingAuthoritySourceLocator,
			flowSnapshot = BuildFlowSnapshot(),
		};

		if(hasPendingAuthorityAction)
		{
			result.deltaOps.Add(new MatchDeltaOp
			{
				opType = pendingAuthorityActionType switch
				{
					MatchActionType.Place => MatchDeltaOpType.Place,
					MatchActionType.Remove => MatchDeltaOpType.Remove,
					_ => MatchDeltaOpType.Pass,
				},
				playerIndex = playerIndex,
				position = pendingAuthorityActionPosition,
				strength = Mathf.Max(0.0001f, pendingAuthorityActionStrength),
			});
		}
		else if(hasLastAcceptedAction)
		{
			result.deltaOps.Add(new MatchDeltaOp
			{
				opType = lastAcceptedActionType switch
				{
					MatchActionType.Place => MatchDeltaOpType.Place,
					MatchActionType.Remove => MatchDeltaOpType.Remove,
					_ => MatchDeltaOpType.Pass,
				},
				playerIndex = playerIndex,
				position = lastAcceptedActionPosition,
				strength = Mathf.Max(0.0001f, lastAcceptedActionStrength),
			});
		}

		return result;
	}

	void ClearPendingAuthorityContext()
	{
		pendingAuthorityActionSeq = 0;
		pendingAuthoritySourceLocator = default;
		pendingAuthorityActionType = MatchActionType.Pass;
		pendingAuthorityActionPosition = Vector2.zero;
		pendingAuthorityActionStrength = 1f;
		hasPendingAuthorityAction = false;
	}

	void BroadcastAcceptedAuthorityMessages(int playerIndex)
	{
		if(!ShouldBroadcastAuthorityResult())
			return;

		if(hasPendingAuthorityAction && pendingAuthoritySourceLocator.IsValid)
		{
			MatchActionResult ack = CreateSnapshotResult(MatchResultKind.ActionAck, true, null, playerIndex, pendingAuthorityActionSeq);
			ack.targetPlayerLocator = pendingAuthoritySourceLocator;
			ack.sourcePlayerLocator = pendingAuthoritySourceLocator;
			ack.boardSnapshot = null;
			SendAuthorityResult(ack, pendingAuthoritySourceLocator);

			MatchActionResult delta = CreateDeltaResult(playerIndex);
			BroadcastAuthorityResult(delta, pendingAuthoritySourceLocator);
			return;
		}

		MatchActionResult hostDelta = CreateDeltaResult(playerIndex);
		BroadcastAuthorityResult(hostDelta, default);
	}

	void SendSnapshotToClient(PlayerLocator targetPlayerLocator)
	{
		if(!targetPlayerLocator.IsValid)
			return;

		MatchActionResult snapshot = CreateSnapshotResult(MatchResultKind.SnapshotPush, true, null, -1, 0);
		snapshot.targetPlayerLocator = targetPlayerLocator;
		SendAuthorityResult(snapshot, targetPlayerLocator);
	}

	bool[] BuildPassStateArray()
	{
		if(PlayerCount <= 0)
			return Array.Empty<bool>();

		bool[] states = new bool[PlayerCount];
		for(int i = 0; i < states.Length; ++i)
			states[i] = playerPassStates.TryGetValue(i, out bool passed) && passed;
		return states;
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

		if(request.requestKind == MatchRequestKind.SnapshotPull)
		{
			SendSnapshotToClient(request.playerLocator);
			return;
		}

		int playerIndex = Lobby.Current.Players.FindIndex(p => p != null && p.locator == request.playerLocator);
		if(playerIndex < 0)
		{
			MatchActionResult reject = CreateSnapshotResult(MatchResultKind.ActionReject, false, "unknown-player", -1, request.actionSeq);
			reject.targetPlayerLocator = request.playerLocator;
			SendAuthorityResult(reject, request.playerLocator);
			return;
		}

		if(!CanPlayerMakeMoveNow(playerIndex))
		{
			MatchActionResult reject = CreateSnapshotResult(MatchResultKind.ActionReject, false, "not-current-player", playerIndex, request.actionSeq);
			reject.targetPlayerLocator = request.playerLocator;
			SendAuthorityResult(reject, request.playerLocator);
			return;
		}

		if(request.turnSeq != turnSeq)
		{
			MatchActionResult reject = CreateSnapshotResult(MatchResultKind.ActionReject, false, "turn-seq-mismatch", playerIndex, request.actionSeq);
			reject.targetPlayerLocator = request.playerLocator;
			SendAuthorityResult(reject, request.playerLocator);
			return;
		}

		if(!(players[playerIndex] is OnlinePlayer onlinePlayer))
		{
			MatchActionResult reject = CreateSnapshotResult(MatchResultKind.ActionReject, false, "player-is-not-online-proxy", playerIndex, request.actionSeq);
			reject.targetPlayerLocator = request.playerLocator;
			SendAuthorityResult(reject, request.playerLocator);
			return;
		}

		pendingAuthorityActionSeq = request.actionSeq;
		pendingAuthoritySourceLocator = request.playerLocator;
		pendingAuthorityActionType = request.actionType;
		pendingAuthorityActionPosition = request.position;
		pendingAuthorityActionStrength = request.strength;
		hasPendingAuthorityAction = true;
		bool handled = onlinePlayer.TryHandleRemoteRequest(request);
		if(!handled)
		{
			ClearPendingAuthorityContext();
			MatchActionResult reject = CreateSnapshotResult(MatchResultKind.ActionReject, false, "invalid-action", playerIndex, request.actionSeq);
			reject.targetPlayerLocator = request.playerLocator;
			SendAuthorityResult(reject, request.playerLocator);
			return;
		}

		if(request.actionType == MatchActionType.Remove)
		{
			BroadcastAcceptedAuthorityMessages(playerIndex);
			ClearPendingAuthorityContext();
			hasLastAcceptedAction = false;
		}
	}

	void OnNetworkActionResult(MatchActionResult result)
	{
		if(result == null)
			return;
		if(result.targetPlayerLocator.IsValid && Lobby.Current != null && result.targetPlayerLocator != Lobby.Current.LocalPlayerLocator)
			return;

		switch(result.resultKind)
		{
			case MatchResultKind.ActionReject:
				pendingLocalActionSeqs.Remove(result.actionSeq);
				pendingLocalActions.Clear();
				RestoreResolvedSnapshotLocally();
				RequestLatestSnapshotFromHost();
				if(!UseContinuousPlacement)
					BeginMoveWindow();
				return;
			case MatchResultKind.ActionAck:
				pendingLocalActionSeqs.Remove(result.actionSeq);
				RemovePendingLocalActionBySequence(result.actionSeq);
				ApplyFlowSnapshot(result.flowSnapshot);
				return;
			case MatchResultKind.DeltaPush:
				ApplyDeltaResult(result);
				ApplyFlowSnapshot(result.flowSnapshot);
				if(pendingLocalActions.Count == 0)
					CaptureResolvedSnapshotFromCurrentState();
				return;
			case MatchResultKind.SnapshotPush:
				ApplySnapshotResult(result);
				return;
		}

		if(result.accepted)
		{
			ApplySnapshotResult(result);
		}
		else
		{
			pendingLocalActionSeqs.Remove(result.actionSeq);
			pendingLocalActions.Clear();
			RestoreResolvedSnapshotLocally();
			RequestLatestSnapshotFromHost();
			if(!UseContinuousPlacement)
				BeginMoveWindow();
		}
	}

	void ApplyDeltaResult(MatchActionResult result)
	{
		if(Board.Current == null || result.deltaOps == null || result.deltaOps.Count == 0)
			return;

		for(int i = 0; i < result.deltaOps.Count; ++i)
		{
			MatchDeltaOp op = result.deltaOps[i];
			switch(op.opType)
			{
				case MatchDeltaOpType.Place:
					ReceivePlace(op.playerIndex, op.position, op.strength);
					break;
				case MatchDeltaOpType.Remove:
					ReceiveRemove(op.playerIndex, op.position);
					break;
				case MatchDeltaOpType.Pass:
					ReceivePass(op.playerIndex);
					break;
			}
		}

		onStateChanged?.Invoke();
	}

	void ApplySnapshotResult(MatchActionResult result)
	{
		BoardState syncedState = NetworkSnapshotUtility.ToBoardState(result.boardSnapshot);
		if(Board.Current != null && syncedState != null)
		{
			Board.Current.SetState(syncedState);
			onStateChanged?.Invoke();
		}

		ApplyFlowSnapshot(result.flowSnapshot);
		pendingLocalActionSeqs.Clear();
		pendingLocalActions.Clear();
		CaptureResolvedSnapshotFromCurrentState();
	}

	void ApplyFlowSnapshot(MatchFlowSnapshot flowSnapshot)
	{
		if(flowSnapshot == null)
			return;

		turnSeq = flowSnapshot.turnSeq;
		CurrentPlayerIndex = flowSnapshot.currentPlayerIndex;
		bool[] passStates = flowSnapshot.passStates;
		if(passStates != null)
		{
			for(int i = 0; i < PlayerCount; ++i)
				SetPlayerPassState(i, i < passStates.Length && passStates[i]);
		}

		if(flowSnapshot.isEnded && !isEnded)
		{
			isEnded = true;
			CancelAllPlayers();
			onEnd?.Invoke();
		}
		else if(!flowSnapshot.isEnded && !isEnded && !UseContinuousPlacement)
		{
			BeginMoveWindow();
		}
	}

	public bool RequestLatestSnapshotFromHost()
	{
		if(Lobby.Current == null || !Lobby.Current.IsOnline || Lobby.Current.IsHost)
			return false;
		if(GameManager.Instance == null || GameManager.Instance.MatchTransport == null)
			return false;

		MatchActionRequest request = new MatchActionRequest
		{
			requestKind = MatchRequestKind.SnapshotPull,
			playerLocator = Lobby.Current.LocalPlayerLocator,
			playerIndex = -1,
			actionType = MatchActionType.Pass,
			position = Vector2.zero,
			strength = 1f,
			turnSeq = turnSeq,
			actionSeq = nextActionSeq++,
		};
		GameManager.Instance.MatchTransport.SendActionRequest(request);
		return true;
	}

	public bool TryApplyPredictedActionAndSendRequest(int playerIndex, MatchActionType actionType, Vector2 position, float strength = 1f)
	{
		if(Lobby.Current == null || !Lobby.Current.IsOnline || Lobby.Current.IsHost)
			return false;
		if(GameManager.Instance == null || GameManager.Instance.MatchTransport == null)
			return false;
		if(playerIndex < 0 || playerIndex >= Lobby.Current.Players.Count)
			return false;
		if(pendingLocalActions.Count >= MaxPendingLocalActions)
			return false;
		if(pendingLocalActions.Count == 0)
			CaptureResolvedSnapshotFromCurrentState();

		bool applied = actionType switch
		{
			MatchActionType.Place => ReceivePlace(playerIndex, position, strength),
			MatchActionType.Pass => true,
			MatchActionType.Remove => true,
			_ => false,
		};
		if(!applied)
			return false;

		if(actionType == MatchActionType.Pass)
			ReceivePass(playerIndex);
		else if(actionType == MatchActionType.Remove)
			ReceiveRemove(playerIndex, position);

		int actionSeq = nextActionSeq++;
		MatchActionRequest request = new MatchActionRequest
		{
			requestKind = MatchRequestKind.Action,
			playerLocator = Lobby.Current.Players[playerIndex].locator,
			playerIndex = playerIndex,
			actionType = actionType,
			position = position,
			strength = strength,
			turnSeq = turnSeq,
			actionSeq = actionSeq,
		};
		GameManager.Instance.MatchTransport.SendActionRequest(request);
		pendingLocalActionSeqs.Add(actionSeq);
		pendingLocalActions.Add(new PendingLocalAction
		{
			actionSeq = actionSeq,
			playerIndex = playerIndex,
			actionType = actionType,
			position = position,
			strength = Mathf.Max(0.0001f, strength),
		});
		return true;
	}

	void CaptureResolvedSnapshotFromCurrentState()
	{
		lastResolvedBoardSnapshot = NetworkSnapshotUtility.BuildBoardSnapshot(Board.Current?.State);
		lastResolvedFlowSnapshot = BuildFlowSnapshot();
	}

	void RemovePendingLocalActionBySequence(int actionSeq)
	{
		for(int i = pendingLocalActions.Count - 1; i >= 0; --i)
		{
			if(pendingLocalActions[i].actionSeq <= actionSeq)
				pendingLocalActions.RemoveAt(i);
		}

		if(pendingLocalActions.Count == 0)
			CaptureResolvedSnapshotFromCurrentState();
	}

	void RestoreResolvedSnapshotLocally()
	{
		if(lastResolvedBoardSnapshot == null || Board.Current == null)
			return;

		BoardState resolvedState = NetworkSnapshotUtility.ToBoardState(lastResolvedBoardSnapshot);
		if(resolvedState == null)
			return;

		Board.Current.SetState(resolvedState);
		ApplyFlowSnapshot(lastResolvedFlowSnapshot);
		onStateChanged?.Invoke();
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
			requestKind = MatchRequestKind.Action,
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

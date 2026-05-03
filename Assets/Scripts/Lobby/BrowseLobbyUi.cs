using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BrowseLobbyUi : MonoBehaviour
{
	const int DefaultPageSize = 8;

	[Header("Search & Navigation")]
	[SerializeField] InputField searchInput;
	[SerializeField] Button refreshButton;
	[SerializeField] Button prevPageButton;
	[SerializeField] Button nextPageButton;
	[SerializeField] Text pageText;

	[Header("Lobby list")]
	[SerializeField] Transform rowContainer;
	[SerializeField] int pageSize = DefaultPageSize;

	[Header("Join by invite code")]
	[SerializeField] InputField inviteCodeInput;
	[SerializeField] Button joinByCodeButton;

	[Header("Navigation")]
	[SerializeField] Button backButton;

	int currentOffset = 0;
	string currentFilter = "";
	bool isLoading = false;

	static GameObject rowPrefab;
	static GameObject RowPrefab => rowPrefab ??= Resources.Load<GameObject>("UI/Browse Lobby/Lobby Row");

	ILobbyService Service => GameManager.Instance?.LobbyService;

	#region Unity life cycle
	protected void Start()
	{
		if(pageSize <= 0)
			pageSize = DefaultPageSize;

		searchInput.onEndEdit.AddListener(OnSearchEndEdit);

		Refresh();
	}

	protected void OnDestroy()
	{
		refreshButton.onClick.RemoveListener(OnRefreshClicked);
		prevPageButton.onClick.RemoveListener(OnPrevPageClicked);
		nextPageButton.onClick.RemoveListener(OnNextPageClicked);
		searchInput.onEndEdit.RemoveListener(OnSearchEndEdit);
		if(joinByCodeButton != null)
			joinByCodeButton.onClick.RemoveListener(OnJoinByCodeClicked);
		if(backButton != null)
			backButton.onClick.RemoveListener(OnBackButtonClicked);
	}
	#endregion

	#region Controls
	public void OnRefreshClicked()
	{
		currentOffset = 0;
		Refresh();
	}

	public void OnPrevPageClicked()
	{
		currentOffset = Mathf.Max(0, currentOffset - pageSize);
		Refresh();
	}

	public void OnNextPageClicked()
	{
		currentOffset += pageSize;
		Refresh();
	}

	public void OnSearchEndEdit(string value)
	{
		currentFilter = value ?? "";
		currentOffset = 0;
		Refresh();
	}

	public void OnBackButtonClicked()
	{
		GameManager.Instance?.SwitchScene(GameScene.StartMenu);
	}
	#endregion

	#region Query
	void Refresh()
	{
		if(isLoading)
			return;

		isLoading = true;
		SetInteractable(false);

		if(Service == null)
		{
			OnQueryResult(new List<LobbySnapshot>());
			return;
		}

		Service.QueryLobbies(currentOffset, pageSize, currentFilter, OnQueryResult);
	}

	void OnQueryResult(IReadOnlyList<LobbySnapshot> results)
	{
		isLoading = false;
		results ??= new List<LobbySnapshot>();

		if(rowContainer != null)
			GameUtility.ClearChildren(rowContainer);

		if(RowPrefab == null)
		{
			Debug.LogError("BrowseLobbyUi cannot load prefab at Resources/UI/Browse Lobby/Lobby Row.");
			results = new List<LobbySnapshot>();
		}
		else if(rowContainer != null)
		{
			for(int i = 0; i < results.Count; ++i)
			{
				LobbySnapshot snapshot = results[i];
				LobbyRow row = Instantiate(RowPrefab, rowContainer).GetComponent<LobbyRow>();
				row.Bind(snapshot, () => OnJoinLobby(snapshot.lobbyId));
			}
		}

		bool hasPrev = currentOffset > 0;
		bool hasNext = results.Count == pageSize;
		int page = currentOffset / pageSize + 1;
		pageText.text = $"第 {page} 页";

		SetInteractable(true);
		prevPageButton.interactable = hasPrev;
		nextPageButton.interactable = hasNext;
	}

	void OnJoinLobby(string lobbyId)
	{
		if(isLoading)
			return;

		isLoading = true;
		SetInteractable(false);
		if(Service == null)
		{
			OnJoinResult(new JoinLobbyResult { success = false });
			return;
		}

		Service.JoinLobby(lobbyId, OnJoinResult);
	}

	public void OnJoinByCodeClicked()
	{
		if(isLoading)
			return;

		string code = inviteCodeInput != null ? inviteCodeInput.text?.Trim() : null;
		if(string.IsNullOrEmpty(code))
			return;

		isLoading = true;
		SetInteractable(false);
		if(Service == null)
		{
			OnJoinResult(new JoinLobbyResult { success = false });
			return;
		}

		Service.JoinLobbyByCode(code, OnJoinResult);
	}

	void OnJoinResult(JoinLobbyResult result)
	{
		isLoading = false;
		if(result != null && result.success)
		{
			GameManager.Instance?.LoadClientLobby(
				result.lobbyLocator,
				result.localPlayerLocator,
				result.visibility,
				result.matchRule,
				result.players);
			GameManager.Instance?.SwitchScene(GameScene.Lobby);
		}
		else
			SetInteractable(true);
	}

	void SetInteractable(bool value)
	{
		refreshButton.interactable = value;
		searchInput.interactable = value;
		prevPageButton.interactable = value;
		nextPageButton.interactable = value;
		if(inviteCodeInput != null)
			inviteCodeInput.interactable = value;
		if(joinByCodeButton != null)
			joinByCodeButton.interactable = value;
		if(backButton != null)
			backButton.interactable = value;
	}
	#endregion
}

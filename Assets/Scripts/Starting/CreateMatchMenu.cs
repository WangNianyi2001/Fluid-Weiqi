using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CreateMatchMenu : MonoBehaviour
{
	[SerializeField] Dropdown matchModeDropdown;
	static readonly MatchMode[] matchModeIndexMap = new MatchMode[]
	{
		MatchMode.Traditional,
		MatchMode.Training,
	};
	public MatchMode MatchMode => matchModeIndexMap[matchModeDropdown.value];

	[SerializeField] Text playerCountText;
	[SerializeField] Slider playerCountSlider;
	public int PlayerCount => Mathf.FloorToInt(playerCountSlider.value);

	[SerializeField] Text boardSizeText;
	[SerializeField] Slider boardSizeSlider;
	public int BoardSize => Mathf.FloorToInt(boardSizeSlider.value);

	protected void Awake()
	{
		boardSizeSlider.onValueChanged.AddListener(_ => RefreshBoardSizeText());
		RefreshBoardSizeText();
		playerCountSlider.onValueChanged.AddListener(_ => RefreshPlayerCount());
		RefreshPlayerCount();
	}

	void RefreshPlayerCount()
	{
		playerCountText.text = PlayerCount.ToString();
	}

	void RefreshBoardSizeText()
	{
		boardSizeText.text = BoardSize.ToString();
	}

	public void StartMatch()
	{
		GameManager.MatchConfig = new()
		{
			mode = MatchMode,
			playerCount = PlayerCount,
			boardSize = BoardSize,
		};
		GameManager.Instance.StartMatch();
	}
}

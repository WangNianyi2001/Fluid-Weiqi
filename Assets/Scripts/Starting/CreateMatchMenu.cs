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

	[SerializeField] Text boardSizeText;
	[SerializeField] Slider boardSizeSlider;
	public int BoardSize => Mathf.FloorToInt(boardSizeSlider.value);

	protected void Awake()
	{
		boardSizeSlider.onValueChanged.AddListener(_ => RefreshBoardSizeText());
		RefreshBoardSizeText();
	}

	void RefreshBoardSizeText()
	{
		boardSizeText.text = BoardSize.ToString();
	}

	public void StartMatch()
	{
		GameManager.MatchConfig = new()
		{
			matchMode = MatchMode,
			boardSize = BoardSize,
		};
		GameManager.Instance.StartMatch();
	}
}

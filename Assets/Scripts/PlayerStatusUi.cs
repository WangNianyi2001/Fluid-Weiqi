using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUi : MonoBehaviour
{
	[SerializeField] private Graphic playerColorGraphic;
	[SerializeField] private Text playerNameText;
	[SerializeField] private Text areaValueText;
	[SerializeField] private GameObject currentTurnIndicator;

	public void SetPlayerName(string playerName)
	{
		if(playerNameText != null)
			playerNameText.text = playerName;
	}

	public void SetAreaValue(int areaPercent)
	{
		if(areaValueText != null)
			areaValueText.text = $"{areaPercent}%";
	}

	public void SetPlayerColor(Color color)
	{
		if(playerColorGraphic != null)
			playerColorGraphic.color = color;
	}

	public void SetCurrentTurn(bool isCurrentTurn)
	{
		if(currentTurnIndicator != null)
			currentTurnIndicator.SetActive(isCurrentTurn);
	}
}

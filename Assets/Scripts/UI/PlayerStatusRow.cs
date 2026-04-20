using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusRow : MonoBehaviour
{
	[SerializeField] Graphic colorGraphic;
	public Color Color
	{
		get => colorGraphic.color;
		set => colorGraphic.color = value;
	}

	[SerializeField] Text nameText;
	public string Name
	{
		get => nameText.text;
		set => nameText.text = value;
	}

	[SerializeField] Text areaValueText;
	float areaValue;
	public float AreaValue
	{
		get => areaValue;
		set => areaValueText.text = $"{Mathf.RoundToInt((areaValue = value) * 100)}%";
	}

	[SerializeField] GameObject currentTurnIndicator;
	public bool IsCurrent
	{
		get => currentTurnIndicator.activeSelf;
		set => currentTurnIndicator.SetActive(value);
	}
}

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
	string baseName;
	bool isScoringRequested;
	bool isResigned;
	public string Name
	{
		get => baseName;
		set
		{
			baseName = value;
			RefreshNameText();
		}
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

	[SerializeField] Graphic passedIndicator;
	public bool IsPassed
	{
		set
		{
			if(passedIndicator != null)
				passedIndicator.enabled = value;
		}
	}

	public bool IsScoringRequested
	{
		set
		{
			isScoringRequested = value;
			RefreshNameText();
		}
	}

	public bool IsResigned
	{
		set
		{
			isResigned = value;
			RefreshNameText();
		}
	}

	void RefreshNameText()
	{
		if(nameText == null)
			return;

		string suffix = string.Empty;
		if(isScoringRequested)
			suffix += " [申请点目]";
		if(isResigned)
			suffix += " [已认输]";

		nameText.text = (baseName ?? string.Empty) + suffix;
	}
}

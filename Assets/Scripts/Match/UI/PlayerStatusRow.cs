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
	public string Name
	{
		get => baseName;
		set
		{
			baseName = value;
			if(nameText != null)
				nameText.text = baseName ?? string.Empty;
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

	[SerializeField] GameObject passedIndicator;
	public bool IsPassed
	{
		set => passedIndicator?.SetActive(value);
	}

	[SerializeField] GameObject scoringRequestedIndicator;

	public bool IsScoringRequested
	{
		set => scoringRequestedIndicator?.SetActive(value);
	}

	[SerializeField] GameObject resignedIndicator;

	public bool IsResigned
	{
		set => resignedIndicator?.SetActive(value);
	}

	[SerializeField] GameObject offlineIndicator;

	public bool IsOffline
	{
		set => offlineIndicator?.SetActive(value);
	}
}

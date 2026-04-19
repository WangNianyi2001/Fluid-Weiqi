using UnityEngine;
using Cinemachine;
using System.Collections;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	#region References
	[SerializeField] CinemachineBrain cBrain;
	[SerializeField] GameObject matchAnchor;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Instance = this;
		cBrain = Camera.main.gameObject.GetComponent<CinemachineBrain>();
	}

	protected void Start()
	{
		EndMatch();
	}
	#endregion

	#region Life cycle
	Match match;
	Board board;

	public static MatchConfig MatchConfig { get; set; } = MatchConfig.Default;

	public void StartMatch()
	{
		if(MatchConfig.matchMode == MatchMode.Undefined)
			throw new System.ArgumentOutOfRangeException("Cannot start match, match mode is undefined.");
		StartCoroutine(StartMatchCoroutine());
	}

	IEnumerator StartMatchCoroutine()
	{
		matchAnchor.SetActive(true);
		yield return new WaitForEndOfFrame();
		yield return new WaitUntil(() => !cBrain.IsBlending);

		ConstructMatch();
	}

	void ConstructMatch()
	{
		if(match != null)
		{
			Destroy(match);
			match = null;
		}
		if(board != null)
		{
			Destroy(board.gameObject);
			board = null;
		}

		board = Instantiate(Resources.Load<GameObject>("Prefabs/Board"), matchAnchor.transform).GetComponent<Board>();
		board.Size = MatchConfig.boardSize;

		match = MatchConfig.matchMode switch
		{
			MatchMode.Traditional => matchAnchor.AddComponent<TraditionalMatch>(),
			// TODO: Training
			_ => throw new System.NotImplementedException("Match mode not yet implemented."),
		};
	}

	public void EndMatch()
	{
		matchAnchor.SetActive(false);
	}

	public void QuitGame()
	{
#if UNITY_EDITOR
		if(UnityEditor.EditorApplication.isPlaying)
		{
			UnityEditor.EditorApplication.isPlaying = false;
			return;
		}
#endif
		Application.Quit();
	}
	#endregion
}

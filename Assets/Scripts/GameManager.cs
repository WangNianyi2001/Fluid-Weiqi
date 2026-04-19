using UnityEngine;
using Cinemachine;
using System.Collections;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	#region References
	public Camera Camera { get; private set; }
	CinemachineBrain cBrain;
	[SerializeField] GameObject matchAnchor;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Instance = this;
		if(Camera == null)
			Camera = Camera.main;
		cBrain = Camera.gameObject.GetComponent<CinemachineBrain>();

		BoardUtilities.Initialize();
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;

		BoardUtilities.Dispose();
	}

	protected void Start()
	{
		EndMatch();
	}
	#endregion

	#region Life cycle
	public Match Match { get; private set; }
	public Board Board { get; private set; }

	public static MatchConfig MatchConfig { get; set; } = MatchConfig.Default;

	public void StartMatch()
	{
		if(MatchConfig.mode == MatchMode.Undefined)
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
		if(Match != null)
		{
			Destroy(Match);
			Match = null;
		}
		if(Board != null)
		{
			Destroy(Board.gameObject);
			Board = null;
		}

		Board = Instantiate(Resources.Load<GameObject>("Prefabs/Board"), matchAnchor.transform).GetComponent<Board>();
		Board.Size = MatchConfig.boardSize;
		Board.PlayerCount = MatchConfig.playerCount;

		Match = MatchConfig.mode switch
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

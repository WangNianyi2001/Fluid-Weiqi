using UnityEngine;
using Cinemachine;
using System.Collections;
using System.Linq;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	#region References
	public Camera Camera { get; private set; }
	CinemachineBrain cBrain;
	[SerializeField] Transform matchAnchor;
	[SerializeField] CinemachineVirtualCamera matchVCam;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Instance = this;
		if(Camera == null)
			Camera = Camera.main;
		cBrain = Camera.gameObject.GetComponent<CinemachineBrain>();

		if(AudioManager.Instance == null)
		{
			var audioGo = new GameObject("AudioManager");
			audioGo.AddComponent<AudioManager>();
		}
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;
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
		var playerInfos = GameUtility.MakePlayerInfos(MatchConfig.playerCount);

		// Construct board
		if(Board != null)
		{
			Destroy(Board.gameObject);
			Board = null;
		}

		Board = Instantiate(Resources.Load<GameObject>("Prefabs/Board"), matchAnchor).GetComponent<Board>();
		Board.PlayerColors = playerInfos.Select(i => i.color).ToArray();
		BoardState initialState = new(MatchConfig.playerCount, MatchConfig.boardSize);
		Board.SetState(initialState);

		// Move camera
		matchVCam.gameObject.SetActive(true);
		matchVCam.enabled = true;
		yield return new WaitForEndOfFrame();
		yield return new WaitUntil(() => !cBrain.IsBlending);

		// Construct match
		if(Match != null)
		{
			Destroy(Match);
			Match = null;
		}
		switch(MatchConfig.mode)
		{
			case MatchMode.Traditional:
				Match = matchAnchor.gameObject.AddComponent<TraditionalMatch>();
				break;

			case MatchMode.Training:
				Match = matchAnchor.gameObject.AddComponent<TrainingMatch>();
				break;

			default:
				throw new System.NotImplementedException("Match mode not yet implemented.");
		}
		Match.PlayerInfos = playerInfos;
	}

	public void EndMatch()
	{
		if(Match != null)
		{
			Destroy(Match);
			Match = null;
		}

		matchVCam.enabled = false;
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

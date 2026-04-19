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

		BoardUtility.Initialize();
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;

		BoardUtility.Dispose();
	}

	protected void Start()
	{
		EndMatch();
	}
	#endregion

	#region Life cycle
	public Match Match { get; private set; }

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

		// Construct match
		if(Match != null)
		{
			Destroy(Match);
			Match = null;
		}
		switch(MatchConfig.mode)
		{
			case MatchMode.Traditional:
				Match = matchAnchor.AddComponent<TraditionalMatch>();
				break;

			default:
				throw new System.NotImplementedException("Match mode not yet implemented.");
		}
		Match.Construct(MatchConfig.boardSize, GameUtility.MakePlayerInfos(MatchConfig.playerCount));
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

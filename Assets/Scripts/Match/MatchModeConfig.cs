using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class MatchModeConfig : ScriptableObject
{
	const string StandardBoardPrefabResourcePath = "Prefabs/Boards/Standard";

	[SerializeField] string modeId;
	[SerializeField] string displayName;
	[SerializeField] bool isTurnBased;
	[SerializeField] List<GameObject> uiAssemblyPrefabs = new();

	public string ModeId => modeId;
	public string DisplayName => displayName;
	public bool IsLegacyMode => GameSettings.Instance.LegacyMatchModes.Contains(this);
	public bool IsTurnBased => isTurnBased;

	public virtual bool ValidateRules(MatchRule rule, Lobby lobby, out string errorMessage)
	{
		errorMessage = null;
		if(rule.boardSize < 2)
		{
			errorMessage = "棋盘尺寸必须大于等于 2。";
			return false;
		}
		return true;
	}

	public virtual void BuildMatch(MatchBuildContext context)
	{
		if(context.MatchSkinPrefab == null)
			throw new MissingReferenceException($"Match mode '{ModeId}' has no match skin prefab configured.");
		if(context.MatchRoot == null)
			throw new MissingReferenceException($"Match mode '{ModeId}' has no match root transform configured.");

		GameObject skinGo = Instantiate(context.MatchSkinPrefab, context.MatchRoot);
		MatchSkin skin = skinGo.GetComponent<MatchSkin>();
		if(skin == null)
			throw new MissingReferenceException($"Match skin prefab '{context.MatchSkinPrefab.name}' does not contain MatchSkin component.");

		RenderSettings.skybox = skin.Skybox;
		DynamicGI.UpdateEnvironment();

		GameObject standardBoardPrefab = Resources.Load<GameObject>(StandardBoardPrefabResourcePath);
		if(standardBoardPrefab == null)
			throw new MissingReferenceException($"Standard board prefab not found at Resources/{StandardBoardPrefabResourcePath}.");

		GameObject boardGo = Instantiate(standardBoardPrefab, skin.BoardRoot);
		Board board = boardGo.GetComponent<Board>();
		if(board == null)
			throw new MissingReferenceException($"Standard board prefab does not contain a Board component.");

		board.SetState(new BoardState(context.PlayerInfos.Count, context.Rule.boardSize));
		board.State.StoneHardness = context.Rule.stoneHardness;
		board.PlayerColors = context.PlayerInfos.Select(info => info.color).ToArray();

		GameObject host = context.UiRoot != null ? context.UiRoot.gameObject : boardGo;
		Match controller = CreateMatchController(host);
		if(controller == null)
			throw new MissingReferenceException($"Match mode '{ModeId}' failed to create match controller.");
		controller.Rule = context.Rule;
		controller.PlayerInfos = context.PlayerInfos;

		for(int i = 0; i < uiAssemblyPrefabs.Count; ++i)
		{
			GameObject uiPrefab = uiAssemblyPrefabs[i];
			if(uiPrefab == null)
				continue;
			Instantiate(uiPrefab, context.UiRoot);
		}
	}

	protected abstract Match CreateMatchController(GameObject host);
}

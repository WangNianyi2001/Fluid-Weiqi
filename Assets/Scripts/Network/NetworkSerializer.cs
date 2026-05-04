using System.Text;
using UnityEngine;

public static class NetworkSerializer
{
	static readonly Encoding Encoding = Encoding.UTF8;

	// --- Lobby snapshot (used as Steam Lobby Metadata value) ---

	public static string SerializeLobbySnapshot(LobbySyncSnapshot snapshot)
		=> JsonUtility.ToJson(snapshot);

	public static LobbySyncSnapshot DeserializeLobbySnapshot(string json)
		=> JsonUtility.FromJson<LobbySyncSnapshot>(json);

	// --- Match action request (used as Steam P2P message payload) ---

	public static byte[] SerializeMatchActionRequest(MatchActionRequest request)
		=> Encoding.GetBytes(JsonUtility.ToJson(request));

	public static MatchActionRequest DeserializeMatchActionRequest(byte[] data, int length)
		=> JsonUtility.FromJson<MatchActionRequest>(Encoding.GetString(data, 0, length));

	// --- Match action result (used as Steam P2P message payload) ---

	public static byte[] SerializeMatchActionResult(MatchActionResult result)
		=> Encoding.GetBytes(JsonUtility.ToJson(result));

	public static MatchActionResult DeserializeMatchActionResult(byte[] data, int length)
		=> JsonUtility.FromJson<MatchActionResult>(Encoding.GetString(data, 0, length));
}

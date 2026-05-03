using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

static class GameSettingsProjectSettingsProvider
{
	const string SettingsPath = "Project/Fluid Weiqi/Game Settings";
	const string PrimaryAssetPath = "Assets/Resources/Game Settings.asset";
	const string LegacyAssetPath = "Assets/Resources/Internal Game Settings.asset";
	const string PrimaryResourcePath = "Game Settings";
	const string LegacyResourcePath = "Internal Game Settings";

	static GameSettings cachedSettings;
	static SerializedObject cachedSerializedSettings;

	[SettingsProvider]
	public static SettingsProvider CreateProvider()
	{
		return new SettingsProvider(SettingsPath, SettingsScope.Project)
		{
			label = "Game Settings",
			guiHandler = _ => DrawGui(),
			keywords = new HashSet<string>(new[]
			{
				"Fluid Weiqi",
				"Game Settings",
				"Match",
				"AI",
				"Mode"
			})
		};
	}

	static void DrawGui()
	{
		EnsureCachedSettings();

		using(new EditorGUILayout.VerticalScope())
		{
			EditorGUILayout.LabelField("Game Settings Asset", EditorStyles.boldLabel);

			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.ObjectField("Asset", cachedSettings, typeof(GameSettings), false);
			EditorGUI.EndDisabledGroup();

			if(cachedSettings == null)
			{
				EditorGUILayout.HelpBox(
					$"No GameSettings asset was found in Resources ('{PrimaryResourcePath}' or legacy '{LegacyResourcePath}').",
					MessageType.Warning);

				using(new EditorGUILayout.HorizontalScope())
				{
					if(GUILayout.Button("Create Game Settings"))
						CreateGameSettingsAsset();

					if(GUILayout.Button("Refresh"))
						RefreshCache();
				}
				return;
			}

			if(cachedSerializedSettings == null)
				cachedSerializedSettings = new SerializedObject(cachedSettings);

			cachedSerializedSettings.Update();
			SerializedProperty prop = cachedSerializedSettings.GetIterator();
			prop.NextVisible(true); // skip "m_Script"
			while(prop.NextVisible(false))
				EditorGUILayout.PropertyField(prop, true);

			if(cachedSerializedSettings.ApplyModifiedProperties())
				EditorUtility.SetDirty(cachedSettings);

			using(new EditorGUILayout.HorizontalScope())
			{
				if(GUILayout.Button("Ping Asset"))
					EditorGUIUtility.PingObject(cachedSettings);

				if(GUILayout.Button("Refresh"))
					RefreshCache();
			}
		}
	}

	static void EnsureCachedSettings()
	{
		if(cachedSettings != null)
			return;

		RefreshCache();
	}

	static void RefreshCache()
	{
		cachedSettings = FindSettingsAsset();
		cachedSerializedSettings = cachedSettings != null ? new SerializedObject(cachedSettings) : null;
	}

	static GameSettings FindSettingsAsset()
	{
		GameSettings settings = Resources.Load<GameSettings>(PrimaryResourcePath);
		if(settings != null)
			return settings;

		settings = Resources.Load<GameSettings>(LegacyResourcePath);
		if(settings != null)
			return settings;

		settings = AssetDatabase.LoadAssetAtPath<GameSettings>(PrimaryAssetPath);
		if(settings != null)
			return settings;

		settings = AssetDatabase.LoadAssetAtPath<GameSettings>(LegacyAssetPath);
		if(settings != null)
			return settings;

		string[] guids = AssetDatabase.FindAssets("t:GameSettings");
		for(int i = 0; i < guids.Length; ++i)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			settings = AssetDatabase.LoadAssetAtPath<GameSettings>(path);
			if(settings != null)
				return settings;
		}

		return null;
	}

	static void CreateGameSettingsAsset()
	{
		string resourcesDir = Path.GetDirectoryName(PrimaryAssetPath);
		if(!Directory.Exists(resourcesDir))
			Directory.CreateDirectory(resourcesDir);

		GameSettings settings = ScriptableObject.CreateInstance<GameSettings>();
		AssetDatabase.CreateAsset(settings, PrimaryAssetPath);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		cachedSettings = settings;
		cachedSerializedSettings = new SerializedObject(settings);

		Selection.activeObject = settings;
		EditorGUIUtility.PingObject(settings);
	}
}

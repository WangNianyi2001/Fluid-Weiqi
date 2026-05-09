using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Batch-mode build helper for the macOS Standalone target.
//
// CLI usage:
//   Unity -batchmode -nographics -quit \
//     -projectPath . \
//     -buildTarget OSXUniversal \
//     -executeMethod MacBuildScript.BuildFromCli \
//     [-arch arm64|x64|universal] \
//     [-outDir Build/Mac]
//
// Defaults: arch=universal, outDir=$PWD/Build/Mac
//
// The script only configures the macOS architecture for this build; it
// intentionally does not modify scripting backend, scripting defines or
// the bundle identifier — those are owned by ProjectSettings.
public static class MacBuildScript
{
	const int ArchX64 = 0;
	const int ArchARM64 = 1;
	const int ArchUniversal = 2;

	[MenuItem("Build/macOS (Apple Silicon)")]
	public static void MenuBuildArm64()
	{
		Run(ArchARM64);
	}

	[MenuItem("Build/macOS (Universal)")]
	public static void MenuBuildUniversal()
	{
		Run(ArchUniversal);
	}

	public static void BuildFromCli()
	{
		int arch = ArchUniversal;
		string outDir = null;
		string[] args = System.Environment.GetCommandLineArgs();
		for(int i = 0; i < args.Length; ++i)
		{
			if(args[i] == "-arch" && i + 1 < args.Length)
			{
				string v = args[i + 1].ToLowerInvariant();
				if(v == "arm64") arch = ArchARM64;
				else if(v == "x64" || v == "x86_64") arch = ArchX64;
				else if(v == "universal") arch = ArchUniversal;
			}
			else if(args[i] == "-outDir" && i + 1 < args.Length)
			{
				outDir = args[i + 1];
			}
		}
		Run(arch, outDir);
	}

	static void Run(int arch, string outDir = null)
	{
		if(string.IsNullOrEmpty(outDir))
			outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "Mac");
		Directory.CreateDirectory(outDir);

		string appName = SanitizeFileName(PlayerSettings.productName) + ".app";
		string outPath = Path.Combine(outDir, appName);

		PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, arch);

		string[] scenes = EditorBuildSettings.scenes
			.Where(s => s.enabled)
			.Select(s => s.path)
			.ToArray();

		BuildPlayerOptions options = new BuildPlayerOptions
		{
			scenes = scenes,
			locationPathName = outPath,
			target = BuildTarget.StandaloneOSX,
			targetGroup = BuildTargetGroup.Standalone,
			options = BuildOptions.None,
		};

		BuildReport report = BuildPipeline.BuildPlayer(options);
		BuildSummary summary = report.summary;
		Debug.Log($"[MacBuildScript] Result={summary.result}, Output={outPath}, Size={summary.totalSize}, Time={summary.totalTime}");

		if(Application.isBatchMode)
			EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
	}

	static string SanitizeFileName(string name)
	{
		foreach(char c in Path.GetInvalidFileNameChars())
			name = name.Replace(c, '_');
		return name;
	}
}

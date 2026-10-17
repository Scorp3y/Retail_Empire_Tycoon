using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds a disposable real-game fixture, never a replacement for the shipping scenes.</summary>
[InitializeOnLoad]
public static class TownPerformanceBatch
{
    private const string Running = "RetailEmpire.Performance.Running";
    private const string Output = "RetailEmpire.Performance.Output";
    private const string Deadline = "RetailEmpire.Performance.Deadline";
    static TownPerformanceBatch() { EditorApplication.update += CheckEditorMeasurement; }

    public static void MeasureEditor()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-town-profile-out");
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Missing profiling output.");
            string output = Path.GetFullPath(args[index + 1]);
            string expected = Path.GetFullPath("Library/TownPerformanceQA/EditorEvidence") + Path.DirectorySeparatorChar;
            if (!output.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected editor evidence path.");
            CreateFixture();
            PlayerSettings.companyName = "RetailEmpireDisposableQA";
            PlayerSettings.productName = "TownPerformanceFixture";
            PlayerSettings.enableFrameTimingStats = true;
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "PENDING\n");
            SessionState.SetString(Output, output);
            SessionState.SetString(Deadline, DateTime.UtcNow.AddMinutes(5).ToString("O"));
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckEditorMeasurement()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(Running, false)) return;
        string output = SessionState.GetString(Output, "");
        string terminal = Path.Combine(output, "complete.txt");
        string text = File.Exists(terminal) ? File.ReadAllText(terminal) : "PENDING";
        if (text.StartsWith("PASS", StringComparison.Ordinal) || text.StartsWith("FAILED", StringComparison.Ordinal))
        { SessionState.SetBool(Running, false); EditorApplication.Exit(text.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1); }
        else if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(Deadline, ""), null, System.Globalization.DateTimeStyles.RoundtripKind))
        { SessionState.SetBool(Running, false); File.WriteAllText(terminal, "FAILED editor probe timed out.\n"); EditorApplication.Exit(1); }
    }

    public static void Build()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory("Library/TownPerformanceQA");
            File.WriteAllText("Library/TownPerformanceQA/build.txt", "RUNNING\n");
            string fixture = CreateFixture();
            // Separate identity also isolates any remaining preference reads/writes from the actual game.
            PlayerSettings.companyName = "RetailEmpireDisposableQA";
            PlayerSettings.productName = "TownPerformanceFixture";
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { fixture, "Assets/Project/Scenes/Loading.unity", CityUpdateSetup.CityPath },
                locationPathName = "Builds/TownPerformancePlayer/TownBenchmark.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report == null || report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows player build failed: " + (report == null ? "no report" : report.summary.result.ToString()));
            File.WriteAllText("Library/TownPerformanceQA/build.txt", "PASS Windows64 Development player; original Game/City/Loading scene assets were not modified.\nBytes=" + report.summary.totalSize);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Library/TownPerformanceQA");
            File.WriteAllText("Library/TownPerformanceQA/build.txt", "FAILED\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private static string CreateFixture()
    {
        string folder = "Assets/Art/TownPerformanceFixture";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        string fixture = folder + "/GameBenchmark.unity";
        EditorSceneManager.SaveScene(scene, fixture, true);
        scene = EditorSceneManager.OpenScene(fixture);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<SaveManager>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<TutorialController>(true))
        {
            if (component.tutorialChoiceUI != null) component.tutorialChoiceUI.SetActive(false);
            if (component.tutorialUI != null) component.tutorialUI.SetActive(false);
            if (component.characterRoot != null) component.characterRoot.SetActive(false);
            UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (var component in UnityEngine.Object.FindObjectsOfType<TutorialDialogue>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<ButtonFadeIn>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in UnityEngine.Object.FindObjectsOfType<AvatarSpawn>(true)) component.enabled = false;
        foreach (var component in UnityEngine.Object.FindObjectsOfType<ScreenFader>(true)) component.SetInstant(0);
        EditorSceneManager.SaveScene(scene);
        return fixture;
    }
}

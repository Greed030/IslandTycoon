using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IslandTycoon.EditorTools
{
    /// <summary>
    /// Editor helpers:
    ///  - menu "Island Tycoon" (setup, self test, reset progress);
    ///  - writes compile errors and console output to Logs/claude_*.txt;
    ///  - executes commands written to Logs/claude_cmd.txt (refresh, play, stop, selftest, shot...)
    ///    so the project can be driven without clicking in the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class IslandTycoonTools
    {
        static readonly string LogDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        static string P(string f) => Path.Combine(LogDir, f);
        static double _nextPoll;
        const string ScenePath = "Assets/IslandTycoon/IslandTycoon.unity";
        const string ConfigPath = "Assets/IslandTycoon/Resources/IslandTycoon/GameConfig.asset";

        static IslandTycoonTools()
        {
            Directory.CreateDirectory(LogDir);
            CompilationPipeline.compilationStarted += _ => Write("claude_compile.txt", $"COMPILING {DateTime.Now:HH:mm:ss}\n", false);
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += _ => Write("claude_compile.txt", $"FINISHED {DateTime.Now:HH:mm:ss}\n", true);
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += s => Status("playmode " + s);
            Status($"READY {DateTime.Now:HH:mm:ss} unity {Application.unityVersion} gameAssembly={(GameType("IslandTycoon.GameModel") != null)}");
            EditorApplication.delayCall += AutoSetup;
        }

        // ------------------------------------------------------------------ logging

        static void Write(string file, string text, bool append)
        {
            try { if (append) File.AppendAllText(P(file), text); else File.WriteAllText(P(file), text); } catch { }
        }

        static void Status(string s) => Write("claude_status.txt", $"[{DateTime.Now:HH:mm:ss}] {s}\n", true);

        static void OnAssemblyCompiled(string asm, CompilerMessage[] msgs)
        {
            var sb = new System.Text.StringBuilder();
            int errors = 0;
            foreach (var m in msgs)
            {
                if (m.type == CompilerMessageType.Error) errors++;
                if (m.type == CompilerMessageType.Error || m.message.Contains("warning CS0") == false)
                    sb.AppendLine($"{m.type}: {m.file}({m.line}): {m.message}");
            }
            Write("claude_compile.txt", $"{Path.GetFileName(asm)}: {errors} error(s)\n{sb}", true);
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            try
            {
                var f = P("claude_console.txt");
                if (File.Exists(f) && new FileInfo(f).Length > 2_000_000) File.WriteAllText(f, "");
                string st = type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? "\n" + stack : "";
                File.AppendAllText(f, $"[{DateTime.Now:HH:mm:ss}] {type}: {msg}{st}\n");
            }
            catch { }
        }

        // ------------------------------------------------------------------ remote commands

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 0.5;
            var f = P("claude_cmd.txt");
            if (!File.Exists(f)) return;
            string[] cmds;
            try { cmds = File.ReadAllLines(f); File.Delete(f); } catch { return; }
            foreach (var raw in cmds)
            {
                var cmd = raw.Trim();
                if (cmd.Length == 0) continue;
                Status("cmd " + cmd);
                try { Run(cmd); }
                catch (Exception e) { Status("cmd failed: " + e.Message); }
            }
        }

        static void Run(string cmd)
        {
            if (cmd == "refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            else if (cmd == "play") { OpenGameScene(); EditorApplication.isPlaying = true; }
            else if (cmd == "stop") EditorApplication.isPlaying = false;
            else if (cmd == "selftest") SelfTest();
            else if (cmd == "reset") ResetProgress();
            else if (cmd == "setup") Setup();
            else if (cmd == "reimport") ReimportSprites();
            else if (cmd == "clearlog") Write("claude_console.txt", "", false);
            else if (cmd.StartsWith("shot"))
            {
                var path = P("claude_shot.png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                Status("screenshot requested");
            }
            else if (cmd.StartsWith("menu:")) Status("menu " + EditorApplication.ExecuteMenuItem(cmd.Substring(5)));
            else if (cmd.StartsWith("game:"))
            {
                var t = GameType("IslandTycoon.GameController");
                var r = t?.GetMethod("DebugCommand", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { cmd.Substring(5) });
                Status("game " + cmd.Substring(5) + " -> " + r);
            }
            else Status("unknown command " + cmd);
        }

        // ------------------------------------------------------------------ setup

        static Type GameType(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Setup();
        }

        [MenuItem("Island Tycoon/Setup Project", false, 0)]
        public static void Setup()
        {
            // landscape mobile (NFR-01)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // tunable config asset (NFR-13)
            var cfgType = GameType("IslandTycoon.GameConfig");
            if (cfgType != null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
                var fresh = ScriptableObject.CreateInstance(cfgType);
                var vf = cfgType.GetField("configVersion");
                if (existing != null && vf != null && (int)vf.GetValue(existing) < (int)vf.GetValue(fresh))
                {
                    AssetDatabase.DeleteAsset(ConfigPath);
                    existing = null;
                    Status("config outdated, re-creating");
                }
                UnityEngine.Object.DestroyImmediate(fresh);
                if (existing != null) cfgType = null;
            }
            if (cfgType != null)
            {
                var so = ScriptableObject.CreateInstance(cfgType);
                AssetDatabase.CreateAsset(so, ConfigPath);
                AssetDatabase.SaveAssets();
                Status("created " + ConfigPath);
            }

            // game scene in build settings
            if (!File.Exists(ScenePath))
            {
                if (EditorApplication.isPlaying) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Status("created " + ScenePath);
            }
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Status("setup done");
        }

        static void OpenGameScene()
        {
            if (!File.Exists(ScenePath)) Setup();
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath && File.Exists(ScenePath))
            {
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("Island Tycoon/Play", false, 1)]
        static void PlayMenu() { OpenGameScene(); EditorApplication.isPlaying = true; }

        [MenuItem("Island Tycoon/Run Self Test", false, 20)]
        public static void SelfTest()
        {
            var t = GameType("IslandTycoon.SelfTest");
            if (t == null) { Status("selftest: game assembly not loaded (compile errors?)"); return; }
            var report = (string)t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Write("claude_selftest.txt", report, false);
            Debug.Log("[IslandTycoon] Self test\n" + report);
            Status("selftest done");
        }

        [MenuItem("Island Tycoon/Reset Progress", false, 40)]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("island_tycoon_save_v2");
            PlayerPrefs.Save();
            Status("progress reset");
            Debug.Log("[IslandTycoon] Progress reset");
        }

        [MenuItem("Island Tycoon/Reimport Sprites", false, 41)]
        public static void ReimportSprites()
        {
            AssetDatabase.ImportAsset(IslandSpriteImporter.Root.TrimEnd('/'), ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Status("sprites reimported");
        }
    }
}

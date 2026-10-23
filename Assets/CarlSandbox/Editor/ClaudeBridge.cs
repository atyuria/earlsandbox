#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Carl.EditorTools
{
    /// <summary>
    /// A tiny file-based remote control for the editor, so tools outside Unity can drive it without the editor
    /// having focus. Write one command per line to Temp/claude/cmd.txt; the result appears in Temp/claude/out.txt
    /// (with the same id) once done.
    ///   id|refresh                       reimport changed files and recompile
    ///   id|play  /  id|stop
    ///   id|call|Namespace.Type.Method|arg1|arg2   a public static method (string/int/float/bool args); its return value is written back
    ///   id|set|Namespace.Type.field|value         a public static field
    ///   id|log|N                          the last N console lines (errors and warnings marked)
    ///   id|errors                         compile errors from the last compile
    ///   id|status
    ///   id|build|C:/path/Game.exe         Windows 64 player of the scenes in the build list
    /// </summary>
    [InitializeOnLoad]
    public static class ClaudeBridge
    {
        static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "claude"));
        static string CmdFile => Path.Combine(Dir, "cmd.txt");
        static string OutFile => Path.Combine(Dir, "out.txt");
        static readonly List<string> log = new List<string>();
        static readonly List<string> compileErrors = new List<string>();
        static double nextPoll;
        static string pendingAfterCompile;

        static ClaudeBridge()
        {
            Directory.CreateDirectory(Dir);
            // keep running (and keep play mode running) while another window has focus
            PlayerSettings.runInBackground = true;
            Application.runInBackground = true;
            EditorApplication.update += Poll;
            Application.logMessageReceivedThreaded += OnLog;
            CompilationPipeline.compilationStarted += _ => compileErrors.Clear();
            CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
            {
                foreach (var m in msgs) if (m.type == CompilerMessageType.Error) lock (compileErrors) compileErrors.Add(m.message);
            };
            pendingAfterCompile = SessionState.GetString("claude.pending", "");
            if (!string.IsNullOrEmpty(pendingAfterCompile))
            {
                SessionState.EraseString("claude.pending");
                Write(pendingAfterCompile, "compiled; errors " + compileErrors.Count + (compileErrors.Count > 0 ? "\n" + string.Join("\n", compileErrors) : ""));
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            string tag = type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? "ERR " : type == LogType.Warning ? "WRN " : "";
            string line = tag + msg + ((type == LogType.Exception) && !string.IsNullOrEmpty(stack) ? "\n    " + stack.Split('\n').FirstOrDefault() : "");
            lock (log) { log.Add(line); if (log.Count > 2000) log.RemoveRange(0, 500); }
        }

        static void Write(string id, string text)
        {
            try { File.AppendAllText(OutFile, "### " + id + "\n" + text + "\n### end " + id + "\n"); } catch { }
        }

        static double refreshAt = -1;

        static void Poll()
        {
            if (refreshAt > 0 && EditorApplication.timeSinceStartup - refreshAt > 4 && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                string pid = SessionState.GetString("claude.pending", "");
                refreshAt = -1;
                if (!string.IsNullOrEmpty(pid))
                {
                    SessionState.EraseString("claude.pending");
                    Write(pid, "no reload; errors " + compileErrors.Count + (compileErrors.Count > 0 ? "\n" + string.Join("\n", compileErrors) : ""));
                }
            }
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            if (!File.Exists(CmdFile)) return;
            string[] lines;
            try { lines = File.ReadAllLines(CmdFile); File.Delete(CmdFile); } catch { return; }
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var parts = raw.Split('|');
                string id = parts[0], cmd = parts.Length > 1 ? parts[1] : "";
                try { Run(id, cmd, parts.Skip(2).ToArray()); }
                catch (Exception e) { Write(id, "EXCEPTION " + e); }
            }
        }

        static void Run(string id, string cmd, string[] a)
        {
            switch (cmd)
            {
                case "refresh":
                    if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                    SessionState.SetString("claude.pending", id);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    CompilationPipeline.RequestScriptCompilation();
                    // if nothing needed compiling (or it failed) the domain won't reload: answer once it has settled
                    refreshAt = EditorApplication.timeSinceStartup;
                    break;
                case "play": EditorApplication.isPlaying = true; Write(id, "playing"); break;
                case "stop": EditorApplication.isPlaying = false; Write(id, "stopped"); break;
                case "status":
                    Write(id, $"playing {EditorApplication.isPlaying} compiling {EditorApplication.isCompiling} errors {compileErrors.Count} time {Time.time:0.0} frame {Time.frameCount}");
                    break;
                case "errors": lock (compileErrors) Write(id, string.Join("\n", compileErrors)); break;
                case "log":
                {
                    int n = a.Length > 0 ? int.Parse(a[0]) : 30;
                    lock (log) Write(id, string.Join("\n", log.Skip(Math.Max(0, log.Count - n))));
                    break;
                }
                case "clearlog": lock (log) log.Clear(); Write(id, "cleared"); break;
                case "call": Write(id, Call(a[0], a.Skip(1).ToArray())); break;
                case "set": Write(id, Set(a[0], a[1])); break;
                case "build":
                {
                    var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                    if (scenes.Length == 0) scenes = new[] { "Assets/sigma project.unity" };
                    var rep = BuildPipeline.BuildPlayer(scenes, a[0], BuildTarget.StandaloneWindows64, a.Length > 1 && a[1] == "dev" ? BuildOptions.Development : BuildOptions.None);
                    var errs = rep.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error).Select(m => m.content);
                    Write(id, $"build {rep.summary.result} {rep.summary.totalSize / 1048576f:0.0} MB {rep.summary.totalTime.TotalSeconds:0}s\n" + string.Join("\n", errs));
                    break;
                }
                default: Write(id, "unknown command " + cmd); break;
            }
        }

        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        static object Convert(string s, Type t)
        {
            if (t == typeof(string)) return s;
            if (t == typeof(int)) return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return bool.Parse(s);
            if (t.IsEnum) return Enum.Parse(t, s);
            return null;
        }

        static string Call(string path, string[] args)
        {
            int dot = path.LastIndexOf('.');
            var t = FindType(path.Substring(0, dot));
            if (t == null) return "no type " + path.Substring(0, dot);
            var m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).FirstOrDefault(x => x.Name == path.Substring(dot + 1) && x.GetParameters().Length == args.Length);
            if (m == null) return "no method " + path;
            var ps = m.GetParameters();
            var vals = new object[args.Length];
            for (int i = 0; i < args.Length; i++) vals[i] = Convert(args[i], ps[i].ParameterType);
            var r = m.Invoke(null, vals);
            return r == null ? "ok" : r.ToString();
        }

        static string Set(string path, string value)
        {
            int dot = path.LastIndexOf('.');
            var t = FindType(path.Substring(0, dot));
            if (t == null) return "no type";
            var f = t.GetField(path.Substring(dot + 1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return "no field";
            f.SetValue(null, Convert(value, f.FieldType));
            return "set";
        }
    }
}
#endif

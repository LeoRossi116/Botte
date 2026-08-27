using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 5);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 50;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle":
                    break;
                case "WaitingForCompile":
                    Debug.Log("[PlayModeTest] Bootstrap compiled. Scheduling Play Mode entry.");
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;
                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;
                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                        EditorApplication.update += WaitFramesThenRun;
                    break;
                case "Done":
                    Debug.Log(SentinelLog);
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _hasRun = false;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;
            if (_hasRun) return;
            _hasRun = true;
            EditorApplication.update -= WaitFramesThenRun;

            Application.logMessageReceived += OnLogMessage;
            string resultJson;
            try
            {
                resultJson = RunTestLogic();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayModeTest] Test threw exception: " + e);
                resultJson = JsonUtility.ToJson(new TestResult { success = false, error = e.Message, logs = _capturedLogs.ToArray() });
            }
            finally
            {
                Application.logMessageReceived -= OnLogMessage;
            }

            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
                AssetDatabase.DeleteAsset(scriptPath);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            if (type == LogType.Error || type == LogType.Exception ||
                message.Contains("[Test]") || message.Contains("TEST_RESULT"))
                _capturedLogs.Add("[" + type + "] " + message);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public bool sceneManagerFound;
            public bool createPanelFound;
            public bool createPanelActive;
            public bool joinPanelFound;
            public bool joinPanelActive;
        }

        private static void Invoke(object inst, string method)
        {
            var m = inst.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            if (m != null) m.Invoke(inst, null);
            else Debug.LogError("[Test] Method not found: " + method);
        }

        private static string RunTestLogic()
        {
            var result = new TestResult();

            // Ensure a profile name exists so ClickedHost/ClickedJoin don't early-out.
            var pmType = System.Type.GetType("PlayerProfileManager, Assembly-CSharp");
            if (pmType != null)
            {
                var currentProp = pmType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var profile = currentProp != null ? currentProp.GetValue(null) : null;
                if (profile != null)
                {
                    var nameField = profile.GetType().GetField("profileName");
                    if (nameField != null)
                    {
                        var cur = nameField.GetValue(profile) as string;
                        if (string.IsNullOrEmpty(cur))
                            nameField.SetValue(profile, "Tester");
                        Debug.Log("[Test] profileName=" + nameField.GetValue(profile));
                    }
                }
            }

            var suiType = System.Type.GetType("SceneUIManager, Assembly-CSharp");
            var sui = suiType != null ? UnityEngine.Object.FindFirstObjectByType(suiType) : null;
            result.sceneManagerFound = sui != null;
            if (sui == null)
            {
                Debug.LogError("[Test] SceneUIManager not found in scene.");
                result.success = false;
                result.error = "SceneUIManager not found";
                result.logs = _capturedLogs.ToArray();
                return JsonUtility.ToJson(result);
            }

            // --- HOST flow: PLAY -> HOST should open CreateLobbyPanel ---
            Invoke(sui, "ClickedPlay");
            Invoke(sui, "ClickedHost");
            var createPanel = GameObject.Find("CreateLobbyPanel");
            result.createPanelFound = createPanel != null;
            result.createPanelActive = createPanel != null && createPanel.activeInHierarchy;
            Debug.Log("[Test] CreateLobbyPanel found=" + result.createPanelFound + " active=" + result.createPanelActive);

            // Verify expected child controls exist on the create page.
            if (createPanel != null)
            {
                int inputs = createPanel.GetComponentsInChildren<TMPro.TMP_InputField>(true).Length;
                int toggles = createPanel.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).Length;
                int buttons = createPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length;
                Debug.Log("[Test] CreatePanel controls inputs=" + inputs + " toggles=" + toggles + " buttons=" + buttons);
            }

            // --- JOIN flow: JOIN should open JoinBrowsePanel ---
            Invoke(sui, "ClickedJoin");
            var joinPanel = GameObject.Find("JoinBrowsePanel");
            result.joinPanelFound = joinPanel != null;
            result.joinPanelActive = joinPanel != null && joinPanel.activeInHierarchy;
            Debug.Log("[Test] JoinBrowsePanel found=" + result.joinPanelFound + " active=" + result.joinPanelActive);

            if (joinPanel != null)
            {
                int inputs = joinPanel.GetComponentsInChildren<TMPro.TMP_InputField>(true).Length;
                int buttons = joinPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length;
                int scrolls = joinPanel.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Length;
                Debug.Log("[Test] JoinPanel controls inputs=" + inputs + " buttons=" + buttons + " scrollRects=" + scrolls);
            }

            result.success = result.sceneManagerFound &&
                             result.createPanelFound && result.createPanelActive &&
                             result.joinPanelFound && result.joinPanelActive;
            result.logs = _capturedLogs.ToArray();
            Debug.Log("TEST_RESULT: success=" + result.success);
            return JsonUtility.ToJson(result);
        }
    }
}

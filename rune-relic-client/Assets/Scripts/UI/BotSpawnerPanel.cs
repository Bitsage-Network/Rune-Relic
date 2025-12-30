using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.Game;

namespace RuneRelic.UI
{
    /// <summary>
    /// Runtime UI panel for bot load testing controls.
    /// </summary>
    public class BotSpawnerPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BotLoadTestSpawner spawner;
        [SerializeField] private Canvas targetCanvas;

        [Header("Panel")]
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private bool createSpawnerIfMissing = true;
        [SerializeField] private Vector2 panelSize = new Vector2(320f, 260f);
        [SerializeField] private Vector2 panelOffset = new Vector2(20f, -20f);
        [SerializeField] private int maxLogLines = 8;

        private RectTransform _panelRoot;
        private Text _statusText;
        private Text _botCountText;
        private Text _logText;
        private Button _startButton;
        private Button _stopButton;

        private readonly Queue<string> _logLines = new Queue<string>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<BotSpawnerPanel>() != null)
            {
                return;
            }

            if (FindObjectOfType<GameManager>() == null)
            {
                return;
            }

            var go = new GameObject("BotSpawnerPanelController");
            go.AddComponent<BotSpawnerPanel>();
        }
#endif

        private void Awake()
        {
            ResolveSpawner();
            if (autoBuild)
            {
                BuildPanel();
            }
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.OnBotLog += HandleBotLog;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.OnBotLog -= HandleBotLog;
            }
        }

        private void Update()
        {
            if (spawner == null)
            {
                return;
            }

            if (_statusText != null)
            {
                string status = spawner.IsSpawning ? "Spawning" : (spawner.ActiveBotCount > 0 ? "Running" : "Stopped");
                _statusText.text = $"Status: {status}";
            }

            if (_botCountText != null)
            {
                _botCountText.text = $"Bots: {spawner.ActiveBotCount}/{spawner.TargetBotCount}";
            }

            if (_startButton != null)
            {
                _startButton.interactable = !spawner.IsSpawning;
            }

            if (_stopButton != null)
            {
                _stopButton.interactable = spawner.IsSpawning || spawner.ActiveBotCount > 0;
            }
        }

        private void ResolveSpawner()
        {
            if (spawner != null)
            {
                return;
            }

            spawner = FindObjectOfType<BotLoadTestSpawner>();
            if (spawner == null && createSpawnerIfMissing)
            {
                var go = new GameObject("BotLoadTestSpawner");
                spawner = go.AddComponent<BotLoadTestSpawner>();
            }
        }

        private void BuildPanel()
        {
            if (_panelRoot != null)
            {
                return;
            }

            Canvas canvas = targetCanvas;
            if (canvas == null)
            {
                canvas = FindCanvas();
            }

            if (canvas == null)
            {
                Debug.LogWarning("[BotSpawnerPanel] No Canvas found for bot UI.");
                return;
            }

            var panel = new GameObject("BotSpawnerPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvas.transform, false);
            _panelRoot = panel.GetComponent<RectTransform>();
            _panelRoot.anchorMin = new Vector2(0f, 1f);
            _panelRoot.anchorMax = new Vector2(0f, 1f);
            _panelRoot.pivot = new Vector2(0f, 1f);
            _panelRoot.anchoredPosition = panelOffset;
            _panelRoot.sizeDelta = panelSize;

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateLabel(panel.transform, "Bot Load Test", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            _statusText = CreateLabel(panel.transform, "Status: Stopped", 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            _botCountText = CreateLabel(panel.transform, "Bots: 0/0", 12, FontStyle.Normal, TextAnchor.MiddleLeft);

            var buttonRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(panel.transform, false);
            var rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;

            _startButton = CreateButton(buttonRow.transform, "Start", StartBots, new Color(0.2f, 0.7f, 0.4f, 0.9f));
            _stopButton = CreateButton(buttonRow.transform, "Stop", StopBots, new Color(0.8f, 0.25f, 0.25f, 0.9f));

            CreateLabel(panel.transform, "Errors", 12, FontStyle.Bold, TextAnchor.MiddleLeft);

            var logBackground = new GameObject("LogBackground", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            logBackground.transform.SetParent(panel.transform, false);
            var logImage = logBackground.GetComponent<Image>();
            logImage.color = new Color(0f, 0f, 0f, 0.35f);
            var logLayout = logBackground.GetComponent<LayoutElement>();
            logLayout.preferredHeight = Mathf.Max(80f, panelSize.y * 0.4f);

            _logText = CreateFillText(logBackground.transform, "No errors yet.", 11, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private Canvas FindCanvas()
        {
            var canvases = FindObjectsOfType<Canvas>();
            if (canvases == null || canvases.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "GameUICanvas")
                {
                    return canvases[i];
                }
            }

            return canvases[0];
        }

        private void StartBots()
        {
            ResolveSpawner();
            if (spawner == null)
            {
                return;
            }

            spawner.StartSpawning();
        }

        private void StopBots()
        {
            if (spawner == null)
            {
                return;
            }

            spawner.StopAllBots();
        }

        private void HandleBotLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _logLines.Enqueue(message);
            while (_logLines.Count > Mathf.Max(1, maxLogLines))
            {
                _logLines.Dequeue();
            }

            UpdateLogText();
        }

        private void UpdateLogText()
        {
            if (_logText == null)
            {
                return;
            }

            if (_logLines.Count == 0)
            {
                _logText.text = "No errors yet.";
                return;
            }

            _logText.text = string.Join("\n", _logLines);
        }

        private Text CreateLabel(Transform parent, string content, int size, FontStyle style, TextAnchor alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = size + 10f;

            return text;
        }

        private Text CreateFillText(Transform parent, string content, int size, FontStyle style, TextAnchor alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 6f);
            rect.offsetMax = new Vector2(-8f, -6f);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.85f, 0.9f, 0.95f, 0.95f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private Button CreateButton(Transform parent, string label, Action onClick, Color color)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = 30f;

            var text = CreateFillText(go.transform, label, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = Color.white;

            return button;
        }
    }
}

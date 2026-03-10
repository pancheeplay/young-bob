using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal sealed class RuntimeConsolePanel
    {
        private const int MaxEntryCount = 100;
        private const int MaxTextLength = 10000;

        private readonly List<string> _entries = new List<string>();
        private readonly GameObject _root;
        private readonly Text _text;
        private readonly Button _openButton;
        private readonly Text _openButtonLabel;
        private bool _isExpanded;

        public RuntimeConsolePanel(Transform parent)
        {
            _openButton = UiFactory.CreateButton(parent, "ConsoleOpenButton", "Console", Vector2.zero, Toggle);
            var openRect = _openButton.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-20f, -20f);
            openRect.sizeDelta = new Vector2(200f, 60f);
            _openButtonLabel = _openButton.GetComponentInChildren<Text>();

            _root = UiFactory.CreatePanel(parent, "ConsolePanel", new Color(0.03f, 0.04f, 0.05f, 0.98f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-20f, -90f);
            rootRect.sizeDelta = new Vector2(560f, 720f);
            
            // Create Scroll View
            var scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(_root.transform, false);
            var scrollRect = scrollView.AddComponent<ScrollRect>();
            var scrollViewRect = scrollView.GetComponent<RectTransform>();
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.offsetMin = new Vector2(20f, 20f);
            scrollViewRect.offsetMax = new Vector2(-20f, -100f);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            viewport.AddComponent<RectMask2D>();
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            // ScrollRect needs an image on the viewport or content to catch drag events
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;

            _text = UiFactory.CreateText(viewport.transform, "ConsoleText", 20, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _text.supportRichText = true;
            var textRect = _text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            
            var fitter = _text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = textRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            _isExpanded = false;
            ApplyVisibility();
        }

        public void Append(string message)
        {
            _entries.Add(FormatEntry(message));
            if (_entries.Count > MaxEntryCount)
            {
                _entries.RemoveAt(0);
            }

            var text = string.Join("\n\n", _entries);
            if (text.Length > MaxTextLength)
            {
                text = "...\n\n" + text.Substring(text.Length - MaxTextLength);
            }
            
            _text.text = text;
            _root.transform.SetAsLastSibling();
            _openButton.transform.SetAsLastSibling();
        }

        private static string FormatEntry(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "<color=#7D8894>[--:--:--][Info]</color> <color=#D7DEE5>(empty log)</color>";
            }

            var normalizedMessage = message.Replace("\r\n", "\n").Trim();
            var lines = normalizedMessage.Split('\n');
            var headerLine = lines[0].Trim();
            var detailLines = lines.Length > 1 ? lines : null;

            var level = "Info";
            var levelColor = "#9CDCFE";
            if (headerLine.StartsWith("[Warning]"))
            {
                level = "Warning";
                levelColor = "#F2C94C";
                headerLine = headerLine.Substring("[Warning]".Length).TrimStart();
            }
            else if (headerLine.StartsWith("[Error]"))
            {
                level = "Error";
                levelColor = "#FF7B72";
                headerLine = headerLine.Substring("[Error]".Length).TrimStart();
            }
            else if (headerLine.StartsWith("[Exception]"))
            {
                level = "Exception";
                levelColor = "#FF6B6B";
                headerLine = headerLine.Substring("[Exception]".Length).TrimStart();
            }
            else if (headerLine.StartsWith("[Assert]"))
            {
                level = "Assert";
                levelColor = "#FF9E64";
                headerLine = headerLine.Substring("[Assert]".Length).TrimStart();
            }
            else if (headerLine.StartsWith("[Log]"))
            {
                headerLine = headerLine.Substring("[Log]".Length).TrimStart();
            }

            var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            var builder = new StringBuilder();
            builder.Append("<color=#7D8894>[");
            builder.Append(timestamp);
            builder.Append("]</color> ");
            builder.Append("<color=");
            builder.Append(levelColor);
            builder.Append(">[");
            builder.Append(level);
            builder.Append("]</color> ");
            builder.Append("<color=#F5F7FA>");
            builder.Append(EscapeRichText(headerLine));
            builder.Append("</color>");

            if (detailLines != null)
            {
                for (var index = 1; index < lines.Length; index++)
                {
                    var detail = lines[index].TrimEnd();
                    if (string.IsNullOrEmpty(detail))
                    {
                        continue;
                    }

                    builder.Append("\n<color=#8B949E>    ");
                    builder.Append(EscapeRichText(detail));
                    builder.Append("</color>");
                }
            }

            return builder.ToString();
        }

        private static string EscapeRichText(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void Toggle()
        {
            _isExpanded = !_isExpanded;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            _root.SetActive(_isExpanded);
            _openButtonLabel.text = _isExpanded ? "Console ▲" : "Console ▼";
            _root.transform.SetAsLastSibling();
            _openButton.transform.SetAsLastSibling();
        }
    }
}

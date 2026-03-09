using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal sealed class RuntimeConsolePanel
    {
        private readonly List<string> _entries = new List<string>();
        private readonly GameObject _root;
        private readonly Text _text;
        private readonly Button _openButton;
        private readonly Button _closeButton;
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

            _root = UiFactory.CreatePanel(parent, "ConsolePanel", new Color(0.03f, 0.04f, 0.05f, 0.98f), new Vector2(0f, 0f), new Vector2(1f, 0.6f), new Vector2(20f, 120f), new Vector2(-20f, -200f));
            
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

            _closeButton = UiFactory.CreateButton(_root.transform, "ConsoleToggle", "Hide Console", new Vector2(0f, -60f), Toggle);
            var closeRect = _closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 1f);
            closeRect.anchorMax = new Vector2(0.5f, 1f);
            closeRect.pivot = new Vector2(0.5f, 1f);
            closeRect.sizeDelta = new Vector2(300f, 60f);

            _isExpanded = false;
            ApplyVisibility();
        }

        public void Append(string message)
        {
            _entries.Add(message);
            if (_entries.Count > 100)
            {
                _entries.RemoveAt(0);
            }

            var text = string.Join("\n", _entries);
            if (text.Length > 10000)
            {
                text = "..." + text.Substring(text.Length - 10000);
            }
            
            _text.text = text;
            _root.transform.SetAsLastSibling();
            _openButton.transform.SetAsLastSibling();
        }

        private void Toggle()
        {
            _isExpanded = !_isExpanded;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            _root.SetActive(_isExpanded);
            _openButton.gameObject.SetActive(!_isExpanded);
            _root.transform.SetAsLastSibling();
            _openButton.transform.SetAsLastSibling();
        }
    }
}

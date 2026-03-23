using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal sealed class SelectionPopupView
    {
        internal sealed class Item
        {
            public string id;
            public string title;
            public string detail;
            public bool isSelected;
            public bool interactable = true;
            public Action onSelected;
        }

        private readonly GameObject _mask;
        private readonly Text _titleText;
        private readonly Text _detailText;
        private readonly RectTransform _windowRect;
        private readonly RectTransform _listPanelRect;
        private readonly Transform _listContent;
        private readonly RectTransform _listContentRect;
        private readonly ScrollRect _listScroll;

        public event Action Closed;

        public bool IsOpen
        {
            get { return _mask != null && _mask.activeSelf; }
        }

        public SelectionPopupView(Transform parent, string namePrefix)
        {
            _mask = UiFactory.CreatePanel(parent, namePrefix + "_Mask", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var popupWindow = UiFactory.CreatePanel(_mask.transform, namePrefix + "_Window", new Color(0.12f, 0.16f, 0.22f, 1f), new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
            _windowRect = popupWindow.GetComponent<RectTransform>();

            _titleText = UiFactory.CreateText(popupWindow.transform, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.86f), new Vector2(0.75f, 1f), new Vector2(24f, 0f), Vector2.zero);
            _titleText.fontStyle = FontStyle.Bold;

            _detailText = UiFactory.CreateText(popupWindow.transform, "Detail", 16, TextAnchor.MiddleLeft, new Vector2(0f, 0.74f), new Vector2(1f, 0.86f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
            _detailText.color = new Color(0.84f, 0.88f, 0.94f, 0.95f);

            var closeButton = UiFactory.CreateButton(popupWindow.transform, "Close", "关闭", Hide);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.78f, 0.86f);
            closeRect.anchorMax = new Vector2(0.98f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            closeButton.GetComponent<Image>().color = new Color(0.3f, 0.22f, 0.2f, 0.95f);

            _listScroll = CreateListContent(popupWindow.transform, namePrefix + "_List", out _listPanelRect, out _listContentRect);
            _listContent = _listContentRect.transform;
            _mask.SetActive(false);
        }

        public void Show(string title, string detail, IReadOnlyList<Item> items, string emptyMessage)
        {
            Debug.Log("[SelectionPopupView] Show title=" + (title ?? string.Empty) + " itemCount=" + (items == null ? 0 : items.Count));
            _titleText.text = title ?? string.Empty;
            _detailText.text = detail ?? string.Empty;

            ClearList();
            if (items == null || items.Count == 0)
            {
                var empty = UiFactory.CreateText(_listContent, "Empty", 20, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, 0f));
                empty.text = string.IsNullOrWhiteSpace(emptyMessage) ? "没有可选项" : emptyMessage;
                _listContentRect.sizeDelta = new Vector2(0f, 120f);
                SetWindowHeightByContent(120f);
            }
            else
            {
                var y = 8f;
                for (var i = 0; i < items.Count; i++)
                {
                    y += BuildItemRow(items[i], i, y) + 8f;
                }

                _listContentRect.sizeDelta = new Vector2(0f, y);
                SetWindowHeightByContent(y);
            }

            Canvas.ForceUpdateCanvases();
            _listScroll.verticalNormalizedPosition = 1f;
            _mask.SetActive(true);
        }

        public void Hide()
        {
            _mask.SetActive(false);
            ClearList();
            Closed?.Invoke();
        }

        private void SetWindowHeightByContent(float contentHeight)
        {
            if (_windowRect == null || _listPanelRect == null)
            {
                return;
            }

            // Title + detail + paddings + content
            const float baseHeight = 180f;
            var desired = baseHeight + Mathf.Max(120f, contentHeight);
            var clamped = Mathf.Clamp(desired, 360f, 1120f);
            _windowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, clamped);

            // Keep list panel filling remaining lower area.
            _listPanelRect.anchorMin = new Vector2(0f, 0f);
            _listPanelRect.anchorMax = new Vector2(1f, 0.74f);
            _listPanelRect.offsetMin = new Vector2(20f, 20f);
            _listPanelRect.offsetMax = new Vector2(-20f, -12f);
        }

        private float BuildItemRow(Item item, int index, float yTop)
        {
            if (item == null)
            {
                return 0f;
            }

            var root = new GameObject("Item_" + index);
            root.transform.SetParent(_listContent, false);
            var rootRect = root.AddComponent<RectTransform>();
            var rowHeight = string.IsNullOrWhiteSpace(item.detail) ? 70f : 108f;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.offsetMin = new Vector2(8f, -yTop - rowHeight);
            rootRect.offsetMax = new Vector2(-8f, -yTop);

            var bg = root.AddComponent<Image>();
            bg.color = item.isSelected ? new Color(0.18f, 0.45f, 0.34f, 0.95f) : new Color(0.2f, 0.28f, 0.42f, 0.95f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.interactable = item.interactable;
            if (item.interactable && item.onSelected != null)
            {
                button.onClick.AddListener(() => item.onSelected());
            }

            if (!item.interactable)
            {
                bg.color = new Color(0.24f, 0.24f, 0.26f, 0.95f);
            }

            var selector = UiFactory.CreateText(root.transform, "Selector", 26, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(0.12f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            selector.text = item.isSelected ? "●" : "○";
            selector.color = item.isSelected ? new Color(1f, 0.95f, 0.72f, 1f) : new Color(0.75f, 0.8f, 0.86f, 0.9f);
            selector.fontStyle = FontStyle.Bold;

            var title = UiFactory.CreateText(root.transform, "Title", 18, TextAnchor.UpperLeft, new Vector2(0.12f, 0.45f), new Vector2(0.82f, 1f), new Vector2(8f, -4f), new Vector2(-4f, -8f));
            title.text = item.title ?? string.Empty;
            title.fontStyle = FontStyle.Bold;

            var detail = UiFactory.CreateText(root.transform, "Detail", 15, TextAnchor.UpperLeft, new Vector2(0.12f, 0f), new Vector2(1f, 0.48f), new Vector2(8f, 4f), new Vector2(-16f, -8f));
            detail.text = item.detail ?? string.Empty;
            detail.color = new Color(0.9f, 0.93f, 0.97f, 0.95f);
            detail.gameObject.SetActive(!string.IsNullOrWhiteSpace(item.detail));

            var mark = UiFactory.CreateText(root.transform, "Mark", 16, TextAnchor.MiddleRight, new Vector2(0.72f, 0.48f), new Vector2(0.98f, 1f), new Vector2(0f, -6f), new Vector2(0f, -10f));
            mark.text = item.isSelected ? "已选" : "";
            mark.color = new Color(1f, 0.95f, 0.72f, 1f);
            mark.fontStyle = FontStyle.Bold;
            return rowHeight;
        }

        private ScrollRect CreateListContent(Transform parent, string name, out RectTransform panelRect, out RectTransform contentRect)
        {
            var list = UiFactory.CreatePanel(parent, name + "_Panel", new Color(0.08f, 0.1f, 0.13f, 0.85f), new Vector2(0f, 0f), new Vector2(1f, 0.74f), new Vector2(20f, 20f), new Vector2(-20f, -12f));
            panelRect = list.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0.5f);

            var scroll = list.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewport = new GameObject(name + "_Viewport");
            viewport.transform.SetParent(list.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(6f, 6f);
            viewportRect.offsetMax = new Vector2(-6f, -6f);
            viewport.AddComponent<RectMask2D>();
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;

            var content = new GameObject(name + "_Content");
            content.transform.SetParent(viewport.transform, false);
            contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return scroll;
        }

        private void ClearList()
        {
            if (_listContent == null)
            {
                return;
            }

            for (var i = _listContent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_listContent.GetChild(i).gameObject);
            }
        }
    }
}

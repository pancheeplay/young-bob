using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleStageSurfaceView
    {
        public RectTransform SurfaceRect { get; private set; }
        public Transform BoardHost { get; private set; }
        public Transform LogHost { get; private set; }

        public BattleStageSurfaceView(Transform parent)
        {
            var surface = UiFactory.CreatePanel(parent, "BattleStageSurface", new Color(0.04f, 0.06f, 0.09f, 0.96f), new Vector2(0f, 0.25f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);
            SurfaceRect = surface.GetComponent<RectTransform>();
            var surfaceImage = surface.GetComponent<Image>();
            surfaceImage.type = Image.Type.Sliced;

            var surfaceGlow = UiFactory.CreatePanel(surface.transform, "SurfaceGlow", new Color(0.14f, 0.2f, 0.26f, 0.12f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            surfaceGlow.transform.SetAsFirstSibling();
            surfaceGlow.GetComponent<Image>().raycastTarget = false;

            var boardFrame = UiFactory.CreatePanel(surface.transform, "BoardFrame", Color.clear, new Vector2(0f, 0.4f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            boardFrame.GetComponent<Image>().raycastTarget = false;
            BoardHost = boardFrame.transform;

            var logFrame = UiFactory.CreatePanel(surface.transform, "LogFrame", Color.clear, new Vector2(0f, 0f), new Vector2(1f, 0.4f), new Vector2(6f, 2f), new Vector2(-6f, -6f));
            logFrame.GetComponent<Image>().raycastTarget = false;
            LogHost = logFrame.transform;

            // var divider = UiFactory.CreatePanel(surface.transform, "Divider", new Color(0.55f, 0.65f, 0.75f, 0.12f), new Vector2(0.04f, 0.41f), new Vector2(0.96f, 0.414f), Vector2.zero, Vector2.zero);
            // divider.GetComponent<Image>().raycastTarget = false;
        }
    }
}

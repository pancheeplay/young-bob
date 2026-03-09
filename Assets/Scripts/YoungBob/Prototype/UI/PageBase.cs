using UnityEngine;
using YoungBob.Prototype.App;

namespace YoungBob.Prototype.UI
{
    internal abstract class PageBase
    {
        protected readonly GameObject Root;
        protected readonly PrototypeSessionController Session;

        protected PageBase(Transform parent, string name, PrototypeSessionController session, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            Session = session;
            Root = UiFactory.CreatePanel(parent, name, color, anchorMin, anchorMax, new Vector2(0f, 0f), new Vector2(0f, 0f));
        }


        public void Show()
        {
            Root.SetActive(true);
        }

        public void Hide()
        {
            Root.SetActive(false);
        }
    }
}

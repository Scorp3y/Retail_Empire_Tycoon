using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    public sealed class BuildEditorHud : MonoBehaviour
    {
        public BuildEditingController editor;
        public Transform uiRoot;
        public Transform warehouseWindow;
        private RectTransform toolbar;
        private TMP_Text title, notice;
        private Button confirm, left, right, remove, cancel;
        private void Start()
        {
            var ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var entry = ui.Button(warehouseWindow, "Редактировать магазин", "Edit store", Vector2.zero, new Vector2(580, 38), editor.Begin);
            var entryRect = (RectTransform)entry.transform;
            entryRect.anchorMin = entryRect.anchorMax = new Vector2(.5f, 0);
            entryRect.pivot = new Vector2(.5f, 0);
            entryRect.anchoredPosition = new Vector2(0, 44);
            entryRect.sizeDelta = new Vector2(-36, 38);
            entryRect.anchorMin = new Vector2(0, 0); entryRect.anchorMax = new Vector2(1, 0);
            toolbar = ui.Rect("Store editing toolbar", uiRoot, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(850, 136));
            ui.Panel(toolbar, ShopUiTheme.Paper); ui.Border(toolbar);
            toolbar.gameObject.AddComponent<ShopPanelFit>().MaximumSize = new Vector2(850, 136);
            title = ui.Label(toolbar, "", new Vector2(16, -9), new Vector2(816, 27), 20);
            notice = ui.Label(toolbar, "", new Vector2(16, -38), new Vector2(816, 43), 15);
            left = ui.Button(toolbar, "-90", new Vector2(16, -91), new Vector2(46, 34), () => editor.Rotate(-1));
            right = ui.Button(toolbar, "+90", new Vector2(68, -91), new Vector2(46, 34), () => editor.Rotate(1));
            confirm = ui.Button(toolbar, "Применить", "Apply", new Vector2(126, -91), new Vector2(150, 34), () => editor.Confirm());
            remove = ui.Button(toolbar, "На склад", "Return to storage", new Vector2(286, -91), new Vector2(174, 34), () => editor.DeleteSelected());
            cancel = ui.Button(toolbar, "Отмена", "Cancel selection", new Vector2(470, -91), new Vector2(180, 34), editor.CancelSelection);
            ui.Button(toolbar, "Готово", "Done", new Vector2(660, -91), new Vector2(174, 34), editor.Finish);
            toolbar.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (toolbar == null) return;
            toolbar.gameObject.SetActive(editor.IsActive);
            if (!editor.IsActive) return;
            bool selected = editor.Selected != null;
            title.text = selected ? ShopText.Item(editor.Selected.item) : ShopText.Get("Редактирование магазина", "Store editing");
            notice.text = selected && !editor.CandidateResult.ok ? BuildPreview.FailureText(editor.CandidateResult) : editor.Notice;
            confirm.interactable = selected && editor.CandidateResult.ok;
            left.interactable = right.interactable = selected && editor.Selected.item.allowRotation;
            remove.interactable = cancel.interactable = selected;
        }
        private void OnDestroy() { if (toolbar != null) Destroy(toolbar.gameObject); }
    }
}

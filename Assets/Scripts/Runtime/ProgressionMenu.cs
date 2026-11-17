using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    // Extends the existing menus with copies of their own styled controls.
    public sealed class ProgressionMenu : MonoBehaviour
    {
        private readonly List<Button> levels = new List<Button>();
        private readonly string[] scenes = { "MainLevel", "Level2", "Level3", "Level4" };
        private readonly string[] names = { "GREEN VALLEY", "DESERT CANYON", "DARK CASTLE", "SHADOW KING" };
        private GameObject confirmation;
        private Text progress;
        private Text message;
        private CanvasGroup settingsGroup;
        private Button resetButton;
        private Button cancelButton;
        public bool ConfirmationOpen => confirmation != null && confirmation.activeSelf;
        public void CloseConfirmation()
        {
            if (confirmation != null) confirmation.SetActive(false);
            if (settingsGroup != null) settingsGroup.interactable = true;
            if (EventSystem.current != null && resetButton != null) EventSystem.current.SetSelectedGameObject(resetButton.gameObject);
        }

        public void Build(GameObject stages, GameObject settings, GameMenuController controller)
        {
            if (stages != null)
            {
                var panel = (RectTransform)stages.transform;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 900);
                var template = panel.Find("Campaign").GetComponent<Button>();
                template.gameObject.SetActive(false);
                for (int i = 0; i < scenes.Length; i++)
                {
                    string scene = scenes[i];
                    levels.Add(CopyButton(template, panel, names[i], -210 - i * 110, () => controller.LoadLevel(scene)));
                }
                ((RectTransform)panel.Find("Enemy AI")).anchoredPosition = new Vector2(0, -650);
                ((RectTransform)panel.Find("Back")).anchoredPosition = new Vector2(0, -790);
                progress = CopyLabel(panel, "Progress", new Vector2(0, -130), new Vector2(680, 50), 24);
            }
            if (settings != null)
            {
                var panel = (RectTransform)settings.transform;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 830);
                var back = panel.Find("Back").GetComponent<Button>();
                ((RectTransform)back.transform).anchoredPosition = new Vector2(0, -740);
                settingsGroup = panel.GetComponent<CanvasGroup>();
                if (settingsGroup == null) settingsGroup = panel.gameObject.AddComponent<CanvasGroup>();
                resetButton = CopyButton(back, panel, "RESET PROGRESS", -630, () => {
                    message.text = "Reset all progress and settings?";
                    confirmation.SetActive(true);
                    settingsGroup.interactable = false;
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
                });
                confirmation = new GameObject("Reset Progress Confirmation", typeof(RectTransform), typeof(Image));
                var overlay = (RectTransform)confirmation.transform;
                overlay.SetParent(transform, false);
                overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
                overlay.offsetMin = overlay.offsetMax = Vector2.zero;
                confirmation.GetComponent<Image>().color = new Color(0.015f, 0.03f, 0.07f, 0.98f);
                var box = new GameObject("Confirmation Panel", typeof(RectTransform), typeof(MenuPanelFit));
                var rect = (RectTransform)box.transform;
                rect.SetParent(overlay, false); rect.sizeDelta = new Vector2(740, 500);
                message = CopyLabel(rect, "Message", new Vector2(0, -90), new Vector2(680, 120), 30);
                cancelButton = CopyButton(back, rect, "CANCEL", -280, CloseConfirmation);
                CopyButton(back, rect, "CONFIRM RESET", -390, () => {
                    if (!SaveManager.Instance.Delete()) { message.text = "Reset failed. Check file permissions and retry."; return; }
                    CloseConfirmation();
                    // Leave any active run so its completion cannot restore reset progress.
                    controller.LoadLevel("MainMenu");
                });
                confirmation.SetActive(false);
            }
            SaveManager.Instance.Changed += Refresh;
            Refresh();
        }

        private static Button CopyButton(Button template, Transform parent, string label, float y, UnityAction action)
        {
            var button = Instantiate(template, parent);
            button.name = label;
            var menuAction = button.GetComponent<MenuButtonAction>();
            if (menuAction != null) { menuAction.enabled = false; Destroy(menuAction); }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(0, y);
            button.GetComponentInChildren<Text>(true).text = label;
            button.gameObject.SetActive(true);
            return button;
        }
        private static Text CopyLabel(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(255, 211, 103, 255); text.raycastTarget = false;
            return text;
        }
        private void Refresh()
        {
            var data = SaveManager.Instance.Data;
            for (int i = 0; i < levels.Count; i++)
            {
                levels[i].interactable = SaveManager.Instance.IsUnlocked(scenes[i]);
                levels[i].GetComponentInChildren<Text>().text = names[i] +
                    (!levels[i].interactable ? " — LOCKED" : data.completedLevels.Contains(scenes[i]) ? " — COMPLETE" : "");
            }
            if (progress != null) progress.text = $"BEST {data.bestScore}    TOTAL COINS {data.totalCoins}";
        }
        private void OnDestroy() { if (SaveManager.Instance != null) SaveManager.Instance.Changed -= Refresh; }
    }
}

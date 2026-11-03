using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonAction : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameMenuController controller = null;
        [SerializeField] private MenuCommand command = MenuCommand.Play;
        private Button button;
        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<GameMenuController>(true);
            button = GetComponent<Button>();
            button.onClick.AddListener(Invoke);
        }
        private void OnDisable() { if (button != null) button.onClick.RemoveListener(Invoke); }
        private void Invoke()
        {
            AudioManager.Instance?.Play(GameSound.Click);
            controller?.Execute(command);
        }
        public void OnCancel(BaseEventData eventData)
        {
            AudioManager.Instance?.Play(GameSound.Click);
            controller?.Execute(MenuCommand.Back);
        }
    }
}

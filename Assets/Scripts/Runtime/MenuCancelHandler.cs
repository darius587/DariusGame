using UnityEngine;
using UnityEngine.EventSystems;

namespace Zhuzbai.Platformer
{
    public sealed class MenuCancelHandler : MonoBehaviour, ICancelHandler
    {
        public void OnCancel(BaseEventData eventData)
        {
            AudioManager.Instance?.Play(GameSound.Click);
            GetComponentInParent<GameMenuController>(true)?.Execute(MenuCommand.Back);
        }
    }
}

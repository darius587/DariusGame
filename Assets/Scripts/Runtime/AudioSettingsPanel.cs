using UnityEngine;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider sfxSlider = null;
        [SerializeField] private Slider musicSlider = null;
        [SerializeField] private Toggle fullscreen = null;
        [SerializeField] private Text sfxValue = null;
        [SerializeField] private Text musicValue = null;
        private void OnEnable()
        {
            var audio = AudioManager.Instance;
            if (audio == null) { Debug.LogError("AudioManager is missing. Run Apply Game Audio.", this); return; }
            sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            fullscreen.SetIsOnWithoutNotify(audio.Fullscreen);
            RefreshLabels();
            sfxSlider.onValueChanged.AddListener(SetSfx);
            musicSlider.onValueChanged.AddListener(SetMusic);
            fullscreen.onValueChanged.AddListener(SetFullscreen);
        }
        private void OnDisable()
        {
            sfxSlider.onValueChanged.RemoveListener(SetSfx);
            musicSlider.onValueChanged.RemoveListener(SetMusic);
            fullscreen.onValueChanged.RemoveListener(SetFullscreen);
            PlayerPrefs.Save();
        }
        private void SetSfx(float value) { AudioManager.Instance?.SetSfxVolume(value); RefreshLabels(); }
        private void SetMusic(float value) { AudioManager.Instance?.SetMusicVolume(value); RefreshLabels(); }
        private void SetFullscreen(bool value)
        {
            AudioManager.Instance?.Play(GameSound.Click);
            AudioManager.Instance?.SetFullscreen(value);
        }
        private void RefreshLabels()
        {
            sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100) + "%";
            musicValue.text = Mathf.RoundToInt(musicSlider.value * 100) + "%";
        }
    }
}

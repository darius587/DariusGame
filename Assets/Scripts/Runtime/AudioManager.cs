using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zhuzbai.Platformer
{
    public enum GameSound { Coin, Jump, Fall, Shoot, Damage, Click }

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        [SerializeField] private AudioClip music = null;
        [SerializeField] private AudioClip mainMenuMusic = null;
        [SerializeField] private AudioClip levelMusic = null;
        [SerializeField] private AudioClip bossMusic = null;
        [SerializeField] private AudioClip gameOver = null;
        [SerializeField] private AudioClip win = null;
        [SerializeField] private AudioClip footsteps = null;
        [SerializeField] private AudioClip coin = null;
        [SerializeField] private AudioClip jump = null;
        [SerializeField] private AudioClip fall = null;
        [SerializeField] private AudioClip shoot = null;
        [SerializeField] private AudioClip damage = null;
        [SerializeField] private AudioClip click = null;
        [SerializeField] private AudioSource musicSource = null;
        [SerializeField] private AudioSource effectsSource = null;
        [SerializeField] private AudioSource uiSource = null;
        [SerializeField] private AudioSource footstepsSource = null;
        public float SfxVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public bool Fullscreen { get; private set; }
        private bool gameplayPaused;
        private bool walking;
        private bool initialized;
        private const string SfxKey = "Zhuzbai.SfxVolume";
        private const string MusicKey = "Zhuzbai.MusicVolume";
        private const string FullscreenKey = "Zhuzbai.Fullscreen";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureAudioSources();
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 0.8f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.45f));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            if (!Application.isMobilePlatform) Screen.fullScreen = Fullscreen;
            ApplyVolumes();
            PlaySceneMusic(SceneManager.GetActiveScene());
            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }


        public void ConfigureAudioSources()
        {
            bool hadMusic = musicSource != null;
            musicSource = ResolveChannel(musicSource, "music Channel", true);
            effectsSource = ResolveChannel(effectsSource, "effects Channel", false, musicSource);
            uiSource = ResolveChannel(uiSource, "ui Channel", false, musicSource, effectsSource);
            footstepsSource = ResolveChannel(footstepsSource, "footsteps Channel", true, musicSource, effectsSource, uiSource);
            if (!Application.isPlaying && musicSource.clip != music) musicSource.clip = music;
            if (footstepsSource.clip != footsteps) footstepsSource.clip = footsteps;
            if (Application.isPlaying && Instance == this && initialized)
            {
                SetChannelVolumes();
                if (!hadMusic) PlaySceneMusic(SceneManager.GetActiveScene());
            }
        }

        private AudioSource ResolveChannel(AudioSource current, string channelName, bool loop, params AudioSource[] reserved)
        {
            bool reusable = current != null && current.transform.IsChildOf(transform);
            for (int i = 0; i < reserved.Length; i++) if (current == reserved[i]) reusable = false;
            AudioSource source = reusable ? current : null;
            if (source == null)
            {
                Transform channel = transform.Find(channelName);
                if (channel == null)
                {
                    channel = new GameObject(channelName).transform;
                    channel.SetParent(transform, false);
                }
                if (!channel.TryGetComponent<AudioSource>(out source))
                    source = channel.gameObject.AddComponent<AudioSource>();
                if (source == null) throw new InvalidOperationException("Could not create AudioSource for " + channelName);
                for (int i = 0; i < reserved.Length; i++)
                    if (source == reserved[i]) throw new InvalidOperationException("Audio channels must use distinct AudioSources: " + channelName);
            }
        
            if (source.GetComponents<AudioSource>().Length != 1)
                throw new InvalidOperationException("Expected one AudioSource on " + source.gameObject.name + ". Remove the extra channel component before applying audio setup.");
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = loop;
            source.enabled = true;
            if (!source.gameObject.activeSelf) source.gameObject.SetActive(true);
            return source;
        }

        private bool SourcesReady => musicSource != null && effectsSource != null && uiSource != null && footstepsSource != null &&
            musicSource != effectsSource && musicSource != uiSource && musicSource != footstepsSource &&
            effectsSource != uiSource && effectsSource != footstepsSource && uiSource != footstepsSource;
        private void EnsureSourcesReady()
        {
            if (!SourcesReady) ConfigureAudioSources();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSourcesReady();
            SetFootsteps(false);
            effectsSource.Stop();
            gameplayPaused = false;
            PlaySceneMusic(scene);
        }
        private void PlaySceneMusic(Scene scene)
        {
            AudioClip selected = scene.name switch {
                "MainMenu" => mainMenuMusic,
                "MainLevel" or "Level2" or "Level3" => levelMusic,
                "Level4" => bossMusic, _ => music
            };
            if (musicSource.clip == selected && musicSource.isPlaying) return;
            musicSource.Stop();
            musicSource.clip = selected;
            musicSource.loop = true;
            if (selected != null) musicSource.Play();
            else Debug.LogError("Missing music for " + scene.name + "; run Setup Game Audio.", this);
        }
        public void PlayGameOver()
        {
            EnsureSourcesReady();
            if (gameOver != null) uiSource.PlayOneShot(gameOver, 0.7f / 0.45f);
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
        public void Play(GameSound sound, bool finishDuringPause = false)
        {
            if (sound != GameSound.Click && gameplayPaused) return;
            EnsureSourcesReady();
            AudioClip clip = sound switch {
                GameSound.Coin => coin, GameSound.Jump => jump, GameSound.Fall => fall,
                GameSound.Shoot => shoot, GameSound.Damage => damage, _ => click
            };
            // Fatal fall/damage tails must finish while the outcome screen pauses gameplay SFX.
            bool uninterrupted = finishDuringPause && sound != GameSound.Click;
            if (clip != null) (sound == GameSound.Click || uninterrupted ? uiSource : effectsSource).PlayOneShot(clip, uninterrupted ? 0.7f / 0.45f : 1);
        }
        public void SetFootsteps(bool playing)
        {
            EnsureSourcesReady();
            playing &= !gameplayPaused && footsteps != null;
            if (walking == playing && (!playing || footstepsSource.isPlaying)) return;
            walking = playing;
            if (playing && footsteps != null) footstepsSource.Play();
            else footstepsSource.Stop();
        }
        public void PlayVictory(AudioClip clip)
        {
            EnsureSourcesReady();
            if (win != null) clip = win;
            if (clip != null) uiSource.PlayOneShot(clip, 0.7f / 0.45f);
        }
        public void SetGameplayPaused(bool paused)
        {
            EnsureSourcesReady();
            if (gameplayPaused == paused) return;
            gameplayPaused = paused;
            if (paused) { SetFootsteps(false); effectsSource.Pause(); }
            else effectsSource.UnPause();
        }
        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            ApplyVolumes();
            PlayerPrefs.Save();
        }
        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            ApplyVolumes();
            PlayerPrefs.Save();
        }
        public void SetFullscreen(bool value)
        {
            Fullscreen = value;
            if (!Application.isMobilePlatform) Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        private void ApplyVolumes()
        {
            EnsureSourcesReady();
            SetChannelVolumes();
        }
        private void SetChannelVolumes()
        {
            musicSource.volume = MusicVolume * 0.65f;
            effectsSource.volume = SfxVolume * 0.7f;
            uiSource.volume = SfxVolume * 0.45f;
            footstepsSource.volume = SfxVolume * 0.16f;
        }
        private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
        private void OnApplicationQuit() => PlayerPrefs.Save();
    }
}

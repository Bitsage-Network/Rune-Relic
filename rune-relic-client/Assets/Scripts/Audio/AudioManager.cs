using UnityEngine;
using UnityEngine.Audio;

namespace RuneRelic.Audio
{
    /// <summary>
    /// Manages game audio including music, SFX, and ambient sounds.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixers")]
        [SerializeField] private AudioMixer masterMixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource uiSource;

        [Header("Music Clips")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip gameMusic;
        [SerializeField] private AudioClip victoryMusic;
        [SerializeField] private AudioClip defeatMusic;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip matchFound;
        [SerializeField] private AudioClip countdownTick;
        [SerializeField] private AudioClip countdownGo;
        [SerializeField] private AudioClip runeCollect;
        [SerializeField] private AudioClip evolution;
        [SerializeField] private AudioClip elimination;
        [SerializeField] private AudioClip abilityUse;
        [SerializeField] private AudioClip shrineCapture;

        [Header("Settings")]
        [SerializeField] private float crossfadeDuration = 1f;
        [SerializeField] private int sfxPoolSize = 10;

        // SFX pool
        private AudioSource[] _sfxPool;
        private int _sfxPoolIndex;

        // Volume settings
        private float _masterVolume = 1f;
        private float _musicVolume = 0.7f;
        private float _sfxVolume = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            InitializeSFXPool();
            LoadVolumeSettings();
        }

        private void InitializeAudioSources()
        {
            if (musicSource == null)
            {
                GameObject musicObj = new GameObject("MusicSource");
                musicObj.transform.SetParent(transform);
                musicSource = musicObj.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                if (musicGroup != null)
                    musicSource.outputAudioMixerGroup = musicGroup;
            }

            if (ambientSource == null)
            {
                GameObject ambientObj = new GameObject("AmbientSource");
                ambientObj.transform.SetParent(transform);
                ambientSource = ambientObj.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
                if (musicGroup != null)
                    ambientSource.outputAudioMixerGroup = musicGroup;
            }

            if (uiSource == null)
            {
                GameObject uiObj = new GameObject("UISource");
                uiObj.transform.SetParent(transform);
                uiSource = uiObj.AddComponent<AudioSource>();
                uiSource.playOnAwake = false;
                if (uiGroup != null)
                    uiSource.outputAudioMixerGroup = uiGroup;
            }
        }

        private void InitializeSFXPool()
        {
            _sfxPool = new AudioSource[sfxPoolSize];
            GameObject poolContainer = new GameObject("SFXPool");
            poolContainer.transform.SetParent(transform);

            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject sfxObj = new GameObject($"SFX_{i}");
                sfxObj.transform.SetParent(poolContainer.transform);
                _sfxPool[i] = sfxObj.AddComponent<AudioSource>();
                _sfxPool[i].playOnAwake = false;
                if (sfxGroup != null)
                    _sfxPool[i].outputAudioMixerGroup = sfxGroup;
            }
        }

        // =====================================================================
        // Music
        // =====================================================================

        public void PlayMenuMusic()
        {
            PlayMusic(menuMusic);
        }

        public void PlayGameMusic()
        {
            PlayMusic(gameMusic);
        }

        public void PlayVictoryMusic()
        {
            PlayMusic(victoryMusic, false);
        }

        public void PlayDefeatMusic()
        {
            PlayMusic(defeatMusic, false);
        }

        private void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null) return;

            if (musicSource.isPlaying && musicSource.clip == clip)
                return;

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = _musicVolume;
            musicSource.Play();
        }

        public void StopMusic(bool fade = true)
        {
            if (fade)
            {
                StartCoroutine(FadeOutMusic());
            }
            else
            {
                musicSource.Stop();
            }
        }

        private System.Collections.IEnumerator FadeOutMusic()
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / crossfadeDuration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = _musicVolume;
        }

        // =====================================================================
        // SFX
        // =====================================================================

        public void PlayButtonClick()
        {
            PlaySFX(buttonClick);
        }

        public void PlayMatchFound()
        {
            PlaySFX(matchFound);
        }

        public void PlayCountdownTick()
        {
            PlaySFX(countdownTick);
        }

        public void PlayCountdownGo()
        {
            PlaySFX(countdownGo);
        }

        public void PlayRuneCollect()
        {
            PlaySFX(runeCollect);
        }

        public void PlayEvolution()
        {
            PlaySFX(evolution);
        }

        public void PlayElimination()
        {
            PlaySFX(elimination);
        }

        public void PlayAbilityUse()
        {
            PlaySFX(abilityUse);
        }

        public void PlayShrineCapture()
        {
            PlaySFX(shrineCapture);
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSFXSource();
            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.Play();
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume * volumeScale);
        }

        private AudioSource GetAvailableSFXSource()
        {
            AudioSource source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Length;
            return source;
        }

        // =====================================================================
        // UI Sounds
        // =====================================================================

        public void PlayUISound(AudioClip clip)
        {
            if (clip == null || uiSource == null) return;
            uiSource.PlayOneShot(clip);
        }

        // =====================================================================
        // Volume Control
        // =====================================================================

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            if (masterMixer != null)
            {
                float db = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
                masterMixer.SetFloat("MasterVolume", db);
            }
            SaveVolumeSettings();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = _musicVolume;
            if (ambientSource != null)
                ambientSource.volume = _musicVolume * 0.5f;
            SaveVolumeSettings();
        }

        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            SaveVolumeSettings();
        }

        public float GetMasterVolume() => _masterVolume;
        public float GetMusicVolume() => _musicVolume;
        public float GetSFXVolume() => _sfxVolume;

        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", _masterVolume);
            PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
            PlayerPrefs.SetFloat("SFXVolume", _sfxVolume);
            PlayerPrefs.Save();
        }

        private void LoadVolumeSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            _sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

            SetMasterVolume(_masterVolume);
            SetMusicVolume(_musicVolume);
            SetSFXVolume(_sfxVolume);
        }
    }
}

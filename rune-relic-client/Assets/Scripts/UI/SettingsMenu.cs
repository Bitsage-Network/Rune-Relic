using UnityEngine;
using UnityEngine.UI;
using RuneRelic.Audio;

namespace RuneRelic.UI
{
    /// <summary>
    /// Settings menu for adjusting audio, graphics, and controls.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject graphicsPanel;
        [SerializeField] private GameObject controlsPanel;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text masterVolumeText;
        [SerializeField] private Text musicVolumeText;
        [SerializeField] private Text sfxVolumeText;

        [Header("Graphics Settings")]
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Slider fpsLimitSlider;
        [SerializeField] private Text fpsLimitText;

        [Header("Controls")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityText;
        [SerializeField] private Toggle invertYToggle;

        [Header("Buttons")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button graphicsTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;

        private Resolution[] _resolutions;

        private void Start()
        {
            // Setup tab buttons
            if (audioTabButton != null)
                audioTabButton.onClick.AddListener(() => ShowPanel(audioPanel));
            if (graphicsTabButton != null)
                graphicsTabButton.onClick.AddListener(() => ShowPanel(graphicsPanel));
            if (controlsTabButton != null)
                controlsTabButton.onClick.AddListener(() => ShowPanel(controlsPanel));
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);

            // Setup audio sliders
            SetupAudioSettings();

            // Setup graphics settings
            SetupGraphicsSettings();

            // Setup controls
            SetupControlSettings();

            // Load saved settings
            LoadSettings();

            // Show audio panel by default
            ShowPanel(audioPanel);

            // Hide settings panel initially
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        private void SetupAudioSettings()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
        }

        private void SetupGraphicsSettings()
        {
            // Quality settings
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                qualityDropdown.value = QualitySettings.GetQualityLevel();
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            // Resolution settings
            if (resolutionDropdown != null)
            {
                _resolutions = Screen.resolutions;
                resolutionDropdown.ClearOptions();

                var options = new System.Collections.Generic.List<string>();
                int currentResolutionIndex = 0;

                for (int i = 0; i < _resolutions.Length; i++)
                {
                    string option = $"{_resolutions[i].width} x {_resolutions[i].height}";
                    options.Add(option);

                    if (_resolutions[i].width == Screen.currentResolution.width &&
                        _resolutions[i].height == Screen.currentResolution.height)
                    {
                        currentResolutionIndex = i;
                    }
                }

                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = currentResolutionIndex;
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            // Fullscreen
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            }

            // VSync
            if (vsyncToggle != null)
            {
                vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
                vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            }

            // FPS Limit
            if (fpsLimitSlider != null)
            {
                fpsLimitSlider.minValue = 30;
                fpsLimitSlider.maxValue = 240;
                fpsLimitSlider.value = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
                fpsLimitSlider.onValueChanged.AddListener(OnFPSLimitChanged);
                UpdateFPSLimitText(fpsLimitSlider.value);
            }
        }

        private void SetupControlSettings()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.1f;
                sensitivitySlider.maxValue = 2f;
                sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 1f);
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                UpdateSensitivityText(sensitivitySlider.value);
            }

            if (invertYToggle != null)
            {
                invertYToggle.isOn = PlayerPrefs.GetInt("InvertY", 0) == 1;
                invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            }
        }

        // =====================================================================
        // Panel Control
        // =====================================================================

        public void Open()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        public void Close()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        private void ShowPanel(GameObject panel)
        {
            if (audioPanel != null)
                audioPanel.SetActive(panel == audioPanel);
            if (graphicsPanel != null)
                graphicsPanel.SetActive(panel == graphicsPanel);
            if (controlsPanel != null)
                controlsPanel.SetActive(panel == controlsPanel);
        }

        // =====================================================================
        // Audio Callbacks
        // =====================================================================

        private void OnMasterVolumeChanged(float value)
        {
            var audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                audioManager.SetMasterVolume(value);
            }
            UpdateVolumeText(masterVolumeText, value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            var audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                audioManager.SetMusicVolume(value);
            }
            UpdateVolumeText(musicVolumeText, value);
        }

        private void OnSFXVolumeChanged(float value)
        {
            var audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                audioManager.SetSFXVolume(value);
            }
            UpdateVolumeText(sfxVolumeText, value);
        }

        private void UpdateVolumeText(Text text, float value)
        {
            if (text != null)
            {
                text.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        // =====================================================================
        // Graphics Callbacks
        // =====================================================================

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index);
            PlayerPrefs.SetInt("QualityLevel", index);
        }

        private void OnResolutionChanged(int index)
        {
            if (_resolutions != null && index < _resolutions.Length)
            {
                Resolution res = _resolutions[index];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        }

        private void OnVSyncChanged(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt("VSync", enabled ? 1 : 0);
        }

        private void OnFPSLimitChanged(float value)
        {
            int fps = Mathf.RoundToInt(value);
            Application.targetFrameRate = fps;
            PlayerPrefs.SetInt("FPSLimit", fps);
            UpdateFPSLimitText(value);
        }

        private void UpdateFPSLimitText(float value)
        {
            if (fpsLimitText != null)
            {
                fpsLimitText.text = $"{Mathf.RoundToInt(value)} FPS";
            }
        }

        // =====================================================================
        // Controls Callbacks
        // =====================================================================

        private void OnSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat("Sensitivity", value);
            UpdateSensitivityText(value);
        }

        private void UpdateSensitivityText(float value)
        {
            if (sensitivityText != null)
            {
                sensitivityText.text = $"{value:F1}x";
            }
        }

        private void OnInvertYChanged(bool inverted)
        {
            PlayerPrefs.SetInt("InvertY", inverted ? 1 : 0);
        }

        // =====================================================================
        // Settings Management
        // =====================================================================

        private void LoadSettings()
        {
            // Audio
            var audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                if (masterVolumeSlider != null)
                {
                    masterVolumeSlider.value = audioManager.GetMasterVolume();
                    UpdateVolumeText(masterVolumeText, masterVolumeSlider.value);
                }
                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.value = audioManager.GetMusicVolume();
                    UpdateVolumeText(musicVolumeText, musicVolumeSlider.value);
                }
                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.value = audioManager.GetSFXVolume();
                    UpdateVolumeText(sfxVolumeText, sfxVolumeSlider.value);
                }
            }

            // Graphics
            if (qualityDropdown != null)
            {
                qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            }

            if (vsyncToggle != null)
            {
                vsyncToggle.isOn = PlayerPrefs.GetInt("VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            }

            if (fpsLimitSlider != null)
            {
                fpsLimitSlider.value = PlayerPrefs.GetInt("FPSLimit", 60);
            }
        }

        private void ResetToDefaults()
        {
            // Audio
            if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.7f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;

            // Graphics
            if (qualityDropdown != null) qualityDropdown.value = 2; // Medium
            if (fullscreenToggle != null) fullscreenToggle.isOn = true;
            if (vsyncToggle != null) vsyncToggle.isOn = true;
            if (fpsLimitSlider != null) fpsLimitSlider.value = 60;

            // Controls
            if (sensitivitySlider != null) sensitivitySlider.value = 1f;
            if (invertYToggle != null) invertYToggle.isOn = false;

            PlayerPrefs.Save();
        }
    }
}

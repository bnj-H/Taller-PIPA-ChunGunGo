using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Main";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        InitializeSlider(masterVolumeSlider, AudioManager.MasterVolume, AudioManager.SetMasterVolume);
        InitializeSlider(musicVolumeSlider, AudioManager.MusicVolume, AudioManager.SetMusicVolume);
        InitializeSlider(sfxVolumeSlider, AudioManager.SfxVolume, AudioManager.SetSfxVolume);
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        Debug.Log("ChunGunGo: Quit pressed in the Editor; Application.Quit is ignored there.");
#endif
    }

    private static void InitializeSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> changed)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        slider.onValueChanged.AddListener(changed);
    }
}

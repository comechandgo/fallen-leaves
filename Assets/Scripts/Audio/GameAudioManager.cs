using UnityEngine;

public sealed class GameAudioManager : MonoBehaviour
{
    private const string MasterVolumeKey = "game.audio.master_volume";
    private const string BgmResourcePath = "Audio/BGM";
    private const string ButtonClickResourcePath = "Audio/UIButtonClick";
    private const string LeafRustleResourcePath = "Audio/LeafRustle";

    private static GameAudioManager instance;
    private static float masterVolume = 1f;
    private static bool missingManagerWarningShown;
    private static bool missingBgmWarningShown;
    private static bool missingButtonClickWarningShown;
    private static bool missingLeafRustleWarningShown;

    private AudioSource bgmSource;
    private AudioSource uiSource;
    private AudioSource leafRustleSource;

    private AudioClip buttonClickClip;
    private AudioClip leafRustleClip;

    public static float MasterVolume => masterVolume;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadMasterVolume();
        CreateAudioSources();
        LoadAudioClips();
        StartBackgroundMusic();
    }

    public static void SetMasterVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            value = 1f;
        }

        masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
    }

    public static void PlayButtonClick()
    {
        if (!TryGetInstance(out GameAudioManager manager)) return;
        if (manager.uiSource == null || manager.buttonClickClip == null) return;

        manager.uiSource.PlayOneShot(manager.buttonClickClip);
    }

    public static void PlayLeafRustle()
    {
        if (!TryGetInstance(out GameAudioManager manager)) return;
        if (manager.leafRustleSource == null || manager.leafRustleClip == null) return;
        if (manager.leafRustleSource.isPlaying) return;

        manager.leafRustleSource.Play();
    }

    private static bool TryGetInstance(out GameAudioManager manager)
    {
        manager = instance;
        if (manager != null) return true;

        if (!missingManagerWarningShown)
        {
            Debug.LogWarning("GameAudioManager is unavailable; audio playback was skipped.");
            missingManagerWarningShown = true;
        }

        return false;
    }

    private static AudioSource CreateSource(GameObject owner, bool loop)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 1f;
        return source;
    }

    private void LoadMasterVolume()
    {
        float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        if (float.IsNaN(savedVolume) || float.IsInfinity(savedVolume))
        {
            savedVolume = 1f;
        }

        masterVolume = Mathf.Clamp01(savedVolume);
        AudioListener.volume = masterVolume;
    }

    private void CreateAudioSources()
    {
        bgmSource = CreateSource(gameObject, true);
        uiSource = CreateSource(gameObject, false);
        leafRustleSource = CreateSource(gameObject, false);
    }

    private void LoadAudioClips()
    {
        AudioClip bgmClip = LoadClip(BgmResourcePath, ref missingBgmWarningShown);
        buttonClickClip = LoadClip(ButtonClickResourcePath, ref missingButtonClickWarningShown);
        leafRustleClip = LoadClip(LeafRustleResourcePath, ref missingLeafRustleWarningShown);

        bgmSource.clip = bgmClip;
        leafRustleSource.clip = leafRustleClip;
    }

    private static AudioClip LoadClip(string resourcePath, ref bool warningShown)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null || warningShown) return clip;

        Debug.LogWarning("Audio clip is missing from Resources/" + resourcePath + ".");
        warningShown = true;
        return null;
    }

    private void StartBackgroundMusic()
    {
        if (bgmSource.clip == null || bgmSource.isPlaying) return;
        bgmSource.Play();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

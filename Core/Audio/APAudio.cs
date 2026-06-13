using System;
using System.IO;
using System.Reflection;

using BepInEx;

using UnityEngine;
using UnityEngine.Networking;

using Object = UnityEngine.Object;

namespace NOAutopilot.Core.Audio;

internal sealed class APAudio
{
    private readonly AudioSource _source;

    private AudioClip _clip;

    private bool _wasActive;
    private string _lastModeSignature = "";
    private bool _initialized;

    private float _lastModeCueTime = -999f;

    public APAudio(GameObject host)
    {
        _source = host.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
        _source.ignoreListenerPause = true;
    }

    public void Load()
    {
        string path = FindAudioPath();

        if (string.IsNullOrEmpty(path))
        {
            Plugin.Logger.LogWarning("[APAudio] No AP sound file found.");
            return;
        }

        AudioType type = GetAudioType(path);

        if (type == AudioType.UNKNOWN)
        {
            Plugin.Logger.LogError($"[APAudio] Unsupported audio type: {path}");
            return;
        }

        try
        {
            string uri = new Uri(path).AbsoluteUri;

            using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, type);

            Plugin.Logger.LogInfo($"[APAudio] Loading audio: {path}");

            UnityWebRequestAsyncOperation op = req.SendWebRequest();

            while (!op.isDone)
            {
            }

            if (!string.IsNullOrEmpty(req.error))
            {
                Plugin.Logger.LogError($"[APAudio] Failed to load audio: {req.error}");
                return;
            }

            _clip = DownloadHandlerAudioClip.GetContent(req);

            if (_clip == null || _clip.loadState != AudioDataLoadState.Loaded)
            {
                Plugin.Logger.LogError("[APAudio] Audio load returned null/unloaded clip.");
                return;
            }

            _clip.name = Path.GetFileNameWithoutExtension(path);

            Plugin.Logger.LogInfo($"[APAudio] Loaded '{_clip.name}', length={_clip.length:F2}s.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[APAudio] Error loading audio: {ex}");
        }
    }

    public void Update()
    {
        if (!Plugin.APSoundEnabled.Value || _clip == null)
        {
            RememberCurrentState();
            return;
        }

        bool active = IsAutopilotLikeActive();
        string modeSignature = GetModeSignature(active);

        if (!_initialized)
        {
            _initialized = true;
            _wasActive = active;
            _lastModeSignature = modeSignature;
            return;
        }

        if (_wasActive && !active)
        {
            PlayDisconnect();
        }
        else if (_wasActive && active && modeSignature != _lastModeSignature)
        {
            PlayModeChange();
        }

        _wasActive = active;
        _lastModeSignature = modeSignature;
    }

    public void Destroy()
    {
        if (_source != null)
        {
            Object.Destroy(_source);
        }
    }

    private void RememberCurrentState()
    {
        _wasActive = IsAutopilotLikeActive();
        _lastModeSignature = GetModeSignature(_wasActive);
        _initialized = true;
    }

    private static bool IsAutopilotLikeActive()
    {
        return (APData.Enabled && !APData.GCASActive) || APData.ALSActive;
    }

    private static string GetModeSignature(bool active)
    {
        if (!active)
        {
            return "OFF";
        }

        string vertical =
            APData.GCASActive ? "GCAS" :
            APData.ALSActive ? "ALS" :
            APData.TargetAlt > 0f ? "ALT" :
            "V_NONE";

        string lateral =
            APData.NavEnabled ? "NAV" :
            APData.TargetCourse >= 0f ? "CRS" :
            APData.TargetRoll != -999f ? "ROLL" :
            "L_NONE";

        string throttle =
            APData.TargetSpeed >= 0f
                ? APData.SpeedHoldIsMach ? "MACH" : "SPD"
                : "T_NONE";

        return $"{vertical}|{lateral}|{throttle}";
    }

    private void PlayDisconnect()
    {
        PlayFrom(0f, interrupt: true);
    }

    private void PlayModeChange()
    {
        if (Time.unscaledTime - _lastModeCueTime < Plugin.APModeCueCooldown.Value)
        {
            return;
        }

        _lastModeCueTime = Time.unscaledTime;

        float start = Mathf.Clamp(
            Plugin.APModeCueStartSeconds.Value,
            0f,
            Mathf.Max(0f, _clip.length - 0.01f));

        PlayFrom(start, interrupt: false);
    }

    private void PlayFrom(float seconds, bool interrupt)
    {
        if (_source == null || _clip == null)
        {
            return;
        }

        if (_source.isPlaying && !interrupt)
        {
            return;
        }

        _source.Stop();
        _source.clip = _clip;
        _source.loop = false;
        _source.volume = Mathf.Clamp(Plugin.APSoundVolumePercent.Value / 100f, 0f, 2f);

        _source.timeSamples = Mathf.Clamp(
            Mathf.RoundToInt(seconds * _clip.frequency),
            0,
            Mathf.Max(0, _clip.samples - 1));
        _source.Play();
    }

    private static string FindAudioPath()
    {
        string configured = Plugin.APSoundFile.Value;

        if (!string.IsNullOrWhiteSpace(configured) &&
            Path.IsPathRooted(configured) &&
            File.Exists(configured))
        {
            return configured;
        }

        string fileName = string.IsNullOrWhiteSpace(configured)
            ? "ap_disconnect.ogg"
            : configured;

        foreach (string dir in GetCandidateAudioDirs())
        {
            string path = Path.Combine(dir, fileName);

            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string[] GetCandidateAudioDirs()
    {
        string assemblyDir = "";

        try
        {
            string location = Assembly.GetExecutingAssembly().Location;
            assemblyDir = Path.GetDirectoryName(location);
        }
        catch
        {
        }

        return
        [
            Path.Combine(assemblyDir ?? "", "Audio"),
            Path.Combine(Paths.PluginPath, "no-autopilot-mod", "Audio"),
            Path.Combine(Paths.PluginPath, "no-autopilot-mod", "no-autopilot-mod", "Audio"),
            Path.Combine(Paths.PluginPath, "Audio"),
            Path.Combine(Paths.BepInExRootPath, "scripts", "Audio"),
        ];
    }

    private static AudioType GetAudioType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".wav" => AudioType.WAV,
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            _ => AudioType.UNKNOWN,
        };
    }
}

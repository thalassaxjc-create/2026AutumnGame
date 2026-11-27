using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class LacquerMinigameController
{
    const string MusicFolder = "Audio/Lacquer/";
    static readonly string[] TrackKeys = { "encanto", "shoudie", "orchid" };

    [Serializable]
    sealed class MusicChart
    {
        public float sweepSeconds;
        public MusicCue[] cues;
    }

    [Serializable]
    struct MusicCue
    {
        public float start;
        public float end;
    }

    MusicChart musicChart;
    AudioClip musicClip;
    AudioSource musicSource;
    Text trackLabel;
    Text introInstructions;
    int trackIndex = -1;
    double musicDspStart;
    bool musicClockActive;

    void SelectMusic(int day)
    {
        int index = Mathf.Clamp(day, 1, TrackKeys.Length) - 1;
        if (index != trackIndex)
        {
            StopMusic();
            trackIndex = index;
            string path = MusicFolder + TrackKeys[index];
            musicClip = Resources.Load<AudioClip>(path);
            TextAsset chartAsset = Resources.Load<TextAsset>(path + "_Chart");
            try
            {
                musicChart = chartAsset != null ? JsonUtility.FromJson<MusicChart>(chartAsset.text) : null;
            }
            catch (ArgumentException)
            {
                musicChart = null;
            }
            if (!CuesFitAudio())
            {
                musicClip = null;
                musicChart = null;
                Debug.LogWarning("刷大漆音乐或谱面无效 " + path, this);
            }
            musicSource.clip = musicClip;
            if (musicClip != null && musicClip.loadState == AudioDataLoadState.Unloaded)
                musicClip.LoadAudioData();
        }

        trackLabel.text = musicChart != null ? $"第 {index + 1} 天  ·  {TrackKeys[index]}" : "";
        introInstructions.text = musicChart != null
            ? "游标随音乐扫过漆点时点按\n朱漆用左键，青漆用右键\n\n漆条从亮圈按住，跟着亮点拖\n早到终点时，按住等尾拍"
            : "游标扫过漆点时点按\n朱漆用左键，青漆用右键\n\n漆条从亮圈按住，跟着亮点拖";
    }

    bool CuesFitAudio()
    {
        if (musicClip == null || musicChart?.cues == null || musicChart.cues.Length == 0 ||
            !(musicChart.sweepSeconds >= 2f && musicChart.sweepSeconds <= musicClip.length))
            return false;
        float previousEnd = 0f;
        foreach (MusicCue cue in musicChart.cues)
        {
            if (!(cue.start > previousEnd) || (cue.end != 0f && !(cue.end > cue.start)))
                return false;
            previousEnd = Mathf.Max(cue.start, cue.end);
        }
        return previousEnd + goodWindow < musicClip.length;
    }

    void PrepareMusic()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.58f;
        musicSource.priority = 32;
    }

    void StartMusic()
    {
        if (musicChart == null)
            return;

        float leadIn = Mathf.Max(0.15f, preShowTime - musicChart.cues[0].start);
        musicDspStart = AudioSettings.dspTime + leadIn;
        songTime = -leadIn;
        musicSource.timeSamples = 0;
        musicClockActive = true;
        musicSource.PlayScheduled(musicDspStart);
    }

    void StopMusic()
    {
        musicClockActive = false;
        if (musicSource != null)
            musicSource.Stop();
    }
}

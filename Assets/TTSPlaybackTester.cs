using UnityEngine;
using UnityEngine.UI;

public class TTSPlaybackTester : MonoBehaviour
{
    public AudioSource audioSource;
    public OVRLipSyncContext lipSyncContext;

    void Start()
    {
        AudioClip clip = Resources.Load<AudioClip>("test_tts"); // no ".wav" in path
        if (clip == null)
        {
            Debug.LogError("Failed to load test_tts.wav from Resources!");
            return;
        }

        audioSource.Stop();
        audioSource.clip = Resources.Load<AudioClip>("test_tts");

        if (audioSource.clip == null)
        {
            Debug.LogError("❌ test_tts.wav not loaded.");
            return;
        }

        Debug.Log($"Loaded clip: {audioSource.clip.name}");
        Debug.Log($"Channels: {audioSource.clip.channels}, Frequency: {audioSource.clip.frequency}");

        audioSource.volume = 1f;
        audioSource.mute = false;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.loop = false;
        audioSource.Play();

        Debug.Log($"IsPlaying: {audioSource.isPlaying}");

    }
}

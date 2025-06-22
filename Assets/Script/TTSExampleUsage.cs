using UnityEngine;
using TMPro;

public class TTSExampleUsage : MonoBehaviour
{
    public KokoroTextToSpeech kokoroTTS;
    public AudioSource audioSource;
    public OVRLipSyncContext lipSyncContext;
    public TextMeshProUGUI inputField; // 👈 Assign this in the Inspector

    // Called by button
    public void OnClickSpeakFromInput()
    {
        string text = inputField.text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("TTS input is empty.");
            return;
        }

        kokoroTTS.RequestSpeech(text, clip =>
        {
            audioSource.clip = clip;
            lipSyncContext.audioSource = audioSource;
            lipSyncContext.audioLoopback = true;
            audioSource.Play();
        },
        error =>
        {
            Debug.LogError("TTS Error: " + error);
        });
    }
}

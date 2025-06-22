using System;
using TMPro;
using UnityEngine;

public class TTSExample : MonoBehaviour
{
    [SerializeField] private TTS textToSpeech;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TextMeshProUGUI inputField;

    public void PressBtn()
    {
        string text = inputField.text;

        if (!string.IsNullOrWhiteSpace(text))
        {
            textToSpeech.GetSpeechAudio(text, OnAudioClipReceived, OnTtsError);
        }
        else
        {
            Debug.LogWarning("Input field is empty.");
        }
    }

    private void OnTtsError(string error)
    {
        Debug.LogError("TTS Error: " + error);
    }

    private void OnAudioClipReceived(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.volume = 1.0f;
            audioSource.mute = false;
            audioSource.bypassEffects = true;
            audioSource.time = 0f;
            audioSource.Play();
        }
        else
        {
            Debug.LogError("Audio clip was null.");
        }
    }
}

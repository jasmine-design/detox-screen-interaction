using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using static OVRLipSync;
using System;
using Unity.VisualScripting;

public class TTSClient : MonoBehaviour
{
    public TextMeshProUGUI inputField;
    public AudioSource audioSource;
    public OVRLipSyncContext lipSyncContext;

    private string apiURL = "http://localhost:5002/tts?text=";

    public void OnClickSend()
    {
        string text = inputField.text;
        if (!string.IsNullOrEmpty(text))
        {
            StartCoroutine(GetTTS(text));
        }
    }

    IEnumerator GetTTS(string text)
    {
        string url = apiURL + UnityWebRequest.EscapeURL(text);
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS request failed: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                Debug.LogError("Downloaded audio clip is null");
                yield break;
            }

            Debug.Log($"✅ Clip loaded: {clip.name}, channels: {clip.channels}, freq: {clip.frequency}");

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = 1f;
            audioSource.mute = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = false;
            audioSource.bypassEffects = true;

            // Make sure lip sync knows about this source
            if (lipSyncContext != null)
            {
                lipSyncContext.audioSource = audioSource;
                lipSyncContext.audioLoopback = true;
            }

            // Let Unity fully prepare the audio
            yield return new WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded);
            yield return new WaitForSeconds(0.1f);

            audioSource.Play();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class TTS : MonoBehaviour
{
    [SerializeField] private string localTtsUrl = "http://localhost:5002/tts?text=";

    public void GetSpeechAudio(string textToConvert, Action<AudioClip> onClipReceived, Action<string> onError = null)
    {
        StartCoroutine(RequestTTS(textToConvert, onClipReceived, onError));
    }

    private IEnumerator RequestTTS(string text, Action<AudioClip> onClipReceived, Action<string> onError)
    {
        string url = localTtsUrl + UnityWebRequest.EscapeURL(text);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS request failed: " + www.error);
                onError?.Invoke(www.error);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    onClipReceived?.Invoke(clip);
                }
                else
                {
                    Debug.LogError("TTS returned null audio clip.");
                    onError?.Invoke("TTS returned null audio clip.");
                }
            }
        }
    }
}

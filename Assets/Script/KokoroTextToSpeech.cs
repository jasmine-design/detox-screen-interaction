
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;

public class KokoroTextToSpeech : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://localhost:5002/tts";

    private Action<AudioClip> _onClipReady;
    private Action<string> _onError;

    public void RequestSpeech(string text, Action<AudioClip> onClipReady, Action<string> onError)
    {
        _onClipReady = onClipReady;
        _onError = onError;
        StartCoroutine(DownloadAndConvert(text));
    }

    private IEnumerator DownloadAndConvert(string text)
    {
        string url = serverUrl;  // Keep it: "http://localhost:5002/tts"

        // Build JSON body:
        string jsonPayload = JsonUtility.ToJson(new TextPayload { text = text });

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(jsonBytes);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                _onError?.Invoke(www.error);
                yield break;
            }

            byte[] wavData = www.downloadHandler.data;
            string tempPath = Path.Combine(Application.persistentDataPath, "kokoro.wav");
            File.WriteAllBytes(tempPath, wavData);

            yield return StartCoroutine(AudioConverter.LoadClipFromWav(tempPath, clip =>
            {
                _onClipReady?.Invoke(clip);
            }));
        }
    }

    [System.Serializable]
    private class TextPayload
    {
        public string text;
    }



}

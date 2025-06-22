
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class AudioConverter : MonoBehaviour
{
    public static IEnumerator LoadClipFromWav(string path, Action<AudioClip> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("AudioConverter failed to load WAV: " + www.error);
                onComplete(null);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
            onComplete(clip);
        }
    }
}

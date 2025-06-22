using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class LLMClient : MonoBehaviour
{
    public TextMeshProUGUI outputText; // Link this to your Text UI
    public KokoroTextToSpeech kokoroTTS;
    public AudioSource audioSource;
    public OVRLipSyncContext lipSyncContext;

    [System.Serializable]
    public class LLMRequest
    {
        public string text;
    }

    [System.Serializable]
    public class LLMResponse
    {
        public string text;
    }

    public void SendTextToLLM(string userText)
    {
        StartCoroutine(PostToLLM(userText));
    }

    IEnumerator PostToLLM(string text)
    {
        LLMRequest payload = new LLMRequest { text = text };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest www = new UnityWebRequest("http://localhost:5002/llm", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = www.downloadHandler.text;
                LLMResponse result = JsonUtility.FromJson<LLMResponse>(jsonResponse);

                Debug.Log("LLM says: " + result.text);
                outputText.text = result.text;

                kokoroTTS.RequestSpeech(result.text, clip =>
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
            else
            {
                Debug.LogError("LLM request failed: " + www.error);
            }
        }
    }
}

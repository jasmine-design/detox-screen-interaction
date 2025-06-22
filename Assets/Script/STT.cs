using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.IO;

public class STT : MonoBehaviour
{
    public TMP_Text outputText;       // Drag your TextMeshProUGUI here
    public AudioSource audioSource;   // Optional, only if you want playback
    public bool useCiwa = true;
    public CiwaManager ciwaManager;
    public LLMClient llmClient;
    private string micDevice;
    private AudioClip recordedClip;
    private string sttUrl = "http://localhost:5002/stt";

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected.");
            return;
        }

        micDevice = Microphone.devices[0];  // Use the first available mic
        Debug.Log($"Using mic: {micDevice}");

        recordedClip = Microphone.Start(micDevice, false, 5, 16000);
    }

    public void StopAndSend()
    {
        Microphone.End(micDevice);
        Debug.Log("Recording stopped");

        // Playback the recording to verify it worked
        //audioSource.clip = recordedClip;
        //audioSource.Play();

        // Save the clip to WAV file
        string path = Application.persistentDataPath + "/stt_record.wav";
        byte[] wavData = WavUtility.FromAudioClip(recordedClip, out _, true);
        File.WriteAllBytes(path, wavData);

        StartCoroutine(SendWavToSTT(path));
    }

    void OnSTTResult(string transcript)
    {
        if (useCiwa && ciwaManager != null)
            ciwaManager.HandleTranscript(transcript);
        else if (llmClient != null)
            llmClient.SendTextToLLM(transcript);

    }


    IEnumerator SendWavToSTT(string filepath)
    {
        byte[] data = File.ReadAllBytes(filepath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", data, "audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(sttUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string response = www.downloadHandler.text;
                Debug.Log("STT Result: " + response);

                string recognizedText = ExtractTextFromJson(response);
                outputText.text = recognizedText;
                OnSTTResult(recognizedText);
            }
            else
            {
                Debug.LogError("STT failed: " + www.error);
                outputText.text = "STT Error: " + www.error;
            }
        }
    }

    public void StopAndSendWithCallback(System.Action<string> callback)
    {
        Microphone.End(micDevice);
        Debug.Log("Recording stopped (callback)");

        string path = Application.persistentDataPath + "/stt_record.wav";
        byte[] wavData = WavUtility.FromAudioClip(recordedClip, out _, true);
        File.WriteAllBytes(path, wavData);

        StartCoroutine(SendWavToSTT(path, callback));
    }

    private IEnumerator SendWavToSTT(string filepath, System.Action<string> callback)
    {
        byte[] data = File.ReadAllBytes(filepath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", data, "audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(sttUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string response = www.downloadHandler.text;
                Debug.Log("STT Result: " + response);

                string recognizedText = ExtractTextFromJson(response);
                outputText.text = recognizedText;

                // 🔁 Call the passed callback with transcript
                callback?.Invoke(recognizedText);
            }
            else
            {
                Debug.LogError("STT failed: " + www.error);
                outputText.text = "STT Error: " + www.error;
                callback?.Invoke(""); // Pass empty string or handle failure
            }
        }
    }


    string ExtractTextFromJson(string json)
    {
        int index = json.IndexOf("\"text\":");
        if (index >= 0)
        {
            int start = json.IndexOf("\"", index + 7) + 1;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }
        return "(no text found)";
    }

}

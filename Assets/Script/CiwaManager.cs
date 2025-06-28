using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine.UI;
using System.Linq;

public enum CiwaStage
{
    Briefing1,
    Briefing2,
    Assessment,
    Feedback
}

public class CiwaManager : MonoBehaviour
{
    public KokoroTextToSpeech kokoroTTS;
    public AudioSource audioSource;
    public STT stt;

    public TextMeshProUGUI statusText;
    public TextMeshProUGUI questionProgressNum;
    public TextMeshProUGUI questionProgressText;

    public TMP_InputField manualInputField;
    public ToggleGroup scoreToggleGroup;
    public List<Toggle> scoreToggles;

    public Transform agentTransform;
    public Animator agentAnimator;

    public GameObject Briefing1Canvas;
    public GameObject Briefing2Canvas;
    public GameObject StartBtn;
    public GameObject AssessmentCanvas;
    public GameObject FeedbackCanvas;
    public GameObject ListeningCanvas;

    private string sessionId = "";
    private string currentQuestion = "";
    private bool assessmentComplete = false;
    private bool answerSubmitted = false;
    private bool waitingForNextQuestion = false;
    private const int ciwaQuestionsCount = 10;
    private bool isTyping = false;
    private bool isListening = false;
    private CiwaStage currentStage = CiwaStage.Briefing1;
    private readonly string[] ciwaQuestions = new string[]
    {
        "Do you feel nauseated? Have you vomited?",
        "Do you notice any shaking in your hands?",
        "Are you sweating more than usual even when resting?",
        "Do you feel anxious or nervous right now?",
        "Do you feel restless or unable to sit still?",
        "Are you experiencing any unusual skin sensations, like itching or crawling?",
        "Have you heard things that others cannot hear?",
        "Have you seen anything unusual or that may not be there?",
        "Do you have a headache or feel pressure in your head?",
        "Can you tell me what day it is and where you are right now?"
    };

    [Header("Settings")]
    public bool enableVoiceInput = true;

    void Start()
    {
        // Prevent null reference errors
        if (manualInputField != null)
        {
            manualInputField.onSelect.AddListener(delegate { isTyping = true; });
            manualInputField.onDeselect.AddListener(delegate { isTyping = false; });
        }

        SetStageUI(currentStage);

        foreach (Toggle toggle in scoreToggles)
        {
            toggle.onValueChanged.AddListener((isOn) => UpdateSubmitButtonState());
        }

        // Start briefing flow
        StartCoroutine(RequestBriefing());
    }

    void SetStageUI(CiwaStage stage)
    {
        // Basic setup
        Briefing1Canvas.SetActive(stage == CiwaStage.Briefing1);
        Briefing2Canvas.SetActive(stage == CiwaStage.Briefing2); // Start as false in coroutine
        AssessmentCanvas.SetActive(stage == CiwaStage.Assessment);
        FeedbackCanvas.SetActive(stage == CiwaStage.Feedback);

        UpdateAgentAppearance(stage);

        // Special handling for Briefing3
        if (stage == CiwaStage.Briefing2)
        {
            StartCoroutine(ShowBriefing2());
        }
    }

    private void UpdateAgentAppearance(CiwaStage stage)
    {
        switch (stage)
        {
            case CiwaStage.Briefing1:
                //Vector3 pos = agentTransform.localPosition;
                //pos.y = 1;
               // agentTransform.localPosition = pos;
                agentAnimator.Play("Idle");
                StartCoroutine(TriggerGreetingAfterDelay());
                print("Briefing Stage");
                break;

            case CiwaStage.Assessment:
                
                //agentTransform.localPosition = new Vector3(0, 1, 0);
                agentAnimator.Play("Idle");
                print("Assessment Stage");
                break;

            case CiwaStage.Feedback:
               // agentTransform.localPosition = new Vector3(0, 1, 0);
                agentAnimator.Play("Idle");
                print("Feedback Stage");
                break;
        }
    }

    [System.Serializable]
    private class CiwaResponse
    {
        public bool completed;
        public string stage;
        public string question;
        public int question_number;
        public string session_id;
        public string message;
    }

    [System.Serializable]
    private class ChatPayload
    {
        public string user_input = "";   // Force default to empty string
    }

    [System.Serializable]
    private class ScorePayload
    {
        public int score;
    }

    [System.Serializable]
    private class EmpathyResponse
    {
        public string empathy_response;
    }

    [System.Serializable]
    private class ExplanationPayload
    {
        public string patient_reply;
    }



    [System.Serializable]
    private class FinalResponse
    {
        public string final_response;
    }

    private IEnumerator RequestBriefing()
    {
        string url = "http://localhost:5002/ciwa_next";   // <-- remove '?briefing=true'

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("CIWA Briefing error: " + www.error);
            yield break;
        }

        CiwaResponse res = JsonUtility.FromJson<CiwaResponse>(www.downloadHandler.text);
        statusText.text = res.question;

        if (string.IsNullOrWhiteSpace(res.question))
        {
            Debug.LogWarning("CIWA Briefing returned empty question — skipping TTS.");
            yield break;
        }

        kokoroTTS.RequestSpeech(res.question, clip =>
        {
            audioSource.clip = clip;
            audioSource.Play();
            StartCoroutine(WaitThenListenBriefing());
        },
        error =>
        {
            Debug.LogError("TTS Error (briefing): " + error);
        });
    }
    private IEnumerator ShowBriefing2()
    {
        // Step 1: Show Briefing2Canvas and disable the button
        Briefing2Canvas.SetActive(true);

        if (StartBtn != null)
        {
            Button btn = StartBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = false; // Disable interaction
            }
            StartBtn.SetActive(true); // Show it, even if not interactable
        }

        // Step 2: Wait for agent to finish speaking
        yield return new WaitWhile(() => audioSource.isPlaying);

        // Step 3: Enable the button
        if (StartBtn != null)
        {
            Button btn = StartBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
            }
        }

        // 🔊 Start listening to user after the agent finishes speaking
        //if (enableVoiceInput && !isTyping)
        //{
          //  StartCoroutine(WaitThenListenBriefing());
        //}
        
    }



    private IEnumerator RequestNextQuestion()
    {
        string url = "http://localhost:5002/ciwa_next";

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("CIWA error: " + www.error);
            yield break;
        }

        CiwaResponse res = JsonUtility.FromJson<CiwaResponse>(www.downloadHandler.text);

        if (res.completed)
        {
            assessmentComplete = true;
            currentStage = CiwaStage.Feedback;
            SetStageUI(currentStage);
            statusText.text = res.message;

            kokoroTTS.RequestSpeech(res.message, clip =>
            {
                audioSource.clip = clip;
                audioSource.Play();
            },
            error =>
            {
                Debug.LogError("TTS Error (final comment): " + error);
            });

            yield break;
        }

        if (res.stage == "briefing")
        {
            currentStage = CiwaStage.Briefing1;
        }
        else if (res.stage == "assessment")
        {
            currentStage = CiwaStage.Assessment;
        }

        SetStageUI(currentStage);

        currentQuestion = res.question;
        int questionNumber = res.question_number;

        answerSubmitted = false;
        UpdateSubmitButtonState();

        foreach (Toggle toggle in scoreToggles)
        {
            toggle.interactable = true;
        }

        if (currentStage == CiwaStage.Assessment)
        {
            questionProgressNum.text = $"Question {questionNumber} of {ciwaQuestionsCount}";
        }
        else
        {
            questionProgressNum.text = "";
        }

        questionProgressText.text = currentQuestion;
        answerSubmitted = false;
        UpdateSubmitButtonState();

        if (currentStage == CiwaStage.Assessment)
        {
            questionProgressNum.text = $"Question {questionNumber} of {ciwaQuestionsCount}";

            // Show the ORIGINAL CIWA question, not the agent text
            string originalQuestion = ciwaQuestions[questionNumber - 1];   // zero-based index
            questionProgressText.text = $"{questionNumber}. {originalQuestion}";
        }
        else
        {
            questionProgressNum.text = "";
            questionProgressText.text = "";
        }

        kokoroTTS.RequestSpeech(currentQuestion, clip =>
        {
            audioSource.clip = clip;
            audioSource.Play();

            // 🔒 Immediately disable submit while TTS plays
            Button submitBtn = AssessmentCanvas.GetComponentInChildren<Button>(true);
            if (submitBtn != null)
            {
                submitBtn.interactable = false;
            }

            // 🔓 Re-check after TTS finishes
            StartCoroutine(EnableSubmitWhenAudioDone());

            if (currentStage == CiwaStage.Assessment)
                StartCoroutine(WaitThenListen());
        },
        error =>
        {
            Debug.LogError("TTS Error: " + error);
        });
    }

    private void UpdateSubmitButtonState()
    {
        if (currentStage != CiwaStage.Assessment) return;

        bool hasSelection = scoreToggleGroup.ActiveToggles().Any();
        bool audioDone = !audioSource.isPlaying;
        bool canSubmit = hasSelection && audioDone && !answerSubmitted && !isListening;

        Button submitBtn = AssessmentCanvas.GetComponentInChildren<Button>(true);
        if (submitBtn != null)
        {
            submitBtn.interactable = canSubmit;
        }
    }

    private IEnumerator EnableSubmitWhenAudioDone()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        UpdateSubmitButtonState();
    }

    private IEnumerator WaitThenListenBriefing()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);

        if (!enableVoiceInput)
        {
            statusText.text = "Voice input disabled.";
            yield break;
        }

        if (isTyping)
        {
            statusText.text = "Voice input paused while typing...";
            yield break;
        }

        agentAnimator.SetTrigger("StartListening");

        stt.StartRecording();
        ListeningCanvas.SetActive(true);

        yield return new WaitForSeconds(5);  // adjust as needed
        stt.StopAndSendWithCallback(OnBriefingSTTComplete);

        ListeningCanvas.SetActive(false);

        agentAnimator.SetTrigger("BackToIdle");
    }

    private void OnBriefingSTTComplete(string transcript)
    {
        statusText.text = "Patient said: " + transcript;
        StartCoroutine(SendBriefChat(transcript));
    }

    private IEnumerator SendBriefChat(string userInput)
    {
        string safeUserInput = string.IsNullOrEmpty(userInput) ? " " : userInput;   // Avoid completely empty string

        ChatPayload payload = new ChatPayload { user_input = safeUserInput };

        string json = JsonUtility.ToJson(payload);

        string chatUrl = "http://localhost:5002/ciwa_brief_chat";

        UnityWebRequest www = new UnityWebRequest(chatUrl, "POST");
        www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            var chatResponse = JsonUtility.FromJson<ChatResponse>(jsonResponse);

            statusText.text = chatResponse.response;

            kokoroTTS.RequestSpeech(chatResponse.response, clip =>
            {
                audioSource.clip = clip;
                audioSource.Play();

                // Always move on to Briefing2 after the response
                currentStage = CiwaStage.Briefing2;
                SetStageUI(currentStage);

                // Optionally: you could log or mark whether a name was detected
                bool nameGiven = DetectNameInTranscript(userInput);
                Debug.Log("Name detected: " + nameGiven);

            }, error =>
            {
                Debug.LogError("TTS Error (brief chat): " + error);
            });
        }
        else
        {
            Debug.LogError("Brief chat failed: " + www.error + " → Response: " + www.downloadHandler.text);
        }
    }

    private IEnumerator WaitThenListen()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);

        if (!enableVoiceInput)
        {
            statusText.text = "Voice input disabled.";
            yield break;
        }

        if (isTyping)
        {
            statusText.text = "Voice input paused while typing...";
            yield break;
        }

        agentAnimator.SetTrigger("StartListening");
        isListening = true;
        UpdateSubmitButtonState();

        stt.StartRecording();
        ListeningCanvas.SetActive(true);

        yield return new WaitForSeconds(10);
        stt.StopAndSendWithCallback(OnSTTComplete);
        
        ListeningCanvas.SetActive(false);

        agentAnimator.SetTrigger("BackToIdle");
        isListening = false;
        UpdateSubmitButtonState();
    }

    private IEnumerator TriggerGreetingAfterDelay()
    {
        yield return new WaitForSeconds(8f);

        agentAnimator.SetTrigger("StartGreeting");

        yield return new WaitForSeconds(2.5f);

        agentAnimator.SetTrigger("BackToIdle");
    }

    private void OnSTTComplete(string transcript)
    {
        statusText.text = "Patient said: " + transcript;

        string cleaned = transcript.Trim().ToLower();

        if (currentStage == CiwaStage.Assessment)
        {
            int? normalizedScore = NormalizeScore(cleaned);

            if (normalizedScore.HasValue && normalizedScore.Value >= 0 && normalizedScore.Value <= 7)
            {
                StartCoroutine(SendScore(normalizedScore.Value));
                return;
            }
        }

        // Normal path for sending input
        if (currentStage == CiwaStage.Briefing1 || currentStage == CiwaStage.Briefing2)
        {
            StartCoroutine(SendBriefChat(transcript));
        }
        else if (currentStage == CiwaStage.Assessment)
        {
            StartCoroutine(SendChat(transcript));
        }
    }


    private int? NormalizeScore(string input)
    {
        var wordToNumber = new Dictionary<string, int>
    {
        {"zero", 0}, {"one", 1}, {"two", 2}, {"three", 3},
        {"four", 4}, {"five", 5}, {"six", 6}, {"seven", 7},
        {"0", 0}, {"1", 1}, {"2", 2}, {"3", 3},
        {"4", 4}, {"5", 5}, {"6", 6}, {"7", 7}
    };

        if (wordToNumber.TryGetValue(input, out int value))
            return value;

        return null;
    }

    public void ContinueToAssessment()
    {
        Debug.Log("ContinueToAssessment called"); 
        StartCoroutine(SendContinue());
    }

    private IEnumerator SendContinue()
    {
        UnityWebRequest www = UnityWebRequest.PostWwwForm("http://localhost:5002/ciwa_continue", "");
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Moved to Assessment phase.");
            currentStage = CiwaStage.Assessment;
            SetStageUI(currentStage);
            StartCoroutine(WaitForNextQuestion());
        }
        else
        {
            Debug.LogError("Failed to continue to assessment: " + www.error);
        }
    }

    public void SubmitManualScore()
    {
        Toggle selectedToggle = scoreToggleGroup.ActiveToggles().FirstOrDefault();

        if (selectedToggle == null)
        {
            statusText.text = "Please select a score (0–7) before submitting.";
            return;
        }

        // Find index of selected toggle → that is the score
        int selectedIndex = scoreToggles.IndexOf(selectedToggle);

        if (selectedIndex >= 0 && selectedIndex <= 7)
        {
            statusText.text = "Manual score submitted: " + selectedIndex;

            // Optional: Clear selection after submit
            scoreToggleGroup.SetAllTogglesOff();

            StartCoroutine(SendScore(selectedIndex));
        }
        else
        {
            statusText.text = "Invalid selection. Please select 0–7.";
        }
    }

    public void SubmitScoreFromToggle()
    {
        // Get first active Toggle
        Toggle selectedToggle = scoreToggleGroup.ActiveToggles().FirstOrDefault();

        if (selectedToggle == null)
        {
            statusText.text = "Please select a score (0–7) before submitting.";
            return;
        }

        // Use GameObject name as label (since your Toggles are named "0", "1", ..., "7")
        string label = selectedToggle.gameObject.name.Trim();
        print(label);

        int parsedScore = -1;

        if (int.TryParse(label, out parsedScore))
        {
            if (parsedScore >= 0 && parsedScore <= 7)
            {
                statusText.text = "Score submitted: " + parsedScore;

                // Optional: reset toggles after submit
                scoreToggleGroup.SetAllTogglesOff();

                StartCoroutine(SendScore(parsedScore));
                answerSubmitted = true;

                // 🔒 Lock toggles after submitting
                foreach (Toggle toggle in scoreToggles)
                {
                    toggle.interactable = false;
                }

                UpdateSubmitButtonState();
            }
            else
            {
                statusText.text = "Score out of valid range (0–7).";
            }
        }
        else
        {
            statusText.text = "Could not parse score from selected Toggle.";
        }
    }





    private IEnumerator SendScore(int score)
    {
        ScorePayload payload = new ScorePayload { score = score };
        string json = JsonUtility.ToJson(payload);

        UnityWebRequest www = new UnityWebRequest("http://localhost:5002/ciwa_score", "POST");
        www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            var empathyData = JsonUtility.FromJson<EmpathyResponse>(jsonResponse);

            statusText.text = empathyData.empathy_response;

            kokoroTTS.RequestSpeech(empathyData.empathy_response, clip =>
            {
                audioSource.clip = clip;
                audioSource.Play();
                StartCoroutine(WaitForExplanation());
            },
            error =>
            {
                Debug.LogError("TTS Error (empathy): " + error);
            });
        }
        else
        {
            Debug.LogError("Score submission failed: " + www.error);
        }
    }

    private IEnumerator WaitForExplanation()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);

        if (ListeningCanvas != null)
        {
            ListeningCanvas.SetActive(true);  // ✅ Show Listening Canvas
            Debug.Log("🎧 ListeningCanvas shown during explanation");
        }

        statusText.text = "Listening for your explanation...";
        agentAnimator.SetTrigger("StartListening");

        stt.StartRecording();
        yield return new WaitForSeconds(10);
        stt.StopAndSendWithCallback(OnExplanationSTTComplete);

        agentAnimator.SetTrigger("BackToIdle");

        if (ListeningCanvas != null)
        {
            ListeningCanvas.SetActive(false);  // ✅ Hide again
            Debug.Log("🛑 ListeningCanvas hidden after explanation");
        }
    }

    private void OnExplanationSTTComplete(string transcript)
    {
        Debug.Log("Explanation received: " + transcript);
        StartCoroutine(SendExplanation(transcript));
    }


    private IEnumerator SendExplanation(string explanation)
    {
        // Use fallback text if explanation is empty or whitespace
        if (string.IsNullOrWhiteSpace(explanation) || explanation.Length < 3)
        {
            explanation = "No answer provided.";
        }

        ExplanationPayload payload = new ExplanationPayload { patient_reply = explanation };
        string json = JsonUtility.ToJson(payload);
        

        UnityWebRequest www = new UnityWebRequest("http://localhost:5002/ciwa_explanation", "POST");
        www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();



        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            if (jsonResponse.Contains("final_response"))
            {
                var finalData = JsonUtility.FromJson<FinalResponse>(jsonResponse);

                statusText.text = finalData.final_response;

                kokoroTTS.RequestSpeech(finalData.final_response, clip =>
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    StartCoroutine(WaitForNextQuestion());
                },
                error =>
                {
                    Debug.LogError("TTS Error (final response): " + error);
                });
            }

            else
            {
                Debug.LogWarning("final_response not found in explanation result.");
                statusText.text = "Sorry, I couldn't understand your explanation. Let's continue.";
                StartCoroutine(RequestNextQuestion());
                yield break;
            }
        }
        else
        {
            Debug.LogError("Explanation submission failed: " + www.error);
        }
    }


    private IEnumerator WaitForNextQuestion()
    {
        if (waitingForNextQuestion)
        {
            Debug.LogWarning("Already waiting for next question. Skipping duplicate call.");
            yield break;
        }

        waitingForNextQuestion = true;

        Debug.Log("Waiting for audio to finish before next question...");
        yield return new WaitWhile(() => audioSource.isPlaying);
        Debug.Log("Audio finished, requesting next question.");

        yield return RequestNextQuestion();

        waitingForNextQuestion = false;
    }

    private IEnumerator SendChat(string userInput)
    {
        string safeUserInput = string.IsNullOrEmpty(userInput) ? " " : userInput;   // Avoid completely empty string

        ChatPayload payload = new ChatPayload { user_input = userInput };
        string json = JsonUtility.ToJson(payload);

        string chatUrl = "";

        if (currentStage == CiwaStage.Briefing1)
        {
            chatUrl = "http://localhost:5002/ciwa_brief_chat";
        }
        else if (currentStage == CiwaStage.Assessment)
        {
            chatUrl = "http://localhost:5002/ciwa_chat";
        }
        else
        {
            Debug.LogWarning("Chat is not allowed in current stage: " + currentStage);
            yield break;
        }

        UnityWebRequest www = new UnityWebRequest(chatUrl, "POST");
        www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            var chatResponse = JsonUtility.FromJson<ChatResponse>(jsonResponse);

            statusText.text = chatResponse.response;

            kokoroTTS.RequestSpeech(chatResponse.response, clip =>
            {
                audioSource.clip = clip;
                audioSource.Play();
            },
            error =>
            {
                Debug.LogError("TTS Error (chat): " + error);
            });
        }
        else
        {
            Debug.LogError("Chat failed: " + www.error + " → Response: " + www.downloadHandler.text);
        }
    }


    [System.Serializable]
    private class ChatResponse
    {
        public string response;
    }

    public void HandleTranscript(string transcript)
    {
        Debug.Log("CIWA Manager received: " + transcript);
        OnSTTComplete(transcript);
    }

    private bool DetectNameInTranscript(string transcript)
    {
        // Guard – empty or whitespace never counts
        if (string.IsNullOrWhiteSpace(transcript)) return false;

        transcript = transcript.ToLowerInvariant();

        // Very simple keyword heuristic
        return transcript.Contains("my name is") ||
               transcript.Contains("i am ") ||
               transcript.Contains("i'm ");
    }
}

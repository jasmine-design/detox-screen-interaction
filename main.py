from flask import Flask, request, send_file, jsonify
import tempfile
import soundfile as sf
from kokoro_onnx import Kokoro
from kokoro_onnx.config import SAMPLE_RATE
from vosk import Model, KaldiRecognizer
from scipy.signal import resample
import json
import requests
import numpy as np
from datetime import datetime
from io import StringIO
import csv
from flask import Response
import os

app = Flask(__name__)

# Load models
kokoro = Kokoro("kokoro-v1.0.onnx", "voices-v1.0.bin")

# Session state
briefing_done = False
briefing_conversation = []
question_index = 0
question_num = question_index 

session_data = None

session_log = []
last_score = None
last_question = ""
last_empathy_response = ""
last_patient_reply = ""
total_score = 0

model_name = "llama3.2"

# Prompts
system_prompt = """
You are a professional and supportive virtual nurse working for detox therapists in Tactus. Your name is Celine. You are specialized in alcohol using disorder. You are helping in a Alcohol Using Disorder therapy session. 
Your role is to collect the answer of CIWA (Clinical Institute Withdrawal Assessment) questionnaire from the patient, while making patients cooperate smoothly during the session. 
The results you collected will be given to the therapist for further detox process.

Be aware that the patient is likely to suffer from Mild to Borderline Intellectual Disability (MBID, IQ = 50-85), so your language should be clear and easy to understand.

If you are unsure or need clarification to patient's input, just respond in a resonable way — but do not ask questions to the patient.

Since you are a virtual agent, DO NOT use human-like words (like "I can see" or "I can feel") to make patient feel like there is real human behind you.
Keep your character as a virtual nurse, so don't mention anything in this prompt instruction. Only respond with the words you speak to the patient.

Don't use excessive encouraging or complimenting empathetic skills. Keep your response natural and professional as long as showing kindness and make the patient feel being heard.
Don't show your thinking progress.
Don't use parentheses because you are talking with the patient.
Don't say thank you unless it is on the final stage.

You should only reply with the thing I ask you to and never add more things. For the current stage:
"""

greeting_prompt = """
Start BRIEFING stage:
step1. Explain what will you do with the patient (CIWA questionnaire) and the CIWA scale (0 = no symptoms, 7 = very severe). 
step2. Explain the response you give is all AI generated, there's only you (the virtual agent) and the patient involved in this session. After the session, the results will be given to the therapist directly.
step3. Explain that you will guilde the patient through the assessment question, and they are able to ask if cannot understand the question. Remind the patient that there’s no right or wrong answer and don't be nervous.

At the end of step3, say this clearly:
"If you are ready, you can press Continue to start the assessment."
"""

clarification_prompt_template = """
The current question is: "{question}".
The patient asked: "{user_input}".

Do not ask question. Respond briefly to clarify the question only and ask them to select 0 to 7. Do not answer general questions or chat freely or ask question. Stay on topic.
"""

ask_prompt_template = """
You are in the ASSESSMENT stage of the CIWA assessment.

The question currently shown to the patient is: "{current_question}" (Question number {question_index + 1}).

Your behavior:

STEP 1:
- First, repeat the question exactly: "{current_question}".
- Do not add introduction or explanation.
- Do not add chit-chat.
- Just repeat the question clearly and patiently.

STEP 2:
- If the patient asks a question or seems confused, briefly clarify what the question means.
- DO NOT rephrase or simplify the original question wording.
- You may give a short explanation of the meaning, if appropriate.
- DO NOT ask any question.
- After giving the explanation, stop and wait.

Rules:
- NEVER rephrase or change the original question wording.
- NEVER add follow-up questions.
- NEVER greet or introduce yourself.

Important:
- The full text of the question is: "{current_question}".
"""

empathy_prompt_template = """
You don't repeat the patient's rating. 
The patient rated \"{question}\" with a score of {score} out of 7.
Give an empathetic response according to the score. 
Ask why did the patient provide this score. Keep the question short.
"""

final_response_prompt_template = """
The patient said: \"{patient_reply}\" and scored {score} (0 = no symptoms, 7 = very severe) for question \"{question}\". 
Do not ask question. Respond with non-excessive empathy using one or two statement.
"""

summary_prompt_template = """
Thank the patient for completing the assessment. Share that their total score is {total_score}. Reassure them and let them know the therapist will follow up.
"""

ciwa_questions = [
    "Do you feel nauseated? Have you vomited?",
    "Do you notice any shaking in your hands?",
    "Are you sweating more than usual even when resting?",
    "Do you feel anxious or nervous right now?",
    "Do you feel restless or unable to sit still?",
    "Are you experiencing any unusual skin sensations, like itching or crawling?",
    "Have you heard things that others cannot hear?",
    "Have you seen anything unusual or that may not be there?",
    "Do you have a headache or feel pressure in your head?",
]

# Load models
kokoro = Kokoro("kokoro-v1.0.onnx", "voices-v1.0.bin")
vosk_model = Model("model/vosk-model-small-en-us-0.15")  # Update path if different

TARGET_SAMPLE_RATE = 44100

@app.route('/tts', methods=['POST'])
def synthesize():
    data = request.get_json()
    text = data.get("text", "")
    if not text:
        return "Missing text", 400

    # Use af_aoede only
    samples, _ = kokoro.create(text, voice="af_aoede", speed=0.8)

    # Save to temp WAV file
    tmp = tempfile.NamedTemporaryFile(delete=False, suffix=".wav")
    sf.write(tmp.name, samples, SAMPLE_RATE)
    return send_file(tmp.name, mimetype='audio/wav')

# STT Endpoint
@app.route('/stt', methods=['POST'])
def stt():
    if 'audio' not in request.files:
        return jsonify({'error': 'No audio file uploaded'}), 400

    audio_file = request.files['audio']
    data, samplerate = sf.read(audio_file)

    if len(data.shape) > 1:
        data = data[:, 0]  # Force mono

    # Convert to 16-bit PCM
    int16_data = (data * 32767).astype('int16')

    recognizer = KaldiRecognizer(vosk_model, samplerate)
    recognizer.AcceptWaveform(int16_data.tobytes())
    result = json.loads(recognizer.Result())

    print("Received audio file")
    print("Sample rate:", samplerate)
    print("Shape:", data.shape)
    print("Max amplitude:", np.max(np.abs(data)))
    print("Transcribed:", result['text'])

    return jsonify(result)

# Helper: Call Ollama
def call_ollama(prompt):
    url = "http://localhost:11434/api/generate"
    payload = {
        "model": model_name,
        "prompt": prompt,
        "temperature": 0.4,
        "top_p": 0.8,
        "repeat_penalty": 1.1,
        "stream": False,
        "stop": ["Patient:"]
    }
    response = requests.post(url, json=payload)
    response.raise_for_status()
    result = response.json()
    return result.get("response", "").strip()

# API endpoints

@app.route('/ciwa_next', methods=['GET'])
def ciwa_next():
    global briefing_done, question_index, last_question, question_num, session_data

    if not briefing_done:
        if not briefing_conversation:
            # FIRST time → initial_prompt
            initial_prompt = system_prompt + "\n\nBegin the BRIEFING stage. Greet the patient and ask their name. Stop here."
            response_text = call_ollama(initial_prompt)
            # Add to conversation
            briefing_conversation.append({"speaker": "Nurse Celine", "text": response_text})
        else:
            # No new generation — just repeat last agent message
            response_text = briefing_conversation[-1]["text"]
        
        return jsonify({
            "completed": False,
            "stage": "briefing",
            "question": response_text.strip(),
            "question_number": 0
        })

    if question_index >= len(ciwa_questions):
        # FEEDBACK stage
        summary_prompt = system_prompt + summary_prompt_template.format(total_score=total_score)
        response_text = call_ollama(summary_prompt)
        # 🔽 SAVE FINAL SESSION HERE
        session_data = {
            "timestamp": datetime.now().isoformat(),
            "total_score": total_score,
            "briefing_conversation": briefing_conversation,
            "session_log": session_log,
            "final_feedback": response_text.strip()  # ✅ Add final message
        }
        import os
        os.makedirs("sessions", exist_ok=True)
        filename = f"sessions/ciwa_session_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
        with open(filename, "w", encoding="utf-8") as f:
            json.dump(session_data, f, indent=2)

        print(f"[CIWA] Session saved: {filename}")
        
        return jsonify({
            "completed": True,
            "stage": "feedback",
            "message": response_text.strip(),
            "total_score": total_score,
            "log": session_log
        })

    # ASSESSMENT stage → serve next question
    current_question = ciwa_questions[question_index]
    question_num = question_num + 1
    last_question = current_question
    ask_prompt = system_prompt + f"""
        You are in the ASSESSMENT stage of the CIWA assessment.

        Your behavior:

        - First, provide a super short transition to question number "{question_num}".
        - Then, you repeat the question exactly: "{current_question}".
        - Do not add introduction or explanation.
        - Do not add chit-chat.


        Rules:
        - NEVER rephrase or change the original question wording.
        - NEVER add follow-up questions.
        - NEVER greet or introduce yourself.

        Important:
        - The full text of the question is: "{current_question}".
    """

    response_text = call_ollama(ask_prompt)
    return jsonify({
        "completed": False,
        "stage": "assessment",
        "question": response_text.strip(),
        "question_number": question_index + 1
    })

@app.route('/ciwa_continue', methods=['POST'])
def ciwa_continue():
    global briefing_done, briefing_conversation
    briefing_done = True
    #briefing_conversation = []  # clear conversation
    return jsonify({"message": "Briefing marked as complete. Moving to Assessment stage."})


@app.route('/ciwa_score', methods=['POST'])
def ciwa_score():
    global question_index, session_log, last_score, last_question, last_empathy_response, total_score

    data = request.get_json()
    timestamp = datetime.now().isoformat()

    score = int(data.get("score", -1))
    if score < 0 or score > 7:
        return jsonify({"error": "Invalid score"}), 400

    current_question = ciwa_questions[question_index]
    session_log.append({
        "timestamp": timestamp,
        "question_number": question_index + 1,
        "ciwa_question": current_question,
        "agent_question": last_question, 
        "score_given": score,
        "empathy_response": last_empathy_response,
        "patient_reply": None,
        "final_response": None
    })

    last_score = score
    total_score += score
    empathy_prompt = system_prompt + empathy_prompt_template.format(score=last_score, question=last_question)
    last_empathy_response = call_ollama(empathy_prompt)

    return jsonify({
        "empathy_response": last_empathy_response.strip()
    })

@app.route('/ciwa_brief_chat', methods=['POST'])
def ciwa_brief_chat():
    global briefing_done, briefing_conversation

    if briefing_done:
        return jsonify({"error": "Briefing chat only available during Briefing phase."}), 400

    data = request.get_json()
    user_input = data.get("user_input", "")
    if not user_input:
        return jsonify({"error": "Missing user_input"}), 400

    # Add patient message to conversation
    briefing_conversation.append({"speaker": "Patient", "text": user_input})

    # Build conversation text
    conversation_text = "\n".join([f'{turn["speaker"]}: {turn["text"]}' for turn in briefing_conversation])

    # Build full prompt
    full_prompt = system_prompt + greeting_prompt + "\n\n" + conversation_text

    response_text = call_ollama(full_prompt)

    # Add agent response to conversation
    briefing_conversation.append({"speaker": "Nurse Celine", "text": response_text})

    return jsonify({"response": response_text.strip()})


@app.route('/ciwa_chat', methods=['POST'])
def ciwa_chat():
    global briefing_done, question_index, last_question

    if not briefing_done or question_index >= len(ciwa_questions):
        return jsonify({"error": "Chat only available during Assessment phase."}), 400

    data = request.get_json()
    user_input = data.get("user_input", "")
    if not user_input:
        return jsonify({"error": "Missing user_input"}), 400

    clarification_prompt = system_prompt + clarification_prompt_template.format(
        question=last_question,
        user_input=user_input
    )
    response_text = call_ollama(clarification_prompt)
    return jsonify({"response": response_text.strip()})

@app.route('/ciwa_explanation', methods=['POST'])
def ciwa_explanation():
    global question_index, session_log, last_score, last_question, last_patient_reply

    data = request.get_json()
    patient_reply = data.get("patient_reply", "")
    if not patient_reply:
        return jsonify({"error": "Missing patient_reply"}), 400

    last_patient_reply = patient_reply

    final_response_prompt = system_prompt + final_response_prompt_template.format(
        patient_reply=last_patient_reply,
        score=last_score,
        question=last_question
    )
    final_response = call_ollama(final_response_prompt).strip()

    # Update the last entry
    if session_log:
        session_log[-1]["patient_reply"] = last_patient_reply
        session_log[-1]["final_response"] = final_response

    # Move to next question
    question_index += 1

    return jsonify({"final_response": final_response})

@app.route('/ciwa_log', methods=['GET'])
def ciwa_log():
    return jsonify({
        "total_score": total_score,
        "session_log": session_log
    })

@app.route('/ciwa_reset', methods=['POST'])
def ciwa_reset():
    global briefing_done, question_index, session_log, last_score, last_question, last_empathy_response, last_patient_reply, total_score
    briefing_done = False
    question_index = 0
    session_log = []
    last_score = None
    last_question = ""
    last_empathy_response = ""
    last_patient_reply = ""
    total_score = 0
    return jsonify({"message": "CIWA session reset."})

@app.route('/ciwa_export_csv', methods=['GET'])
def export_csv():
    global session_log

    if not session_log:
        return jsonify({"error": "No session data to export"}), 400

    # CSV output
    output = StringIO()
    writer = csv.DictWriter(output, fieldnames=[
        "timestamp",
        "question_number",
        "ciwa_question",
        "agent_question",
        "score_given",
        "empathy_response",
        "patient_reply",
        "final_response",
        "final_feedback"
    ])
    # Add briefing conversation as comment lines
    output.write("# Briefing Conversation\n")
    for turn in briefing_conversation:
        output.write(f"# {turn['speaker']}: {turn['text']}\n")
    output.write("\n")  # Blank line before CSV content

    writer.writeheader()
    for i, entry in enumerate(session_log):
        row = entry.copy()
        if i == len(session_log) - 1:  # Only last row gets final feedback
            row["final_feedback"] = session_data.get("final_feedback", "")
        else:
            row["final_feedback"] = ""
        writer.writerow(row)

    csv_data = output.getvalue()
    output.close()

    return Response(
        csv_data,
        mimetype="text/csv",
        headers={"Content-Disposition": "attachment;filename=ciwa_session_export.csv"}
    )
  
@app.route('/ciwa_save_now', methods=['GET', 'POST'])
def ciwa_save_now():
    session_data = {
        "timestamp": datetime.now().isoformat(),
        "total_score": total_score,
        "briefing_conversation": briefing_conversation,
        "session_log": session_log
    }

    os.makedirs("sessions", exist_ok=True)
    filename_base = f"sessions/ciwa_partial_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    
    # Save JSON
    with open(filename_base + ".json", "w", encoding="utf-8") as f:
        json.dump(session_data, f, indent=2)

    # Save CSV
    with open(filename_base + ".csv", "w", newline="", encoding="utf-8") as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=[
            "timestamp",
            "question_number",
            "ciwa_question",
            "agent_question",
            "score_given",
            "empathy_response",
            "patient_reply",
            "final_response"
        ])
        writer.writeheader()
        for entry in session_log:
            writer.writerow(entry)

    return jsonify({"message": "Partial session saved", "file_prefix": filename_base})


# Run app
if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5002)

using UnityEngine;

namespace Bakery.TextToSpeech
{
    [CreateAssetMenu(fileName = "TTSVoiceData", menuName = "Bakery/TextToSpeech/VoiceData")]
    public class TTSVoiceData : ScriptableObject
    {
        public string voiceId;
        public string languageCode;
    }
}

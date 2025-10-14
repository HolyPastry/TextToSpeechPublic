
using UnityEngine;

namespace Bakery.TextToSpeech
{
    [CreateAssetMenu(fileName = "TTSVoiceParams", menuName = "Bakery/TextToSpeech/TTSVoiceParams", order = 1)]
    public class TTSVoiceParams : ScriptableObject
    {
        public string Uri;
        public int ProjectId;
        public string ProductName;
        public string CompanyName;
    }
}

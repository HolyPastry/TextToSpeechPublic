
using UnityEngine;

namespace Bakery.TextToSpeech
{
    [CreateAssetMenu(fileName = "TTSVoiceParams", menuName = "Bakery/TextToSpeech/TTSVoiceParams", order = 1)]
    public class TTSSettings : ScriptableObject
    {
        [Header("API Settings")]
        public string Uri;
        public int ProjectId;
        public string ProductName;
        public string CompanyName;

        [Header("Manager Settings")]
        public int VerboseMode;
        public int CacheSize = 1000;
        public int JobQueueSize = 10000;
    }
}

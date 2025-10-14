using UnityEngine;
using System.Collections;

namespace Bakery.TextToSpeech
{

    [RequireComponent(typeof(AudioSource))]
    public class TTSVoice : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private TTSVoiceData _voiceId;

        public void SetVoice(TTSVoiceData voiceId) => _voiceId = voiceId;
        public float Length => _audioSource.clip != null ? _audioSource.clip.length : -1;

        //return length of the audio clip
        public IEnumerator SpeakRoutine(string line)
        {
            long jobId = TTSServices.LoadVoice(_voiceId, line);
            yield return TTSServices.WaitForVoice(jobId);
            _audioSource.clip = TTSServices.GetLoadedClip(jobId);
            _audioSource.Play();
        }
    }

}
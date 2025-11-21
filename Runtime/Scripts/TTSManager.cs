using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace Bakery.TextToSpeech
{
    public class TTSManager : MonoBehaviour
    {
        [SerializeField] private TTSVoiceParams _voiceParams;
        [SerializeField] private int _verboseMode;
        [SerializeField] private int _cacheSize = 1000;
        [SerializeField] private int _jobQueueSize = 10000;
        private readonly List<long> _jobs = new();
        private readonly List<long> _failedJobs = new();
        private readonly Dictionary<long, AudioClip> _cache = new();

        private long _jobIdCounter = 0;

        void Awake()
        {
            CreateTTSDirectory();
        }

        void OnDisable()
        {
            TTSServices.WaitForVoice = delegate { Debug.Log("AIVoice Manager has been unloaded"); return null; };
            TTSServices.GetLoadedClip = delegate { Debug.Log("AIVoice Manager has been unloaded"); return null; };
            TTSServices.LoadVoice = delegate { Debug.Log("AIVoice Manager has been unloaded"); return -1; };
            Verbose = delegate { };
        }

        void OnEnable()
        {
            TTSServices.LoadVoice = PreloadVoice;
            TTSServices.WaitForVoice = WaitForVoice;
            TTSServices.GetLoadedClip = GetAudioClip;
            Verbose = VerboseHandler;
        }

        private void VerboseHandler(int level, string message)
        {
            if (_verboseMode < level) return;

            if (_verboseMode == 1)
                Debug.LogWarning(message);
            else
                Debug.Log(message);

        }

        private static Action<int, string> Verbose = delegate { };

        private static void CreateTTSDirectory()
        {
            if (!Directory.Exists(TTSServices.PersistentFolder))
            {
                Verbose(3, "TTS - Creating Persistent Folder");
                Directory.CreateDirectory(TTSServices.PersistentFolder);
            }
        }

        private AudioClip GetAudioClip(long jobId)
        {
            _failedJobs.Remove(jobId);
            if (!_cache.TryGetValue(jobId, out AudioClip audioClip))
            {
                Verbose(1, $"TTS - {jobId} - AudioClip not cached");
                return null;
            }
            if (audioClip != null)
            {
                Verbose(2, $"TTS - {jobId} - AudioClip retrieved.");
                return audioClip;
            }
            Verbose(1, $"TTS - {jobId} - AudioClip unexpectedly came back null.");
            return null;
        }

        private CustomYieldInstruction WaitForVoice(long jobId)
        {
            return new WaitUntil(
                () => _failedJobs.Contains(jobId) || _cache.ContainsKey(jobId));
        }

        private long PreloadVoice(TTSVoiceData voiceData, string line)
        {
            if (line.Trim().Length == 0)
            {
                Verbose(1, "Text is empty");
                return -1;
            }
            CullCache();
            CullJobs();

            var filename = TTSServices.TextToFileName(line, voiceData.voiceId);

            long jobId = _jobIdCounter++;

            //Debug.Log("NumJObInProgress: " + _jobs.Count);
            PreLoadVoiceAsync(jobId, voiceData, line, filename);

            return jobId;
        }

        private void CullJobs()
        {
            if (_jobs.Count < _jobQueueSize)
                return;

            Verbose(3, $"TTS - Job queue size exceeded: {_jobs.Count}, culling oldest jobs");

            while (_jobs.Count > _jobQueueSize)
            {
                long oldestJobId = _jobs[0];
                _jobs.RemoveAt(0);
                if (_cache.ContainsKey(oldestJobId))
                    _cache.Remove(oldestJobId);
            }
        }

        private void CullCache()
        {
            if (_cache.Count < _cacheSize)
                return;

            Verbose(3, $"TTS - Cache size exceeded: {_cache.Count}, culling oldest entries");

            while (_cache.Count > _cacheSize)
            {
                long oldestKey = _cache.Keys.Min();
                _cache.Remove(oldestKey);
            }
        }

        private async void PreLoadVoiceAsync(long jobId, TTSVoiceData voiceData, string text, string filename)
        {

            AudioClip audioClip;
            if (File.Exists(TTSServices.StreamingFilePath(filename)))
            {
                Verbose(3, $"TTS - {jobId} - found in StreamingAssets: {text}");
                audioClip = await TTSServices.LoadAudioClipFromLocal(TTSServices.StreamingFilePath(filename));
            }
            else if (File.Exists(TTSServices.PersistentFilePath(filename)))
            {
                Verbose(3, $"TTS - {jobId} - found in PersistentData: {text}");
                audioClip = await TTSServices.LoadAudioClipFromLocal(TTSServices.PersistentFilePath(filename));
            }
            else
            {

                Verbose(3, $"TTS - {jobId} - awaits service availability");
                await ServiceIsAvailable();
                _jobs.Add(jobId);
                audioClip = await CreateAudio(jobId, filename, text, voiceData.voiceId);
                _jobs.Remove(jobId);
            }

            if (audioClip != null)
                _cache.Add(jobId, audioClip);
            else
            {
                Verbose(1, $"TTS - {jobId} - Failed to create audio clip for: {text}");
                _failedJobs.Add(jobId);
            }

        }

        private async Task ServiceIsAvailable()
        {
            while (_jobs.Count >= 4)
                await Task.Yield();
        }

        [Serializable]
        public struct WebRequestParams
        {

            public string filename;
            public string voice;
            public string text;
            public string thingy;
            public string id;
        }
        private async Task<AudioClip> CreateAudio(long jobId, string filename, string text, string voiceId)
        {

            WebRequestParams @params = new()
            {
                filename = filename,
                voice = voiceId,
                text = text,
                id = _voiceParams.CompanyName + "-" + _voiceParams.ProductName + "-" + _voiceParams.ProjectId
            };
            Verbose(2, $"TTS - {jobId} - Sending audio request");
            byte[] mp3Data = await SendRequest(@params, _voiceParams.Uri);
            if (mp3Data == null)
            {
                Verbose(1, "Failed to get mp3 data from the clouds: " + voiceId);
                return null;
            }
            Verbose(2, $"TTS - {jobId} - Received {filename}, saving to Disk");
            SaveMp3ToPersistentData(filename, mp3Data);
            return await TTSServices.LoadAudioClipFromLocal(TTSServices.PersistentFilePath(filename));
        }

        private static async Task<byte[]> SendRequest(WebRequestParams @params, string uri)
        {
            try
            {
                UnityWebRequest request;
                request = UnityWebRequest.Post(uri
                                    , JsonUtility.ToJson(@params).ToString()
                                    , "application/json");
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SendWebRequest();

                while (request.result == UnityWebRequest.Result.InProgress)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(request.error);
                    return null;
                }

                return request.downloadHandler.data;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                return null;
            }
        }

        private void SaveMp3ToPersistentData(string filename, byte[] mp3Data)
        {

            string path = TTSServices.PersistentFilePath(filename);
            File.WriteAllBytes(path, mp3Data);
            // Debug.Log($"MP3 file saved to: {path}");
        }


    }

}
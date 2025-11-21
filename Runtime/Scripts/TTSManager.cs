using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace Bakery.TextToSpeech
{

    public class TTSManager
    {
        [SerializeField] private TTSSettings _settings;

        private readonly List<long> _jobs = new();
        private readonly List<long> _failedJobs = new();
        private readonly Dictionary<long, AudioClip> _cache = new();

        private long _jobIdCounter = 0;

        public TTSManager()
        {
            CreateTTSDirectory();
            _settings = Resources.Load<TTSSettings>("TTSSettings");
            if (_settings == null)
                Debug.LogError("TTSSettings asset not found in Resources folder.");
        }

        private void Verbose(int level, string message)
        {
            if (_settings.VerboseMode < level) return;

            if (_settings.VerboseMode == 1)
                Debug.LogWarning(message);
            else
                Debug.Log(message);

        }
        public static string TTSFolder = "TTS";
        public static string PersistentFolder => Path.Combine(Application.persistentDataPath, TTSFolder);
        public static string StreamingFolder => Path.Combine(Application.streamingAssetsPath, TTSFolder);

        public static string PersistentFilePath(string filename)
            => Path.Combine(PersistentFolder, $"{filename}.mp3");

        public static string StreamingFilePath(string filename)
            => Path.Combine(StreamingFolder, $"{filename}.mp3");



        public static string TextToFileName(string text, string voiceId)
        {
            text = voiceId.ToString() + text.ToLower();
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public static async Task<AudioClip> LoadAudioClipFromLocal(string filename)
        {
            //Debug.Log($"Loading from: {filename}");
            using var unityWebRequest =
                UnityWebRequestMultimedia.GetAudioClip(new Uri(filename), AudioType.MPEG);

            var operation = unityWebRequest.SendWebRequest();

            while (!operation.isDone) await Task.Yield();
            if (operation.webRequest.error != null)
                Debug.LogWarning(operation.webRequest.error);

            return DownloadHandlerAudioClip.GetContent(unityWebRequest);
        }

        public static bool FileExistsLocally(string filename)
        {
            return File.Exists(StreamingFilePath(filename))
               || File.Exists(PersistentFilePath(filename));
        }
        private void CreateTTSDirectory()
        {
            if (!Directory.Exists(TTSManager.PersistentFolder))
            {
                Verbose(3, "TTS - Creating Persistent Folder");
                Directory.CreateDirectory(TTSManager.PersistentFolder);
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

        public async Task<AudioClip> GetVoiceAsync(TTSVoiceData voiceData, string line)
        {
            if (line.Trim().Length == 0)
            {
                Verbose(1, "Text is empty");
                return null;
            }
            CullCache();
            CullJobs();

            var filename = TTSManager.TextToFileName(line, voiceData.voiceId);

            long jobId = _jobIdCounter++;

            //Debug.Log("NumJObInProgress: " + _jobs.Count);
            await PreLoadVoiceAsync(jobId, voiceData, line, filename);
            while (!_failedJobs.Contains(jobId) && !_cache.ContainsKey(jobId))
                await Task.Yield();

            if (_failedJobs.Contains(jobId))
                return null;

            return _cache[jobId];
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

            var filename = TTSManager.TextToFileName(line, voiceData.voiceId);

            long jobId = _jobIdCounter++;

            _ = PreLoadVoiceAsync(jobId, voiceData, line, filename);

            return jobId;
        }

        private void CullJobs()
        {
            if (_jobs.Count < _settings.JobQueueSize)
                return;

            Verbose(3, $"TTS - Job queue size exceeded: {_jobs.Count}, culling oldest jobs");

            while (_jobs.Count > _settings.JobQueueSize)
            {
                long oldestJobId = _jobs[0];
                _jobs.RemoveAt(0);
                if (_cache.ContainsKey(oldestJobId))
                    _cache.Remove(oldestJobId);
            }
        }

        private void CullCache()
        {
            if (_cache.Count < _settings.CacheSize)
                return;

            Verbose(3, $"TTS - Cache size exceeded: {_cache.Count}, culling oldest entries");

            while (_cache.Count > _settings.CacheSize)
            {
                long oldestKey = _cache.Keys.Min();
                _cache.Remove(oldestKey);
            }
        }

        private async Task<long> PreLoadVoiceAsync(long jobId, TTSVoiceData voiceData, string text, string filename)
        {

            AudioClip audioClip;
            if (File.Exists(TTSManager.StreamingFilePath(filename)))
            {
                Verbose(3, $"TTS - {jobId} - found in StreamingAssets: {text}");
                audioClip = await TTSManager.LoadAudioClipFromLocal(TTSManager.StreamingFilePath(filename));
            }
            else if (File.Exists(TTSManager.PersistentFilePath(filename)))
            {
                Verbose(3, $"TTS - {jobId} - found in PersistentData: {text}");
                audioClip = await TTSManager.LoadAudioClipFromLocal(TTSManager.PersistentFilePath(filename));
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
            return jobId;

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
                id = _settings.CompanyName + "-" + _settings.ProductName + "-" + _settings.ProjectId
            };
            Verbose(2, $"TTS - {jobId} - Sending audio request");
            byte[] mp3Data = await SendRequest(@params, _settings.Uri);
            if (mp3Data == null)
            {
                Verbose(1, "Failed to get mp3 data from the clouds: " + voiceId);
                return null;
            }
            Verbose(2, $"TTS - {jobId} - Received {filename}, saving to Disk");
            SaveMp3ToPersistentData(filename, mp3Data);
            return await TTSManager.LoadAudioClipFromLocal(TTSManager.PersistentFilePath(filename));
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

            string path = TTSManager.PersistentFilePath(filename);
            File.WriteAllBytes(path, mp3Data);
            // Debug.Log($"MP3 file saved to: {path}");
        }


    }

}
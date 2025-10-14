using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Security.Cryptography;
using System.IO;


namespace Bakery.TextToSpeech
{
    public class TTSServices
    {
        public static Func<long, CustomYieldInstruction> WaitForVoice = delegate
        {
            Debug.Log("AIVoice Manager is Missing");
            return new WaitUntil(() => true);
        };

        public static Func<long, AudioClip> GetLoadedClip = delegate { Debug.Log("AIVoice Manager is Missing"); return null; };

        public static Func<TTSVoiceData, string, long> LoadVoice = delegate { Debug.Log("AIVoice Manager is Missing"); return -1; };
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

    }

}
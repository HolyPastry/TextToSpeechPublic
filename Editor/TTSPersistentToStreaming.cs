

#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bakery.TextToSpeech
{

    public static class PersistentToResource
    {
        [MenuItem("Bakery/TextToSpeech/Convert TTS to Streaming")]
        public static void ConvertTTSToResource()
        {
            ConvertToStreaming(TTSServices.TTSFolder);
        }

        [MenuItem("Bakery/TextToSpeech/DeleteStreamingAssets")]
        public static void DeleteStreamingAssets()
        {
            if (Directory.Exists(Application.streamingAssetsPath + "/" + TTSServices.TTSFolder))
                Directory.Delete(Application.streamingAssetsPath + "/" + TTSServices.TTSFolder, true);
            AssetDatabase.Refresh();
        }

        [MenuItem("Bakery/TextToSpeech/DeletePersistentData")]
        public static void DeletePersistentData()
        {
            if (Directory.Exists(Application.persistentDataPath + "/" + TTSServices.TTSFolder))
                Directory.Delete(Application.persistentDataPath + "/" + TTSServices.TTSFolder, true);
        }

        public static void ConvertToStreaming(string folderName)
        {
            CreateDirectoryIfMissing(folderName);

            string[] files = Directory.GetFiles(Application.persistentDataPath + "/" + folderName);
            foreach (var file in files)
            {

                string filename = Path.GetFileName(file);
                if (file.StartsWith(".") ||
                    File.Exists(Application.streamingAssetsPath + "/" + folderName + "/" + filename))
                    continue;

                FileUtil.CopyFileOrDirectory(file, Application.streamingAssetsPath + "/" + folderName + "/" + filename);
            }
            AssetDatabase.Refresh();
        }

        public static void ConvertFileToStreaming(string filePath, bool overrideFile)
        {
            CreateDirectoryIfMissing(TTSServices.TTSFolder);

            string filename = Path.GetFileName(filePath);
            if (File.Exists(TTSServices.StreamingFilePath(filename)))
                return;

            FileUtil.CopyFileOrDirectory(filePath, TTSServices.StreamingFilePath(filename));
        }

        private static void CreateDirectoryIfMissing(string folderName)
        {
            if (!Directory.Exists(Application.streamingAssetsPath))
                Directory.CreateDirectory(Application.streamingAssetsPath);
            if (!Directory.Exists(Application.streamingAssetsPath + "/" + folderName))
                Directory.CreateDirectory(Application.streamingAssetsPath + "/" + folderName);
        }
    }
}
#endif
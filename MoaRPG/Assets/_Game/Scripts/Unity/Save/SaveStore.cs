using System;
using System.IO;
using System.Text;
using Moa.Core.Growth;
using UnityEngine;

namespace Moa.Unity.Save
{
    /// <summary>저장 파일을 읽은 결과 (3-5).</summary>
    public enum SaveLoadStatus
    {
        /// <summary>파일이 없다 — 새 계정.</summary>
        Missing,
        Loaded,
        /// <summary>읽을 수 없거나 형식이 맞지 않았다 — 파일을 백업으로 옮겼고 새 계정으로 시작한다.</summary>
        Broken,
    }

    /// <summary>
    /// 저장 파일 한 곳 (3-5): JSON(JsonUtility) 하나, 기본 자리 Application.persistentDataPath/moa_save.json, PlayerPrefs는 쓰지 않는다.
    /// 쓰기는 같은 폴더의 .tmp에 쓴 뒤 바꿔 끼운다(쓰다 끊겨도 앞 파일이 남음). 읽다가 깨졌으면(JSON 오류 · 형식 번호 · 필수 칸)
    /// 원래 파일을 "이름.broken-날짜.json"으로 옮겨 두고 Broken — 부르는 쪽은 새 계정(0)으로 시작한다.
    /// 테스트는 임시 경로로 만든다.
    /// </summary>
    public sealed class SaveStore
    {
        public const string FileName = "moa_save.json";

        public SaveStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("save path is empty", nameof(path));
            FilePath = path;
        }

        /// <summary>사용자 저장 파일 자리.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public string FilePath { get; }
        /// <summary>마지막으로 깨진 파일을 옮긴 자리 (없으면 null) · 마지막 오류 (없으면 null).</summary>
        public string LastBackupPath { get; private set; }
        public string LastError { get; private set; }
        public int SaveCount { get; private set; }

        public SaveLoadStatus Load(out SaveSnapshot snapshot)
        {
            snapshot = null;
            LastError = null;
            if (!File.Exists(FilePath)) return SaveLoadStatus.Missing;
            SaveSnapshot s = null;
            try
            {
                string text = File.ReadAllText(FilePath, Encoding.UTF8);
                s = string.IsNullOrWhiteSpace(text) ? null : JsonUtility.FromJson<SaveSnapshot>(text);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                s = null;
            }
            if (!SaveFormat.Validate(s, out string reason))
            {
                if (LastError == null) LastError = reason;
                Backup();
                return SaveLoadStatus.Broken;
            }
            snapshot = s;
            return SaveLoadStatus.Loaded;
        }

        /// <summary>스냅샷을 쓴다. 되면 true (실패하면 LastError).</summary>
        public bool Save(SaveSnapshot snapshot)
        {
            if (snapshot == null) return false;
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
                SaveCount++;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        void Backup()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath) ?? "";
                string name = Path.GetFileNameWithoutExtension(FilePath);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string target = Path.Combine(dir, name + ".broken-" + stamp + ".json");
                for (int n = 1; File.Exists(target); n++) target = Path.Combine(dir, name + ".broken-" + stamp + "-" + n + ".json");
                File.Move(FilePath, target);
                LastBackupPath = target;
            }
            catch (Exception e)
            {
                LastError = (LastError != null ? LastError + "; " : "") + "backup failed: " + e.Message;
                LastBackupPath = null;
            }
        }
    }
}

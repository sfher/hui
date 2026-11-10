using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 重生点自动存档的磁盘读写。位置固定在 Application.persistentDataPath：
/// checkpoint.json / checkpoint.backup.json / checkpoint.tmp。
/// 与 LabSettingsStore 同一路数：先写临时文件再替换正式档并留备份；
/// 读取走正式档 → 备份 → null；解析失败不覆盖原文件，保留现场。
/// 测试用 DirectoryOverride 指向临时目录，避免碰真实存档。
/// </summary>
public static class LabCheckpointStore
{
    public const int CurrentVersion = 1;
    const string FileName = "checkpoint.json";
    const string BackupFileName = "checkpoint.backup.json";
    const string TempFileName = "checkpoint.tmp";

    /// <summary>测试/工具可改写到临时目录；为空时使用 Application.persistentDataPath。</summary>
    public static string DirectoryOverride;

    /// <summary>读档后待消费的续玩点：主页设置，TuningLab 进关第一帧拿走并清掉。</summary>
    public static Vector2? PendingContinue;

    /// <summary>存档内容：场景名 + 重生点位置。复活状态由 ForceRespawn 规则统一决定。</summary>
    [Serializable]
    public sealed class Data
    {
        public int version = CurrentVersion;
        public string scene;
        public Vector2 checkpoint;
    }

    public static string DataDirectory =>
        string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;

    public static string SavePath => Path.Combine(DataDirectory, FileName);
    public static string BackupPath => Path.Combine(DataDirectory, BackupFileName);
    public static string TempPath => Path.Combine(DataDirectory, TempFileName);

    public static bool Exists => File.Exists(SavePath) || File.Exists(BackupPath);

    /// <summary>激活重生点时自动调用；失败只告警，不影响玩法。</summary>
    public static bool Save(string scene, Vector2 checkpoint)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        var data = new Data { scene = scene, checkpoint = checkpoint };

        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Replace(TempPath, SavePath, BackupPath);
            else File.Move(TempPath, SavePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabCheckpointStore: 存档失败（{SavePath}）：{e.Message}");
            return false;
        }
    }

    /// <summary>没有可用存档时返回 null；正式档损坏时退备份。</summary>
    public static Data Load()
    {
        if (TryRead(SavePath, out var data)) return data;
        if (TryRead(BackupPath, out data))
        {
            Debug.LogWarning("LabCheckpointStore: 正式存档不可用，已读取备份。");
            return data;
        }
        return null;
    }

    /// <summary>新游戏覆盖旧档：正式档、备份和残留临时文件一起删。</summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabCheckpointStore: 删除存档失败：{e.Message}");
        }
    }

    static bool TryRead(string path, out Data data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            var parsed = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            if (parsed == null || string.IsNullOrEmpty(parsed.scene))
            {
                Debug.LogWarning($"LabCheckpointStore: 存档为空或缺少场景名（{path}）。");
                return false;
            }
            data = parsed;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabCheckpointStore: 读取存档失败（{path}）：{e.Message}");
            data = null;
            return false;
        }
    }
}
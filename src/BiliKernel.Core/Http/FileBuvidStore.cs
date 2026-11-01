// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Authorization;
using System.Diagnostics;

namespace Richasy.BiliKernel.Http;

/// <summary>
/// 基于本地文件的设备标识存储。
/// </summary>
public sealed class FileBuvidStore : IBuvidStore
{
    private readonly string _filePath;

    private string? _buvid3;
    private string? _buvid4;
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBuvidStore"/> class.
    /// </summary>
    public FileBuvidStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BiliKernel"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBuvidStore"/> class.
    /// </summary>
    /// <param name="directory">存放设备标识的目录。</param>
    public FileBuvidStore(string directory)
    {
        Verify.NotNullOrWhiteSpace(directory);
        _filePath = Path.Combine(directory, "buvid.txt");
    }

    /// <inheritdoc/>
    public string? Buvid3
    {
        get
        {
            Load();
            return _buvid3;
        }
    }

    /// <inheritdoc/>
    public string? Buvid4
    {
        get
        {
            Load();
            return _buvid4;
        }
    }

    /// <inheritdoc/>
    public void Save(string buvid3, string? buvid4)
    {
        Verify.NotNullOrWhiteSpace(buvid3);
        _buvid3 = buvid3;
        _buvid4 = buvid4;
        _loaded = true;
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_filePath, [buvid3, buvid4 ?? string.Empty]);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"保存设备标识失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"保存设备标识失败：{ex.Message}");
        }
    }

    private void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var lines = File.ReadAllLines(_filePath);
            _buvid3 = lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]) ? lines[0] : null;
            _buvid4 = lines.Length > 1 && !string.IsNullOrWhiteSpace(lines[1]) ? lines[1] : null;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"读取设备标识失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"读取设备标识失败：{ex.Message}");
        }
    }
}
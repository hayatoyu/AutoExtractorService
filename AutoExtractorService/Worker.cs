using System.Diagnostics;
using Microsoft.Extensions.Options;


namespace AutoExtractorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ExtractorOptions _options;

        private readonly HashSet<string> _videoExts, _subExts, _archiveExts;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private FileSystemWatcher? _watcher;

        public Worker(ILogger<Worker> logger, IOptions<ExtractorOptions> options)
        {
            _logger = logger;
            _options = options.Value;

            _videoExts = new HashSet<string>(_options.VideoExtensions, StringComparer.OrdinalIgnoreCase);
            _subExts = new HashSet<string>(_options.SubtitleExtensions, StringComparer.OrdinalIgnoreCase);
            _archiveExts = new HashSet<string>(_options.ArchiveExtensions, StringComparer.OrdinalIgnoreCase);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Directory.Exists(_options.WatchFolder))
            {
                Directory.CreateDirectory(_options.WatchFolder);
            }

            _watcher = new FileSystemWatcher(_options.WatchFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Renamed += OnFileRenamed;

            _logger.LogInformation("AutoExtractorService 已啟動，監控目錄：{WatcherFolder}", _options.WatchFolder);

            return Task.CompletedTask;
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            CheckAndProcess(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            CheckAndProcess(e.FullPath);
        }

        private void CheckAndProcess(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            if(!_archiveExts.Contains(ext))
            {
                _logger.LogInformation("檔案 {FilePath} 不是支援的壓縮檔格式，忽略處理。", filePath);
                return;
            }
            Task.Run(async () =>
            {
                _logger.LogInformation("檔案已排入佇列：{FilePath}", Path.GetFileName(filePath));

                // 等待前一個檔案解壓縮完成
                await _semaphore.WaitAsync();
                try
                {
                    ProcessArchive(filePath);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        private void ProcessArchive(string archivePath)
        {
            try
            {
                WaitForFileUnlock(archivePath);

                var archiveInfo = new FileInfo(archivePath);
                if (!archiveInfo.Exists) return;

                string folderName = Path.GetFileNameWithoutExtension(archiveInfo.Name);
                string targetDir = Path.Combine(archiveInfo.DirectoryName!, folderName);

                _logger.LogInformation("開始解壓縮：{ArchiveName}", archiveInfo.Name);

                // 呼叫 7-Zip 進行解壓縮
                var startInfo = new ProcessStartInfo
                {
                    FileName = _options.SevenZipPath,
                    Arguments = $"x \"{archivePath}\" -o\"{targetDir}\" -p{_options.Password} -y -sdel",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using(var process = Process.Start(startInfo))
                {
                    process?.WaitForExit();
                    if(process.ExitCode == 0)
                    {
                        File.Delete(archivePath);
                    }
                    else
                    {
                        throw new Exception($"解壓縮失敗，7-Zip 回傳錯誤碼：{process.ExitCode}");
                    }
                }

                // 處理解壓縮後的檔案
                if (Directory.Exists(targetDir))
                {
                    var dirInfo = new DirectoryInfo(targetDir);

                    // Step A: 影片檔改名加 .bak
                    foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                    {
                        if (_videoExts.Contains(file.Extension))
                        {
                            string newPath = file.FullName + ".bak";
                            file.MoveTo(newPath);
                            _logger.LogInformation("影片檔 {FileName} 已改名為 {NewFileName}", file.FullName, newPath);
                        }
                    }

                    // Step B: 刪除除了 .bak 影片與字幕檔以外的所有檔案
                    foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                    {
                        bool isBakVideo = file.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase);
                        bool isSubtitle = _subExts.Contains(file.Extension);

                        if(!isBakVideo && !isSubtitle)
                        {
                            file.Delete();
                            _logger.LogInformation("刪除檔案 {FileName}", file.FullName);
                        }
                    }

                    _logger.LogInformation("處理完成：{FolderName}", folderName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理壓縮檔 {ArchivePath} 時發生錯誤。", archivePath);
            }
        }

        private static void WaitForFileUnlock(string filePath)
        {
            while (true)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (stream != null)
                    {
                        break;
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(1000);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        public override void Dispose()
        {
            _watcher?.Dispose();
            base.Dispose();
        }
    }
}

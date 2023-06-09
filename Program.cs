﻿using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace AutoUnrar
{
    class Program
    {
        private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".flv", ".mov", ".wmv" };

        private static bool ContainsVideoFile(string folderPath)
        {
            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            return allFiles.Any(file => VideoExtensions.Contains(Path.GetExtension(file).ToLower()));
        }
        
        //This checks the file size and if its changing, retries if it is
        private static async Task WaitForFileDownloadCompletion(string filePath, int intervalSeconds, int maxRetries)
        {
            int retries = 0;
            long previousSize = -1;
            FileInfo fileInfo = new FileInfo(filePath);

            while (retries < maxRetries)
            {
                fileInfo.Refresh();

                if (previousSize == fileInfo.Length)
                {
                    retries++;
                    Console.WriteLine($"File size is changing...Waiting for download to complete, Interval: {intervalSeconds} seconds | retries: {retries} | max retries: {maxRetries}");
                }
                else
                {
                    retries = 0;
                }

                previousSize = fileInfo.Length;
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            }
        }
        private static async Task<bool> IsFileInUse(string filePath)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30)); // Add a 30-second delay before checking if the file is in use
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        private static async Task ExtractRarFile(string rarFilePath)
        {
            var folderPath = Path.GetDirectoryName(rarFilePath);
            if (!ContainsVideoFile(folderPath))
            {
                Console.WriteLine($"Found a path that doesn't have a video file! {folderPath}");
                Console.WriteLine($"Extracting RAR: {rarFilePath}");
                if (await IsFileInUse(rarFilePath))
                {
                    await WaitForFileDownloadCompletion(rarFilePath, 30, 20); // Wait for the file download to complete
                    if (await IsFileInUse(rarFilePath))
                    {
                        Console.WriteLine($"Skipping extraction for {rarFilePath} as it is in use by another process.");
                        return;
                    }
                }
                try
                {
                    using var archive = RarArchive.Open(rarFilePath);
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(folderPath, new ExtractionOptions { Overwrite = true });
                    }
                    Console.WriteLine($"Successfully extracted: {rarFilePath}");
                    Console.WriteLine("Monitoring for new .rar files. Press 'q' to exit.");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to extract {rarFilePath}: {e}");
                }
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("Monitoring for new .rar files. Press 'q' to exit.");

            var folderPath = "C:\\UnZip test";
            var rarFiles = Directory.GetFiles(folderPath, "*.rar", SearchOption.AllDirectories);

            foreach (var rarFile in rarFiles)
            {
                await ExtractRarFile(rarFile);
            }

            using var watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.rar",
                IncludeSubdirectories = true
            };

            watcher.Created += async (sender, eventArgs) => await ExtractRarFile(eventArgs.FullPath);
            watcher.EnableRaisingEvents = true;

            using var changeWatcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*",
                IncludeSubdirectories = true
            };

            changeWatcher.Changed += async (sender, eventArgs) => await ExtractRarFile(eventArgs.FullPath);
            changeWatcher.EnableRaisingEvents = true;

            while (Console.Read() != 'q') ;
        }
    }
}
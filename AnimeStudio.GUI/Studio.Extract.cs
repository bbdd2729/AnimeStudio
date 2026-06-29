using System;
using System.Collections.Generic;
using System.IO;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
        public static int ExtractFolder(string path, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileOriPath = Path.GetDirectoryName(file);
                var fileSavePath = fileOriPath.Replace(path, savePath);
                extractedCount += ExtractFile(file, fileSavePath);
                Progress.Report(i + 1, files.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string[] fileNames, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                var fileName = fileNames[i];
                extractedCount += ExtractFile(fileName, savePath);
                Progress.Report(i + 1, fileNames.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string fileName, string savePath)
        {
            int extractedCount = 0;
            var reader = new FileReader(fileName);
            reader = reader.PreProcessing(Game);
            if (reader.FileType == FileType.BundleFile)
                extractedCount += ExtractBundleFile(reader, savePath);
            else if (reader.FileType == FileType.WebFile)
                extractedCount += ExtractWebDataFile(reader, savePath);
            else if (reader.FileType == FileType.BlkFile)
                extractedCount += ExtractBlkFile(reader, savePath);
            else if (reader.FileType == FileType.BlockFile)
                extractedCount += ExtractBlockFile(reader, savePath);
            else if (reader.FileType == FileType.Blb3File)
                extractedCount += ExtractBlb3File(reader, savePath);
            else if (reader.FileType == FileType.VFSFile)
                extractedCount += ExtractVFSFile(reader, savePath);
            else
                reader.Dispose();
            return extractedCount;
        }
        private static int ExtractBlb3File(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var bundleFile = new Blb3File(reader, reader.FullPath);
                reader.Dispose();
                if (bundleFile.fileList != null && bundleFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, bundleFile.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mr0k)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
        }
        private static int ExtractBundleFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var bundleFile = new BundleFile(reader, Game);
                reader.Dispose();
                if (bundleFile.fileList != null && bundleFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, bundleFile.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mr0k)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
        }

        private static int ExtractWebDataFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            var webFile = new WebFile(reader);
            reader.Dispose();
            if (webFile.fileList != null && webFile.fileList.Count > 0)
            {
                var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                return ExtractStreamFile(extractPath, webFile.fileList);
            }
            return 0;
        }

        private static int ExtractBlkFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                using var stream = BlkUtils.Decrypt(reader, (Blk)Game);
                do
                {
                    stream.Offset = stream.AbsolutePosition;
                    var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                    var subReader = new FileReader(dummyPath, stream, true);
                    var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    switch (subReader.FileType)
                    {
                        case FileType.BundleFile:
                            total += ExtractBundleFile(subReader, subSavePath);
                            break;
                        case FileType.MhyFile:
                            total += ExtractMhyFile(subReader, subSavePath);
                            break;
                    }
                } while (stream.Remaining > 0);
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Blk)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return total;
        }

        private static int ExtractBlockFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            using var stream = new OffsetStream(reader.BaseStream, 0);
            do
            {
                stream.Offset = stream.AbsolutePosition;
                var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
                var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                var subReader = new FileReader(dummyPath, stream, true);
                if (subReader.FileType == FileType.Blb3File)
                    total += ExtractBlb3File(subReader, subSavePath);
                else if (subReader.FileType == FileType.VFSFile)
                    total += ExtractVFSFile(subReader, subSavePath);
                else
                    total += ExtractBundleFile(subReader, subSavePath);
            } while (stream.Remaining > 0);
            return total;
        }

        private static int ExtractVFSFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var vfsFile = new VFSFile(reader, reader.FullPath, Studio.Game.Type);
                reader.Dispose();
                if (vfsFile.fileList != null && vfsFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, vfsFile.fileList);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading VFS file {reader.FullPath}", e);
            }
            return 0;
        }

        private static int ExtractMhyFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var mhy0File = new MhyFile(reader, (Mhy)Game);
                reader.Dispose();
                if (mhy0File.fileList != null && mhy0File.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, mhy0File.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mhy)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
        }

        private static int ExtractStreamFile(string extractPath, List<StreamFile> fileList)
        {
            int extractedCount = 0;
            if (fileList == null || fileList.Count == 0)
            {
                return 0;
            }
            foreach (var file in fileList)
            {
                var filePath = Path.Combine(extractPath, file.path);
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }
                if (!File.Exists(filePath))
                {
                    if (file.stream == null)
                    {
                        continue;
                    }
                    using (var fileStream = File.Create(filePath))
                    {
                        file.stream.CopyTo(fileStream);
                    }
                    extractedCount += 1;
                }
                file.stream.Dispose();
            }
            return extractedCount;
        }

        public static void UpdateContainers()
        {
            if (exportableAssets.Count == 0)
            {
                return;
            }

            Logger.Info("Updating Containers...");
            var isZZZ = Game.Type.IsZZZ();
            var sourceFileIds = new Dictionary<string, uint?>();

            foreach (var asset in exportableAssets)
            {
                if (isZZZ && TryUpdateZZZContainer(asset))
                {
                    continue;
                }

                TryUpdateIndexedContainer(asset, sourceFileIds);
            }

            Logger.Info("Updated !!");
        }

        private static bool TryUpdateZZZContainer(AssetItem asset)
        {
            if (!ulong.TryParse(asset.Container, out var hash) || !Paths.TryGetValue(hash, out var z3Path))
            {
                return false;
            }

            asset.Container = z3Path;
            return true;
        }

        private static void TryUpdateIndexedContainer(AssetItem asset, Dictionary<string, uint?> sourceFileIds)
        {
            if (!int.TryParse(asset.Container, out var value))
            {
                return;
            }

            var id = GetSourceFileId(asset.SourceFile.originalPath, sourceFileIds);
            if (!id.HasValue)
            {
                return;
            }

            var last = unchecked((uint)value);
            var path = ResourceIndex.GetContainer(id.Value, last);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            asset.Container = path;
            if (asset.Type == ClassIDType.MiHoYoBinData)
            {
                asset.Text = Path.GetFileNameWithoutExtension(path);
            }
        }

        private static uint? GetSourceFileId(string originalPath, Dictionary<string, uint?> sourceFileIds)
        {
            originalPath ??= string.Empty;
            if (sourceFileIds.TryGetValue(originalPath, out var id))
            {
                return id;
            }

            var name = Path.GetFileNameWithoutExtension(originalPath);
            id = uint.TryParse(name, out var parsedId) ? parsedId : null;
            sourceFileIds.Add(originalPath, id);
            return id;
        }
    }
}


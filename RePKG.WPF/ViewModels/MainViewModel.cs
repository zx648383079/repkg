using RePKG.Application.Package;
using RePKG.Application.Texture;
using RePKG.Core.Package;
using RePKG.Core.Package.Enums;
using RePKG.Core.Texture;
using RePKG.WPF.Loggers;
using RePKG.WPF.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RePKG.WPF.ViewModels
{
    public class MainViewModel: BindableBase
    {

        public MainViewModel(ILogger logger)
        {
            outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Logger = logger;
        }

        public ILogger? Logger { get; private set; }

        private CancellationTokenSource CancelToken = new();

        private string outputFolder;

        public string OutputFolder
        {
            get => outputFolder;
            set => Set(ref outputFolder, value);
        }

        private bool filterImage = true;

        public bool FilterImage
        {
            get => filterImage;
            set => Set(ref filterImage, value);
        }

        private bool filterJson = true;

        public bool FilterJson
        {
            get => filterJson;
            set => Set(ref filterJson, value);
        }


        private bool filterOther = true;

        public bool FilterOther
        {
            get => filterOther;
            set => Set(ref filterOther, value);
        }


        private ObservableCollection<FileItem> fileItems = new();

        public ObservableCollection<FileItem> FileItems
        {
            get => fileItems;
            set => Set(ref fileItems, value);
        }

        public void AddFile(string file)
        {
            foreach (var item in FileItems)
            {
                if (item.SourceFileName == file)
                {
                    return;
                }
            }
            FileItems.Add(new FileItem(file));
        }

        public void AddFile(IEnumerable<string> files)
        {
            foreach (var item in files)
            {
                if (File.Exists(item))
                {
                    AddFile(item);
                    continue;
                }
                if (Directory.Exists(item))
                {
                    AddFolderAsync(item);
                }
            }
        }

        public void AddFolderAsync(string folder)
        {
            Task.Factory.StartNew(() =>
            {
                AddFolderAsync(new DirectoryInfo(folder));
            });
        }

        public void AddFolderAsync(DirectoryInfo folder)
        {
            var items = folder.GetFiles().Where(i => i.Extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase) || 
            i.Extension.Equals(".tex", StringComparison.OrdinalIgnoreCase)).Select(i => i.FullName);
            App.Current.Dispatcher.Invoke(() =>
            {
                AddFile(items);
            });
            foreach (var item in folder.GetDirectories())
            {
                AddFolderAsync(item);
            }
        }

        public async Task ExecuteAsync()
        {
            CancelToken.Cancel();
            CancelToken = new();
            var token = CancelToken.Token;
            await Task.Factory.StartNew(() =>
            {
                Extract(FileItems.ToArray(), OutputFolder, token);
            }, token);
        }

        private readonly TexReader _texReader = TexReader.Default;
        private readonly PackageReader _packageReader = new();
        private readonly ITexJsonInfoGenerator _texJsonInfoGenerator = new TexJsonInfoGenerator();
        private readonly TexToImageConverter _texToImageConverter = new TexToImageConverter();

        private void Extract(FileItem[] fileItems, string folder, CancellationToken token)
        {
            foreach (var item in fileItems)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                item.Status = FileStatus.None;
                Logger?.Info($"Extracting {item.SourceFileName}");
                Extract(item, folder, token);
                Logger?.Info($"Extracted");
            }
            
        }

        private void Extract(FileItem item, string folder, CancellationToken token)
        {
            var ext = Path.GetExtension(item.SourceFileName);
            if (ext.Equals(".pkg", StringComparison.OrdinalIgnoreCase))
            {
                ExtractPkg(item, folder, token);
                return;
            }
            if (ext.Equals(".tex", StringComparison.OrdinalIgnoreCase))
            {
                var tex = LoadTex(File.ReadAllBytes(item.SourceFileName), item.SourceFileName);

                if (tex == null || token.IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    var filePath = Path.Combine(folder,
                        Path.GetFileNameWithoutExtension(item.SourceFileName));

                    ConvertToImageAndSave(tex, filePath, true);
                    var jsonInfo = _texJsonInfoGenerator.GenerateInfo(tex);
                    File.WriteAllText($"{filePath}.tex-json", jsonInfo);
                }
                catch (Exception e)
                {
                    Logger?.Error(e.Message);
                }
                return;
            }
            Logger?.Error($"Unrecognized file extension: {ext}");
        }

        private void ExtractPkg(FileItem item, string folder, CancellationToken token)
        {
            using var reader = new BinaryReader(File.Open(item.SourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read));
            var package = _packageReader.ReadFrom(reader);
            var entries = FilterEntries(package.Entries);
            var outputDirectory = folder;
            item.Status = FileStatus.DOING;
            foreach (var entry in entries)
            {
                if (token.IsCancellationRequested)
                {
                    item.Status = FileStatus.CANCEL;
                    return;
                }
                ExtractEntry(entry, outputDirectory, token);
            }
            item.Status = FileStatus.SUCCESS;
        }

        private IEnumerable<PackageEntry> FilterEntries(IEnumerable<PackageEntry> entries)
        {
            if (FilterJson && FilterImage && FilterOther)
            {
                return entries;
            }
            var items = new List<PackageEntry>();
            var imageExtMap = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"};
            foreach (var item in entries)
            {
                var ext = item.Extension.ToLower();
                if (ext == ".tex")
                {
                    items.Add(item);
                    continue;
                }
                if (ext == ".json")
                {
                    if (FilterJson)
                    {
                        items.Add(item);
                    }
                    continue;
                }
                if (imageExtMap.Contains(ext))
                {
                    if (FilterImage)
                    {
                        items.Add(item);
                    }
                    continue;
                }
                if (FilterOther)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private void ExtractEntry(PackageEntry entry, string outputDirectory, CancellationToken token)
{
            var filePathWithoutExtension = Path.Combine(outputDirectory, entry.DirectoryPath, entry.Name);
            if (filePathWithoutExtension == null)
            {
                return;
            }
            var filePath = filePathWithoutExtension + entry.Extension;
            Directory.CreateDirectory(Path.GetDirectoryName(filePathWithoutExtension)!);
            File.WriteAllBytes(filePath, entry.Bytes);
            // convert and save
            if (entry.Type != EntryType.Tex)
            {
                return;
            }

            var tex = LoadTex(entry.Bytes, entry.FullPath);
            if (tex == null)
            {
                return;
            }
            try
            {
                ConvertToImageAndSave(tex, filePathWithoutExtension, true);
                var jsonInfo = _texJsonInfoGenerator.GenerateInfo(tex);
                File.WriteAllText($"{filePathWithoutExtension}.tex-json", jsonInfo);
            }
            catch (Exception e)
            {
                Logger?.Error("Failed to write texture");
                Logger?.Error(e.Message);
            }
        }

        private ITex? LoadTex(byte[] bytes, string name)
        {
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
                return _texReader.ReadFrom(reader);
            }
            catch (Exception e)
            {
                Logger?.Error("Failed to read texture");
                Logger?.Error(e.Message);
            }

            return null;
        }

        private void ConvertToImageAndSave(ITex tex, string path, bool overwrite)
        {
            var format = _texToImageConverter.GetConvertedFormat(tex);
            var outputPath = $"{path}.{format.GetFileExtension()}";

            if (!overwrite && File.Exists(outputPath))
                return;

            var resultImage = _texToImageConverter.ConvertToImage(tex);

            File.WriteAllBytes(outputPath, resultImage.Bytes);
        }
    }
}

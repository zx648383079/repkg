using RePKG.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RePKG.WPF.Models
{
    public class FileItem: BindableBase
    {

        public string Name { get; set; }

        public string SourceFileName { get; set; }

        private string ouputFileName = string.Empty;

        public string OuputFileName
        {
            get => ouputFileName;
            set => Set(ref ouputFileName, value);
        }

        private FileStatus status = FileStatus.None;

        public FileStatus Status
        {
            get => status;
            set => Set(ref status, value);
        }

        public FileItem(string fileName)
        {
            SourceFileName = fileName;
            Name = Path.GetFileName(fileName);
        }

    }

    public enum FileStatus
    {
        None,
        DOING,
        SUCCESS,
        FAILURE,
        CANCEL
    }
}

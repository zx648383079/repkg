using RePKG.WPF.Loggers;
using RePKG.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RePKG.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var logger = new EventLogger();
            ViewModel = new MainViewModel(logger);
            DataContext = ViewModel;
            var isLastProgress = false;
            logger.OnLog += (s, e) =>
            {
                isLastProgress = false;
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoggerTb.AppendLine(s, true);
                });
            };
            logger.OnProgress += (s, e, msg) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (isLastProgress)
                    {
                        LoggerTb.ReplaceLine($"{s}/{e} {msg}");
                    }
                    else
                    {
                        LoggerTb.AppendLine($"{s}/{e} {msg}");
                    }
                });
                isLastProgress = true;
            };
        }

        public MainViewModel ViewModel;

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Microsoft.Win32.OpenFileDialog()
            {
                Multiselect = true,
                Filter = "pkg|*.pkg|All Files|*.*"
            };
            if (picker.ShowDialog() != true)
            {
                return;
            }
            ViewModel.AddFile(picker.FileNames);
        }

        private void AddFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new System.Windows.Forms.FolderBrowserDialog();
            if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            ViewModel.AddFolderAsync(picker.SelectedPath);
        }

        private void OutpuBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = ViewModel.OutputFolder,
                ShowNewFolderButton = true
            };
            if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            ViewModel.OutputFolder = picker.SelectedPath;
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            _ = ViewModel.ExecuteAsync();
        }

        private void ViewBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(ViewModel.OutputFolder))
            {
                Process.Start("explorer", ViewModel.OutputFolder);
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.FileItems.Clear();
            LoggerTb.Clear();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Link;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (IEnumerable<string>)e.Data.GetData(DataFormats.FileDrop);
                ViewModel.AddFile(files);
            }
        }
    }
}

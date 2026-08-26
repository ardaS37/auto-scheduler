using AutoScheduler.Core.IO;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using Microsoft.Win32;
using System;
using System.IO;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class ImportViewModel : BaseViewModel
    {
        public ProjectStore Store { get; }

        private string _lastFilePath;
        public string LastFilePath
        {
            get => _lastFilePath;
            set => Set(ref _lastFilePath, value);
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand LoadCommand { get; }

        public ImportViewModel(ProjectStore store)
        {
            Store = store;
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);
        }

        private void Save()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "AutoScheduler Project (*.autosched.json)|*.autosched.json|JSON (*.json)|*.json",
                    FileName = string.IsNullOrWhiteSpace(Store.ProjectName) ? "project.autosched.json" : $"{Store.ProjectName}.autosched.json"
                };

                if (dlg.ShowDialog() != true) return;

                ProjectSerializer.Save(Store, dlg.FileName);
                LastFilePath = dlg.FileName;
                Status = $"Kaydedildi: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                Status = "Kaydetme hatası: " + ex.Message;
            }
        }

        private void Load()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "AutoScheduler Project (*.autosched.json)|*.autosched.json|JSON (*.json)|*.json"
                };

                if (dlg.ShowDialog() != true) return;

                ProjectSerializer.Load(Store, dlg.FileName);
                LastFilePath = dlg.FileName;
                Status = $"Yüklendi: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                Status = "Yükleme hatası: " + ex.Message;
            }
        }
    }
}

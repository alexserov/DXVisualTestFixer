﻿using DevExpress.Mvvm;
using DevExpress.XtraEditors.DXErrorProvider;
using DXVisualTestFixer.Common;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DXVisualTestFixer.UI.Models {
    public class RepositoryModel : BindableBase, IDXDataErrorInfo {
        readonly Dispatcher dispatcher;
        public readonly Repository Repository;

        public string Version {
            get { return GetProperty(() => Version); }
            set { SetProperty(() => Version, value, OnVersionChanged); }
        }
        public string Path {
            get { return GetProperty(() => Path); }
            set { SetProperty(() => Path, value, OnPathChanged); }
        }
        public DownloadState DownloadState {
            get { return GetProperty(() => DownloadState); }
            set { SetProperty(() => DownloadState, value); }
        }

        void OnVersionChanged() {
            Repository.Version = Version;
        }
        void OnPathChanged() {
            Repository.Path = Path;
        }

        public RepositoryModel() : this(new Repository()) { }
        public RepositoryModel(Repository source) {
            Repository = source;
            Version = Repository.Version;
            Path = Repository.Path;
            dispatcher = Dispatcher.CurrentDispatcher;
            UpdateDownloadState();
        }

        public void GetError(ErrorInfo info) { }

        public void GetPropertyError(string propertyName, ErrorInfo info) {
            info.ErrorText = null;
            info.ErrorType = ErrorType.None;
            if(propertyName == nameof(Path)) {
                IsValid(info);
            }
        }
        public bool IsValid(ErrorInfo info = null) {
            if(info == null)
                info = new ErrorInfo();
            if(!Directory.Exists(Path)) {
                info.ErrorText = $"Directory \"{Path}\" does not exists. Example value: \"c:\\Work\\2017.1\\XPF\". Since the 18.1 version you must use specific repository - XPF\\VisualTests";
                info.ErrorType = ErrorType.Default;
                return false;
            }
            if(String.IsNullOrWhiteSpace(Version)) {
                info.ErrorText = $"Version \"{Version}\" does not valid. Enter valid value";
                return false;
            }
            string configPath = System.IO.Path.Combine(Path, "VisualTestsConfig.xml");
            if(!File.Exists(configPath)) {
                info.ErrorText = $"File \"VisualTestsConfig.xml\" does not exists in directory \"{Path}\".\nSince the 18.1 version you must use specific git repository:\n" +
                    "git@gitserver:XPF/VisualTests.git\n" +
                    "Don't create fork, use as is.\n" +
                    "Contact Zinovyev for additional info";
                info.ErrorType = ErrorType.Default;
                return false;
            }
            return true;
        }

        public static void ActualizeRepositories(ICollection<RepositoryModel> Repositories, string filePath) {
            List<string> savedVersions = Repositories.Select(r => r.Version).ToList();
            foreach(var ver in Repository.Versions.Where(v => !savedVersions.Contains(v))) {
                string verDir = String.Format("20{0}", ver);
                foreach(var directoryPath in Directory.GetDirectories(filePath)) {
                    string dirName = System.IO.Path.GetFileName(directoryPath);
                    if(dirName.Contains(String.Format("20{0}", ver)) || dirName.Contains(ver)) {
                        if(!File.Exists(directoryPath + "\\VisualTestsConfig.xml"))
                            continue;
                        Repositories.Add(new RepositoryModel(new Repository() { Version = ver, Path = directoryPath + "\\" }));
                    }
                }
            }
        }

        public void UpdateDownloadState() {
            DownloadState = GetDownloadState();
        }

        DownloadState GetDownloadState() {
            if(!Directory.Exists(Path))
                return DownloadState.ReadyToDownload;
            if(File.Exists(System.IO.Path.Combine(Path, "VisualTestsConfig.xml")))
                return DownloadState.Downloaded;
            return DownloadState.CanNotDownload;
        }
        public void Download() {
            if(DownloadState != DownloadState.ReadyToDownload)
                return;
            Task.Factory.StartNew(DownloadAsync);
        }
        async Task DownloadAsync() {
            await dispatcher.BeginInvoke(new Action(() => {
                DownloadState = DownloadState.Downloading;
            }));
            var git = ServiceLocator.Current.GetInstance<IGitWorker>();
            if(!(await git.Clone(Repository))) {
                await dispatcher.BeginInvoke(new Action(() => {
                    DownloadState = DownloadState.CanNotDownload;
                }));
                return;
            }   
            await dispatcher.BeginInvoke(new Action(() => {
                DownloadState = DownloadState.Downloaded;
            }));
            
        }
    }
    public enum DownloadState {
        ReadyToDownload, Downloading, Downloaded, CanNotDownload
    }
}

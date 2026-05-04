using System;
using System.Collections.Generic;
using System.IO;

namespace DPAPIerLib {

    public enum SecretsRefreshMode {
        StaticCache, Dynamic, RefreshOnFileChange,
    }

    public class SecretsProvider : IDisposable {
        private readonly SecretsRefreshMode _refreshMode;
        private readonly string _fileName;
        private readonly object _locker = new object();
        private Dictionary<string, string> _values;
        private FileSystemWatcher _watcher;
        private bool _disposed;


        /// <param name="fileName">Existing file with secrets that was prepared using this library or accompanying Console program DPAPIer</param>
        /// <param name="refreshMode">
        /// How changes in the secret file are refreshed within this object. With StaticCache values are read once here, in constructor, and never
        /// change during application's lifetime; With Dynamic they are individually obtained directly from the file on each request.
        /// With RefreshOnFileChange (default) they are cached now and then cache is refreshed on each change of the secret file
        /// </param>
        public SecretsProvider(string fileName, SecretsRefreshMode refreshMode = SecretsRefreshMode.RefreshOnFileChange) {
            _refreshMode = refreshMode;
            _fileName = Path.GetFullPath(fileName);
            if (_refreshMode != SecretsRefreshMode.Dynamic) {
                RefreshValues();
                if (_refreshMode == SecretsRefreshMode.RefreshOnFileChange) {
                    _watcher = new FileSystemWatcher(Path.GetDirectoryName(_fileName), Path.GetFileName(_fileName));
                    _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
                    _watcher.Changed += OnWatchedFileChanged;
                    _watcher.Created += OnWatchedFileChanged;
                    _watcher.Renamed += OnWatchedFileChanged;
                    _watcher.EnableRaisingEvents = true;
                }
            }
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs e) {
            if (!IsTargetFile(e.FullPath)) return;

            try { RefreshValues(); } catch { }
        }

        private bool IsTargetFile(string path) {
            return string.Equals(Path.GetFullPath(path), _fileName, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshValues() {
            Dictionary<string, string> values = DPAPIer.GetAllValues(_fileName);

            lock (_locker) {
                _values = values;
            }
        }


        public string GetValue(string key, string defaultValue = null) {
            if (_refreshMode == SecretsRefreshMode.Dynamic) {
                return DPAPIer.GetValue(_fileName, key, null, defaultValue);
            }

            lock (_locker) {
                return _values.TryGetValue(key, out var value) ? value : defaultValue;
            }
        }

        public void SaveValue(string key, string value) {
            lock (_locker) {
                DPAPIer.StoreValue(_fileName, key, value);
                if (_refreshMode != SecretsRefreshMode.Dynamic) _values = DPAPIer.GetAllValues(_fileName);
            }
        }

        public void Dispose() {
            if (_disposed) return;

            _watcher?.Dispose();
            _disposed = true;
        }
    }
}

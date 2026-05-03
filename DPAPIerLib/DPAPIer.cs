using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DPAPIerLib {
    public static class DPAPIer {
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;
        private const int CRYPTPROTECT_LOCAL_MACHINE = 0x4;
        private const string DefaultDelimiter = "=";
        private const string ScopePrefix = "@scope ";
        private const string DelimiterPrefix = "@delimiter ";

        /// <summary>
        /// Encrypts a file for the current Windows user. If the target file exists, it is replaced.
        /// If no target is provided, the source file is encrypted in place and inPlace must be true.
        /// </summary>
        public static void EncryptFileUser(string srcFile, string trgFile = "", bool inPlace = false, string delimiter = null) => EncryptFile(srcFile, trgFile, inPlace, DPAPILevel.User, delimiter);

        /// <summary>
        /// Encrypts a file for permitted Windows users on this machine. If the target file exists, it is replaced.
        /// If no target is provided, the source file is encrypted in place and inPlace must be true.
        /// </summary>
        public static void EncryptFileMachine(string srcFile, string trgFile = "", bool inPlace = false, string delimiter = null) => EncryptFile(srcFile, trgFile, inPlace, DPAPILevel.Machine, delimiter);

        private static void EncryptFile(string srcFile, string trgFile, bool inPlace, DPAPILevel level, string delimiter) {
            string targetFile = ResolveTargetFile(srcFile, trgFile, inPlace);
            byte[] plainBytes = File.ReadAllBytes(srcFile);
            if (delimiter != null) plainBytes = AddValueMetadata(plainBytes, level, delimiter);
            byte[] encryptedBytes = Protect(plainBytes, level);
            WriteBytesSafely(targetFile, encryptedBytes);
        }


        /// <summary>
        /// Decrypts a file. DPAPI reads the protection mode from the encrypted data.
        /// If the target file exists, it is replaced. If no target is provided,
        /// the source file is decrypted in place and inPlace must be true.
        /// </summary>
        public static void DecryptFile(string srcFile, string trgFile = "", bool inPlace = false) {
            string targetFile = ResolveTargetFile(srcFile, trgFile, inPlace);
            byte[] encryptedBytes = File.ReadAllBytes(srcFile);
            byte[] plainBytes = Unprotect(encryptedBytes);
            WriteBytesSafely(targetFile, plainBytes);
        }

        /// <summary>
        /// Decrypts a file and returns its UTF-8 text.
        /// </summary>
        public static string GetDecrypted(string fileName) {
            EnsureSourceFileExists(fileName);
            byte[] encryptedBytes = File.ReadAllBytes(fileName);
            return Encoding.UTF8.GetString(Unprotect(encryptedBytes));
        }

        /// <summary>
        /// Re-encrypts a value file from user protection to machine protection.
        /// If the file contains @scope metadata, it must be @scope u; if metadata is missing,
        /// the method trusts the requested direction and writes @scope m to the result.
        /// If the target file exists, it is replaced. If no target is provided,
        /// the source file is rewritten in place and inPlace must be true.
        /// </summary>
        public static void ReEncryptUserToMachine(string srcFile, string trgFile = "", bool inPlace = false) => ReEncrypt(srcFile, trgFile, inPlace, DPAPILevel.User, DPAPILevel.Machine);

        /// <summary>
        /// Re-encrypts a value file from machine protection to user protection.
        /// If the file contains @scope metadata, it must be @scope m; if metadata is missing,
        /// the method trusts the requested direction and writes @scope u to the result.
        /// If the target file exists, it is replaced. If no target is provided,
        /// the source file is rewritten in place and inPlace must be true.
        /// </summary>
        public static void ReEncryptMachineToUser(string srcFile, string trgFile = "", bool inPlace = false) => ReEncrypt(srcFile, trgFile, inPlace, DPAPILevel.Machine, DPAPILevel.User);

        private static void ReEncrypt(string srcFile, string trgFile, bool inPlace, DPAPILevel sourceLevel, DPAPILevel targetLevel) {
            string targetFile = ResolveTargetFile(srcFile, trgFile, inPlace);
            ValueDocument document = ParseValueDocument(GetDecrypted(srcFile), null);
            if (document.TryGetScope(out DPAPILevel storedLevel) && storedLevel != sourceLevel) {
                throw new InvalidOperationException("The command protection choice does not match the file @scope marker.");
            }

            document.SetScope(targetLevel);
            document.EnsureDelimiterMetadata();
            WriteBytesSafely(targetFile, Protect(Encoding.UTF8.GetBytes(document.Serialize()), targetLevel));
        }

        /// <summary>
        /// Returns a value from an encrypted key/value file. If delimiter is null, @delimiter metadata is used when present;
        /// otherwise equal sign is used. Non-pair lines are ignored.
        /// If the key is not found, defaultValue is returned. If duplicate keys exist,
        /// the first one wins. Keys are trimmed and matched case-insensitively.
        /// </summary>
        public static string GetValue(string fileName, string key, string delimiter = null, string defaultValue = null) {
            EnsureSourceFileExists(fileName);
            ValueDocument document = ReadValueDocument(fileName, delimiter);
            string normalizedKey = NormalizeKey(key, document.Delimiter);

            foreach (ValueLine line in document.Lines) {
                if (line.HasPair && KeysMatch(line.Key, normalizedKey)) return line.Value;
            }

            return defaultValue;
        }


        /// <summary>
        /// Tries to return a value from an encrypted key/value file.
        /// Returns false when the key is not found. If duplicate keys exist,
        /// the first one wins. Keys are trimmed and matched case-insensitively.
        /// </summary>
        public static bool TryGetValue(string fileName, string key, out string value, string delimiter = null) {
            value = GetValue(fileName, key, delimiter, null);
            return value != null;
        }


        /// <summary>
        /// Returns true if a key exists in an encrypted key/value file.
        /// Keys are trimmed and matched case-insensitively.
        /// </summary>
        public static bool ValueExists(string fileName, string key, string delimiter = null) => TryGetValue(fileName, key, out string value, delimiter);



        /// <summary>
        /// Returns all key/value pairs from an encrypted file. Non-pair lines are ignored.
        /// If duplicate keys exist, the first one wins. The returned dictionary uses
        /// case-insensitive keys.
        /// </summary>
        public static Dictionary<string, string> GetAllValues(string fileName, string delimiter = null) {
            return GetAllValues(fileName, out string resolvedDelimiter, delimiter);
        }

        /// <summary>
        /// Returns all key/value pairs from an encrypted file and the delimiter used to parse them.
        /// Non-pair lines are ignored. If duplicate keys exist, the first one wins.
        /// </summary>
        public static Dictionary<string, string> GetAllValues(string fileName, out string resolvedDelimiter, string delimiter = null) {
            EnsureSourceFileExists(fileName);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ValueDocument document = ReadValueDocument(fileName, delimiter);
            resolvedDelimiter = document.Delimiter;

            // Duplicate keys should not be produced by this class; if present, the first value wins.
            foreach (ValueLine line in document.Lines) {
                if (line.HasPair && !values.ContainsKey(line.Key)) values.Add(line.Key, line.Value);
            }

            return values;
        }

        /// <summary>
        /// Returns every recognized key/value pair from an encrypted file and the delimiter used to parse them.
        /// Non-pair lines are ignored. Keys are trimmed.
        /// </summary>
        public static List<KeyValuePair<string, string>> GetValuePairs(string fileName, out string resolvedDelimiter, string delimiter = null) {
            EnsureSourceFileExists(fileName);
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            ValueDocument document = ReadValueDocument(fileName, delimiter);
            resolvedDelimiter = document.Delimiter;

            foreach (ValueLine line in document.Lines) {
                if (line.HasPair) values.Add(new KeyValuePair<string, string>(line.Key, line.Value));
            }

            return values;
        }

        /// <summary>
        /// Stores a value in an existing encrypted key/value file, using the file's @scope marker
        /// to preserve its protection mode. The file must already exist and start with @scope u or @scope m.
        /// Prefer StoreValueUser or StoreValueMachine when creating a file.
        /// If delimiter is null, @delimiter metadata is used when present; otherwise equal sign is used.
        /// </summary>
        public static void StoreValue(string fileName, string key, string value, string delimiter = null) => StoreValueCore(fileName, key, value, delimiter, null);

        /// <summary>
        /// Stores a value in an encrypted key/value file using user protection.
        /// If the file does not exist, it is created with @scope u and @delimiter metadata.
        /// If it exists, it must start with @scope u.
        /// </summary>
        public static void StoreValueUser(string fileName, string key, string value, string delimiter = null) => StoreValueCore(fileName, key, value, delimiter, DPAPILevel.User);

        /// <summary>
        /// Stores a value in an encrypted key/value file using machine protection.
        /// If the file does not exist, it is created with @scope m and @delimiter metadata.
        /// If it exists, it must start with @scope m.
        /// </summary>
        public static void StoreValueMachine(string fileName, string key, string value, string delimiter = null) => StoreValueCore(fileName, key, value, delimiter, DPAPILevel.Machine);

        private static void StoreValueCore(string fileName, string key, string value, string delimiter, DPAPILevel? requestedLevel) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));

            ValidateValue(value);

            bool fileExists = File.Exists(fileName);
            if (!fileExists && requestedLevel == null) throw new ArgumentException("Protection choice is required when creating a value file.", nameof(requestedLevel));

            ValueDocument document = fileExists
                ? ReadValueDocument(fileName, delimiter)
                : ValueDocument.Create(requestedLevel.Value, ResolveDelimiter(null, delimiter));

            string resolvedDelimiter = document.Delimiter;
            string normalizedKey = NormalizeKey(key, resolvedDelimiter);
            DPAPILevel level = ResolveWriteLevel(document, requestedLevel);
            document.EnsureDelimiterMetadata();

            bool updated = false;
            foreach (ValueLine line in document.Lines) {
                if (line.HasPair && KeysMatch(line.Key, normalizedKey)) {
                    if (updated) line.Removed = true;
                    else {
                        line.ReplacementText = normalizedKey + resolvedDelimiter + (value ?? string.Empty);
                        updated = true;
                    }
                }
            }

            if (!updated) document.AppendPair(normalizedKey, value ?? string.Empty);

            string plainText = document.Serialize();
            byte[] encryptedBytes = Protect(Encoding.UTF8.GetBytes(plainText), level);
            WriteBytesSafely(fileName, encryptedBytes);
        }

        public static bool RemoveValue(string fileName, string key, string delimiter = null) => RemoveValueCore(fileName, key, delimiter, null);
        public static bool RemoveValueUser(string fileName, string key, string delimiter = null) => RemoveValueCore(fileName, key, delimiter, DPAPILevel.User);
        public static bool RemoveValueMachine(string fileName, string key, string delimiter = null) => RemoveValueCore(fileName, key, delimiter, DPAPILevel.Machine);

        private static bool RemoveValueCore(string fileName, string key, string delimiter, DPAPILevel? requestedLevel) {
            EnsureSourceFileExists(fileName);

            ValueDocument document = ReadValueDocument(fileName, delimiter);
            string normalizedKey = NormalizeKey(key, document.Delimiter);
            DPAPILevel level = ResolveWriteLevel(document, requestedLevel);
            bool removed = false;

            foreach (ValueLine line in document.Lines) {
                if (line.HasPair && KeysMatch(line.Key, normalizedKey)) {
                    line.Removed = true;
                    removed = true;
                }
            }

            if (removed) {
                document.EnsureDelimiterMetadata();
                WriteBytesSafely(fileName, Protect(Encoding.UTF8.GetBytes(document.Serialize()), level));
            }

            return removed;
        }

        private static DPAPILevel ResolveWriteLevel(ValueDocument document, DPAPILevel? requestedLevel) {
            DPAPILevel storedLevel = document.RequireScope();
            if (requestedLevel != null && requestedLevel.Value != storedLevel) {
                throw new InvalidOperationException("The command protection choice does not match the file @scope marker.");
            }

            return storedLevel;
        }

        public static byte[] EncryptBytesUser(byte[] data) => EncryptBytes(data, DPAPILevel.User);
        public static byte[] EncryptBytesMachine(byte[] data) => EncryptBytes(data, DPAPILevel.Machine);

        private static byte[] EncryptBytes(byte[] data, DPAPILevel level) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return Protect(data, level);
        }

        public static byte[] DecryptBytes(byte[] encryptedData) {
            if (encryptedData == null) throw new ArgumentNullException(nameof(encryptedData));
            return Unprotect(encryptedData);
        }

        public static byte[] EncryptStringUser(string value) => EncryptString(value, DPAPILevel.User);
        public static byte[] EncryptStringMachine(string value) => EncryptString(value, DPAPILevel.Machine);

        private static byte[] EncryptString(string value, DPAPILevel level) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return Protect(Encoding.UTF8.GetBytes(value), level);
        }

        public static string DecryptString(byte[] encryptedData) {
            if (encryptedData == null) throw new ArgumentNullException(nameof(encryptedData));
            return Encoding.UTF8.GetString(Unprotect(encryptedData));
        }

        public static bool CanDecrypt(string fileName) {
            // This only proves DPAPI can unprotect the blob; it does not validate text format or original scope.
            try {
                EnsureSourceFileExists(fileName);
                Unprotect(File.ReadAllBytes(fileName));
                return true;
            } catch {
                return false;
            }
        }

        private static bool KeysMatch(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        
        private static string ResolveTargetFile(string srcFile, string trgFile, bool inPlace) {
            EnsureSourceFileExists(srcFile);

            bool hasTarget = !string.IsNullOrWhiteSpace(trgFile);
            string targetFile = hasTarget ? trgFile : srcFile;
            bool sameFile = string.Equals(
                Path.GetFullPath(srcFile),
                Path.GetFullPath(targetFile),
                StringComparison.OrdinalIgnoreCase);

            if (sameFile && !inPlace) throw new ArgumentException("In-place operation requires inPlace to be true.", nameof(inPlace));
            if (inPlace && hasTarget && !sameFile) throw new ArgumentException("inPlace can only be true when the target file is the source file.", nameof(inPlace));

            return targetFile;
        }

        private static void EnsureSourceFileExists(string fileName) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (!File.Exists(fileName)) throw new FileNotFoundException("Source file does not exist.", fileName);
        }

        private static string NormalizeKey(string key, string delimiter) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            string normalizedKey = key.Trim();
            if (normalizedKey.Length == 0) throw new ArgumentException("Key cannot be null, empty, or whitespace.", nameof(key));
            if (normalizedKey.IndexOf(delimiter, StringComparison.Ordinal) >= 0) throw new ArgumentException("Key cannot contain the delimiter.", nameof(key));
            if (normalizedKey.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new ArgumentException("Key cannot contain line breaks.", nameof(key));
            return normalizedKey;
        }

        private static void ValidateValue(string value) {
            if (value != null && value.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new ArgumentException("Value cannot contain line breaks.", nameof(value));
        }

        private static void ValidateDelimiter(string delimiter) {
            if (string.IsNullOrEmpty(delimiter)) throw new ArgumentException("Delimiter cannot be null or empty.", nameof(delimiter));
            if (delimiter.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new ArgumentException("Delimiter cannot contain line breaks.", nameof(delimiter));
        }

        private static ValueDocument ReadValueDocument(string fileName, string delimiter) => ParseValueDocument(GetDecrypted(fileName), delimiter);
        

        private static ValueDocument ParseValueDocument(string text, string requestedDelimiter) {
            ValueDocument document = new ValueDocument();
            int index = 0;

            while (index < text.Length) {
                int lineStart = index;

                while (index < text.Length && text[index] != '\r' && text[index] != '\n') index++;

                string lineText = text.Substring(lineStart, index - lineStart);
                string ending = string.Empty;

                if (index < text.Length) {
                    if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n') {
                        ending = "\r\n";
                        index += 2;
                    } else {
                        ending = text[index].ToString();
                        index++;
                    }
                }

                document.Lines.Add(ValueLine.ParseMetadata(lineText, ending));
            }

            document.ResolveDelimiter(requestedDelimiter);
            document.ParsePairs();

            return document;
        }

        private sealed class ValueDocument {
            public List<ValueLine> Lines { get; } = new List<ValueLine>();
            public string Delimiter { get; private set; }

            public static ValueDocument Create(DPAPILevel level, string delimiter) {
                ValueDocument document = new ValueDocument();
                document.Delimiter = delimiter;
                document.Lines.Add(ValueLine.NewScope(level));
                document.Lines.Add(ValueLine.NewDelimiter(delimiter));
                return document;
            }

            public DPAPILevel RequireScope() {
                if (Lines.Count == 0) throw MissingScopeException();

                ValueLine firstLine = Lines[0];
                if (!firstLine.HasScope) throw MissingScopeException();

                return firstLine.ScopeLevel;
            }

            public bool TryGetScope(out DPAPILevel level) {
                if (Lines.Count > 0 && Lines[0].HasScope) {
                    level = Lines[0].ScopeLevel;
                    return true;
                }

                level = DPAPILevel.User;
                return false;
            }

            public void SetScope(DPAPILevel level) {
                if (!TryGetScope(out DPAPILevel currentLevel)) {
                    if (Lines.Count > 0 && Lines[0].Ending.Length == 0) Lines[0].Ending = Environment.NewLine;

                    Lines.Insert(0, ValueLine.NewScope(level));
                    return;
                }

                Lines[0].ReplacementText = FormatScopeLine(level);
                Lines[0].ScopeLevel = level;
            }

            public void ResolveDelimiter(string requestedDelimiter) {
                ValidateDelimiterMetadataPosition();

                string storedDelimiter = null;
                if (Lines.Count > 1 && Lines[1].HasDelimiter) storedDelimiter = Lines[1].Delimiter;

                Delimiter = DPAPIer.ResolveDelimiter(storedDelimiter, requestedDelimiter);
            }

            public void ParsePairs() {
                foreach (ValueLine line in Lines)  line.ParsePair(Delimiter);
            }

            public void EnsureDelimiterMetadata() {
                if (Delimiter == null) Delimiter = DefaultDelimiter;

                if (Lines.Count > 1 && Lines[1].HasDelimiter) {
                    Lines[1].ReplacementText = FormatDelimiterLine(Delimiter);
                    Lines[1].Delimiter = Delimiter;
                    return;
                }

                if (Lines.Count > 0 && Lines[0].Ending.Length == 0) Lines[0].Ending = Environment.NewLine;
                Lines.Insert(Math.Min(1, Lines.Count), ValueLine.NewDelimiter(Delimiter));
            }

            public void AppendPair(string key, string value) {
                if (Lines.Count > 0 && Lines[Lines.Count - 1].Ending.Length == 0) {
                    Lines[Lines.Count - 1].Ending = Environment.NewLine;
                }

                Lines.Add(ValueLine.NewPair(key + Delimiter + value));
            }

            public string Serialize() {
                StringBuilder builder = new StringBuilder();

                foreach (ValueLine line in Lines) {
                    if (line.Removed) continue;

                    builder.Append(line.ReplacementText ?? line.Text);
                    builder.Append(line.Ending);
                }

                return builder.ToString();
            }

            private static Exception MissingScopeException() => new FormatException("Encrypted value file does not start with @scope u or @scope m.");
            

            private void ValidateDelimiterMetadataPosition() {
                for (int i = 0; i < Lines.Count; i++) {
                    ValueLine line = Lines[i];
                    if (line.HasMalformedDelimiter) throw new FormatException("Delimiter metadata must be written as @delimiter followed by the delimiter.");
                    if (line.HasDelimiter && i != 1) throw new FormatException("Delimiter metadata must be the second decrypted text line.");
                }
            }
        }

        private sealed class ValueLine {
            public string Text { get; private set; }
            public string Ending { get; set; }
            public bool HasPair { get; private set; }
            public string Key { get; private set; }
            public string Value { get; private set; }
            public bool HasScope { get; private set; }
            public DPAPILevel ScopeLevel { get; set; }
            public bool HasDelimiter { get; private set; }
            public bool HasMalformedDelimiter { get; private set; }
            public string Delimiter { get; set; }
            public bool Removed { get; set; }
            public string ReplacementText { get; set; }

            private ValueLine(string text, string ending) {
                Text = text;
                Ending = ending;
            }

            public static ValueLine NewPair(string text) => new ValueLine(text, string.Empty);
            

            public static ValueLine NewScope(DPAPILevel level) {
                ValueLine line = new ValueLine(FormatScopeLine(level), Environment.NewLine);
                line.HasScope = true;
                line.ScopeLevel = level;
                return line;
            }

            public static ValueLine NewDelimiter(string delimiter) {
                ValueLine line = new ValueLine(FormatDelimiterLine(delimiter), Environment.NewLine);
                line.HasDelimiter = true;
                line.Delimiter = delimiter;
                return line;
            }

            public static ValueLine ParseMetadata(string text, string ending) {
                ValueLine line = new ValueLine(text, ending);
                if (TryParseScope(text, out DPAPILevel scopeLevel)) {
                    line.HasScope = true;
                    line.ScopeLevel = scopeLevel;
                    return line;
                }

                if (TryParseDelimiter(text, out string parsedDelimiter)) {
                    line.HasDelimiter = true;
                    line.Delimiter = parsedDelimiter;
                    return line;
                }

                if (string.Equals(text.Trim(), "@delimiter", StringComparison.OrdinalIgnoreCase)) {
                    line.HasMalformedDelimiter = true;
                }

                return line;
            }

            public void ParsePair(string delimiter) {
                if (HasScope || HasDelimiter || HasMalformedDelimiter) return;

                int delimiterIndex = Text.IndexOf(delimiter, StringComparison.Ordinal);

                if (delimiterIndex < 0) return;

                string key = Text.Substring(0, delimiterIndex).Trim();
                if (key.Length == 0) return;

                HasPair = true;
                Key = key;
                Value = Text.Substring(delimiterIndex + delimiter.Length);
            }

            private static bool TryParseScope(string text, out DPAPILevel level) {
                string trimmed = text.Trim();
                if (string.Equals(trimmed, ScopePrefix + "u", StringComparison.OrdinalIgnoreCase)) {
                    level = DPAPILevel.User;
                    return true;
                }

                if (string.Equals(trimmed, ScopePrefix + "m", StringComparison.OrdinalIgnoreCase)) {
                    level = DPAPILevel.Machine;
                    return true;
                }

                level = DPAPILevel.User;
                return false;
            }

            private static bool TryParseDelimiter(string text, out string delimiter) {
                if (text.StartsWith(DelimiterPrefix, StringComparison.OrdinalIgnoreCase)) {
                    delimiter = text.Substring(DelimiterPrefix.Length);
                    return true;
                }

                delimiter = null;
                return false;
            }
        }

        private static string FormatScopeLine(DPAPILevel level) => ScopePrefix + (level == DPAPILevel.Machine ? "m" : "u");
        
        private static string FormatDelimiterLine(string delimiter)=>  DelimiterPrefix + delimiter;

        private static byte[] AddValueMetadata(byte[] plainBytes, DPAPILevel level, string delimiter) {
            ValidateDelimiter(delimiter);
            string header = FormatScopeLine(level) + Environment.NewLine + FormatDelimiterLine(delimiter) + Environment.NewLine;
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] result = new byte[headerBytes.Length + plainBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
            Buffer.BlockCopy(plainBytes, 0, result, headerBytes.Length, plainBytes.Length);
            return result;
        }
        
        private static string ResolveDelimiter(string storedDelimiter, string requestedDelimiter) {
            if (storedDelimiter != null) ValidateDelimiter(storedDelimiter);
            if (requestedDelimiter != null) ValidateDelimiter(requestedDelimiter);

            if (storedDelimiter != null && requestedDelimiter != null && !string.Equals(storedDelimiter, requestedDelimiter, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The provided delimiter does not match the file @delimiter marker.");
            }

            return requestedDelimiter ?? storedDelimiter ?? DefaultDelimiter;
        }

        private static void WriteBytesSafely(string fileName, byte[] bytes) {
            string directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tempFile = Path.Combine(directory ?? string.Empty, Path.GetRandomFileName());
            File.WriteAllBytes(tempFile, bytes);

            try {
                if (File.Exists(fileName)) File.Delete(fileName);

                File.Move(tempFile, fileName);
            } catch {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                throw;
            }
        }

        private static byte[] Protect(byte[] plainBytes, DPAPILevel level)  => Transform(plainBytes, true, level);
        

        private static byte[] Unprotect(byte[] encryptedBytes) => Transform(encryptedBytes, false, DPAPILevel.User);
        

        private static byte[] Transform(byte[] input, bool protect, DPAPILevel level) {
            EnsureWindows();
            DATA_BLOB inputBlob = new DATA_BLOB();
            DATA_BLOB outputBlob = new DATA_BLOB();
            IntPtr inputPointer = IntPtr.Zero;
            // LOCAL_MACHINE is a protect-time choice; unprotect reads the scope from the DPAPI blob.
            int flags = protect && level == DPAPILevel.Machine
                ? CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_LOCAL_MACHINE
                : CRYPTPROTECT_UI_FORBIDDEN;

            try {
                inputPointer = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputPointer, input.Length);
                inputBlob.cbData = input.Length;
                inputBlob.pbData = inputPointer;

                bool succeeded = protect
                    ? CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, flags, ref outputBlob)
                    : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, flags, ref outputBlob);

                if (!succeeded) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                byte[] output = new byte[outputBlob.cbData];
                Marshal.Copy(outputBlob.pbData, output, 0, outputBlob.cbData);
                return output;
            } catch { 
                if (protect) {
                    throw new Exception("Unexpected error while encrypting data");
                } else {
                    throw new Exception("Unexpected error while decrypting data. Possible reason is that source file was not properly encrypted on this computer or under current user.");
                }
            } finally {
                if (inputPointer != IntPtr.Zero) Marshal.FreeHGlobal(inputPointer);
                if (outputBlob.pbData != IntPtr.Zero) LocalFree(outputBlob.pbData);
            }
        }
        private static void EnsureWindows() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB {
            public int cbData;
            public IntPtr pbData;
        }

        private enum DPAPILevel {
            User,
            Machine
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DATA_BLOB pDataIn,
            string szDataDescr,
            IntPtr pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn,
            IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DATA_BLOB pDataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}

using System.Runtime.InteropServices;
using System.Text;
using Dpapi = DPAPIerLib.DPAPIer;
using Xunit;

namespace DPAPIerLib.Tests;

public sealed class DPAPIerTests {
    [Fact]
    public void EncryptStringUser_round_trips_plain_text() {
        RunOnWindows(() => {
            string encrypted = Dpapi.EncryptStringUser("plain text");

            Assert.NotEqual("plain text", encrypted);
            Assert.NotEmpty(Convert.FromBase64String(encrypted));
            Assert.Equal("plain text", Dpapi.DecryptString(encrypted));
        });
    }

    [Fact]
    public void EncryptStringToBytesUser_round_trips_plain_text() {
        RunOnWindows(() => {
            byte[] encrypted = Dpapi.EncryptStringToBytesUser("plain text");

            Assert.NotEqual("plain text", Encoding.UTF8.GetString(encrypted));
            Assert.Equal("plain text", Dpapi.DecryptString(encrypted));
        });
    }

    [Fact]
    public void EncryptBytesMachine_round_trips_bytes() {
        RunOnWindows(() => {
            byte[] data = [0, 1, 2, 3, 250, 251, 252, 255];

            byte[] encrypted = Dpapi.EncryptBytesMachine(data);

            Assert.NotEqual(data, encrypted);
            Assert.Equal(data, Dpapi.DecryptBytes(encrypted));
        });
    }

    [Fact]
    public void EncryptFileUser_and_DecryptFile_round_trip_to_target_files() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string plainFile = temp.PathFor("plain.txt");
            string encryptedFile = temp.PathFor("encrypted.dpapi");
            string decryptedFile = temp.PathFor("decrypted.txt");
            File.WriteAllText(plainFile, "file secret");

            Dpapi.EncryptFileUser(plainFile, encryptedFile);
            Dpapi.DecryptFile(encryptedFile, decryptedFile);

            Assert.True(Dpapi.CanDecrypt(encryptedFile));
            Assert.Equal("file secret", File.ReadAllText(decryptedFile));
        });
    }

    [Fact]
    public void StoreValueUser_creates_and_updates_key_value_file() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");

            Dpapi.StoreValueUser(fileName, "  ApiKey  ", "first", delimiter: ":");
            Dpapi.StoreValue(fileName, "apikey", "second");
            Dpapi.StoreValue(fileName, "ConnectionString", "Server=.;Database=DPAPIer");

            Dictionary<string, string> values = Dpapi.GetAllValues(fileName, out string delimiter);

            Assert.Equal(":", delimiter);
            Assert.Equal("second", Dpapi.GetValue(fileName, "APIKEY"));
            Assert.Equal("fallback", Dpapi.GetValue(fileName, "missing", defaultValue: "fallback"));
            Assert.True(Dpapi.ValueExists(fileName, "connectionstring"));
            Assert.Equal(2, values.Count);
            Assert.Equal("Server=.;Database=DPAPIer", values["ConnectionString"]);
        });
    }

    [Fact]
    public void RemoveValueUser_removes_existing_value_and_reports_missing_values() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");
            Dpapi.StoreValueUser(fileName, "first", "1");
            Dpapi.StoreValueUser(fileName, "second", "2");

            bool removed = Dpapi.RemoveValueUser(fileName, " FIRST ");
            bool removedAgain = Dpapi.RemoveValueUser(fileName, "first");

            Assert.True(removed);
            Assert.False(removedAgain);
            Assert.False(Dpapi.ValueExists(fileName, "first"));
            Assert.Equal("2", Dpapi.GetValue(fileName, "second"));
        });
    }

    [Fact]
    public void ReEncryptUserToMachine_preserves_values_and_writes_machine_scope() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string userFile = temp.PathFor("user.dpapi");
            string machineFile = temp.PathFor("machine.dpapi");
            Dpapi.StoreValueUser(userFile, "name", "value");

            Dpapi.ReEncryptUserToMachine(userFile, machineFile);

            Assert.Equal("value", Dpapi.GetValue(machineFile, "name"));
            Assert.StartsWith("@scope m", Dpapi.GetDecrypted(machineFile), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void EncryptBytesUser_rejects_null_data() {
        Assert.Throws<ArgumentNullException>(() => Dpapi.EncryptBytesUser(null!));
    }

    [Fact]
    public void SecretsProvider_static_cache_returns_values_and_defaults() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");
            Dpapi.StoreValueUser(fileName, "ApiKey", "secret");

            using SecretsProvider provider = new(fileName, SecretsRefreshMode.StaticCache);

            Assert.Equal("secret", provider.GetValue("apikey"));
            Assert.Equal("fallback", provider.GetValue("missing", "fallback"));
        });
    }

    [Fact]
    public void SecretsProvider_dynamic_passes_default_value_to_dpapi() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");
            Dpapi.StoreValueUser(fileName, "ApiKey", "secret", delimiter: ":");

            using SecretsProvider provider = new(fileName, SecretsRefreshMode.Dynamic);

            Assert.Equal("fallback", provider.GetValue("missing", "fallback"));
        });
    }

    [Fact]
    public void SecretsProvider_save_value_refreshes_cache_with_normalized_key() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");
            Dpapi.StoreValueUser(fileName, "ApiKey", "old");

            using SecretsProvider provider = new(fileName, SecretsRefreshMode.StaticCache);
            provider.SaveValue("  apikey  ", "new");

            Assert.Equal("new", provider.GetValue("ApiKey"));
        });
    }

    [Fact]
    public void SecretsProvider_refreshes_cache_after_external_atomic_update() {
        RunOnWindows(() => {
            using TempDirectory temp = new();
            string fileName = temp.PathFor("values.dpapi");
            Dpapi.StoreValueUser(fileName, "ApiKey", "old");

            using SecretsProvider provider = new(fileName);
            Dpapi.StoreValue(fileName, "ApiKey", "new");

            bool refreshed = SpinWait.SpinUntil(
                () => provider.GetValue("ApiKey") == "new",
                TimeSpan.FromSeconds(5));

            Assert.True(refreshed);
        });
    }

    private static void RunOnWindows(Action test) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        test();
    }

    private sealed class TempDirectory : IDisposable {
        private readonly string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DPAPIerLib.Tests",
            Guid.NewGuid().ToString("N"));

        public string PathFor(string fileName) {
            Directory.CreateDirectory(path);
            return System.IO.Path.Combine(path, fileName);
        }

        public void Dispose() {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }
}

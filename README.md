# DPAPIer

DPAPIer is a Windows-focused .NET solution for protecting local secrets with the Windows Data Protection API (DPAPI). It includes a command-line utility for encrypting files and values, plus a reusable library for applications that need DPAPI-based file, byte-array, string, or key/value storage.

DPAPI lets Windows protect data using user or machine credentials, so your application does not need to store a separate encryption key.

## Projects

| Project | Purpose |
| --- | --- |
| `DPAPIer` | Console application for encrypting, decrypting, storing, reading, listing, and removing protected values. |
| `DPAPIerLib` | `netstandard2.0` library exposing DPAPI helpers through `DPAPIerLib.DPAPIer`. |
| `DPAPIerLib.Tests` | xUnit test project for the library behavior. |

## Key Features

- Encrypt and decrypt whole files with current-user or local-machine DPAPI protection.
- Encrypt and decrypt strings directly from the command line.
- Store named key/value secrets inside encrypted files.
- Read, list, update, and remove encrypted values without exposing the whole file.
- Re-encrypt value files between user and machine protection.

## Important Security Notes

DPAPI-protected data is local to Windows.

- User-protected data can be decrypted only by the same Windows user account.
- Machine-protected data can be decrypted by permitted users on the same machine.
- DPAPI-encrypted files are not portable to another computer.
- Data may not be recoverable after Windows is reinstalled, even on the same computer.
- DPAPI does not protect secrets from someone who can already sign in as the protected Windows user.

Use `user` protection unless another account on the same machine must decrypt the data.

## Requirements

- Windows for encryption and decryption operations.
- .NET SDK capable of building the solution.
- The console targets `net10.0`.
- The library targets `netstandard2.0` and thus can be used with most of .NET implementations.

## Build

From the repository root:

```powershell
dotnet build
```

Run tests:

```powershell
dotnet test
```

After a debug build, the console executable is produced under:

```text
DPAPIer\bin\Debug\net10.0\DPAPIer.exe
```

## Console Quick Start

Encrypt a file for the current user:

```powershell
DPAPIer e user -f "plain.txt" -t "secret.dpapi"
```

Decrypt a file:

```powershell
DPAPIer d -f "secret.dpapi" -t "plain.txt"
```

Encrypt a string:

```powershell
DPAPIer es user "secret text"
```

Decrypt an encrypted string:

```powershell
DPAPIer ds "<encrypted string>"
```

Store a named value:

```powershell
DPAPIer s user -f "values.dpapi" -k "Password" -v "correct horse battery staple"
```

Read a named value:

```powershell
DPAPIer g -f "values.dpapi" -k "Password"
```

List keys and values:

```powershell
DPAPIer keys "values.dpapi"
DPAPIer values "values.dpapi"
```

Remove a named value:

```powershell
DPAPIer r -f "values.dpapi" -k "Password"
```

See the full command documentation in [`DPAPIer/README.md`](DPAPIer/README.md) and the user-focused guide in [`DPAPIer/USER_GUIDE.md`](DPAPIer/USER_GUIDE.md).

## Library Quick Start

Reference the library project:

```xml
<ProjectReference Include="..\DPAPIerLib\DPAPIerLib.csproj" />
```

Import the namespace:

```csharp
using DPAPIerLib;
```

Encrypt and decrypt files:

```csharp
DPAPIer.EncryptFileUser("settings.json", "settings.dpapi");
DPAPIer.EncryptFileMachine("settings.json", "machine-settings.dpapi");
DPAPIer.DecryptFile("settings.dpapi", "settings.json");
```

Encrypt and decrypt strings:

```csharp
string encrypted = DPAPIer.EncryptStringUser("secret text");
string plain = DPAPIer.DecryptString(encrypted);
```

Store and read encrypted values:

```csharp
DPAPIer.StoreValueUser("values.dpapi", "Password", "correct horse battery staple");

if (DPAPIer.TryGetValue("values.dpapi", "Password", out string value)) {
    Console.WriteLine(value);
}
```

See the full library documentation in [`DPAPIerLib/README.md`](DPAPIerLib/README.md).

## Value File Format

DPAPIer value files are encrypted DPAPI blobs. After decryption, the text format is intentionally simple:

```text
@scope u
@delimiter =
Password=correct horse battery staple
ApiKey=local-development-key
```

The `@scope` marker records whether the file is protected for `user` or `machine`. The `@delimiter` marker records how keys and values are separated. Commands that rewrite value files preserve unrecognized text and empty lines where possible.

## Command Summary

```text
DPAPIer e|encrypt    u|user|m|machine [o|override] -f <file> [-t <target>] [-d <delimiter>]
DPAPIer d|decrypt    [o|override] -f <file> [-t <target>]
DPAPIer es|-es       u|user|m|machine [cc|-cc] <string>
DPAPIer ds|-ds       <encrypted-string>
DPAPIer re|reencrypt u|user|m|machine [o|override] -f <file> [-t <target>]
DPAPIer p|put        [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
DPAPIer s|set|store  [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
DPAPIer g|get        -f <file> -k <key> [-d <delimiter>]
DPAPIer keys|-keys   -f <file> [-d <delimiter>]
DPAPIer values|vals|-values|-vals -f <file> [-d <delimiter>]
DPAPIer r|remove     [u|user|m|machine] -f <file> -k <key> [-d <delimiter>]
```

## Repository Documentation

- [`DPAPIer/README.md`](DPAPIer/README.md): complete console reference.
- [`DPAPIer/USER_GUIDE.md`](DPAPIer/USER_GUIDE.md): task-oriented user guide.
- [`DPAPIerLib/README.md`](DPAPIerLib/README.md): library API guide and usage notes.

## License
Free to use without restrictions. No warranty or liability - use at your own risk. 


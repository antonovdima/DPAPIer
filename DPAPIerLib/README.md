# DPAPIerLib

DPAPIerLib is a small .NET library for encrypting and decrypting data with the Windows Data Protection API (DPAPI). It can work with whole files, raw byte arrays, strings, and simple encrypted key/value files.

DPAPI protects local data using Windows-managed secrets, so an application can encrypt data without storing its own encryption key. DPAPIerLib can protect data for the current Windows user or for permitted users on the same machine.

***Attention!** DPAPI-encrypted files are not transferable to other computers and may not be recoverable after Windows is reinstalled, even on the same computer.*

***Attention!** DPAPI does not protect data from someone who can log in as the protected Windows user. Machine-protected data may also be readable by other permitted users on the same computer.*

The public API is exposed through the static `DPAPIerLib.DPAPIer` class.

## Platform

DPAPI is a Windows feature. Operations that encrypt or decrypt data require Windows at runtime.

The project targets `netstandard2.0`, so it can be referenced by many .NET applications, but the runtime operating system still must be Windows for DPAPI operations to succeed.

## User Or Machine Protection

When protecting data, choose who can decrypt it later:

- User protection: only the same Windows user account.
- Machine protection: permitted Windows user accounts on the same machine.

This choice is made when data is encrypted or rewritten. It is stored inside the DPAPI blob. When data is decrypted, Windows DPAPI reads the needed information from the blob, so callers do not need to provide user or machine protection for read operations.

## Install Or Reference

Reference the project from another .NET project:

```xml
<ProjectReference Include="..\DPAPIerLib\DPAPIerLib.csproj" />
```

Or reference a built DLL directly. A convenient development layout is to keep third-party or local binary dependencies in a folder under the consuming project, for example:

```text
MyNextProject\
  MyNextProject.csproj
  libs\
    DPAPIerLib.dll
```

Then add this to `MyNextProject.csproj`:

```xml
<ItemGroup>
  <Reference Include="DPAPIerLib">
    <HintPath>libs\DPAPIerLib.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

`Private` tells MSBuild to copy `DPAPIerLib.dll` to the build output, so it is available beside the consuming application during development, build, and publish. Avoid referencing the DLL directly from this repository's `bin\Debug` or `bin\Release` folder; that makes the consuming project depend on a local build path.

If you want a hybrid setup, you can prefer a local DLL when it exists and fall back to a shared machine location otherwise:

```xml
<ItemGroup Condition="Exists('libs\DPAPIerLib.dll')">
  <Reference Include="DPAPIerLib">
    <HintPath>libs\DPAPIerLib.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>

<ItemGroup Condition="!Exists('libs\DPAPIerLib.dll')">
  <Reference Include="DPAPIerLib">
    <HintPath>C:\Tools\DPAPIerLib\DPAPIerLib.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

This uses `libs\DPAPIerLib.dll` when present; otherwise it uses the shared copy. Keeping `Private` as `true` is still recommended because the selected DLL is copied beside the consuming application. Setting `Private` to `false` may compile, but the built program must still be able to find the DLL at runtime.

Because this library targets `netstandard2.0`, the same DLL can be referenced from .NET Framework 4.8 and from modern .NET projects such as .NET 6, 8, or 10. .NET Framework versions earlier than 4.8 may support .NET Standard 2.0 in some cases, but 4.8 or newer is the safer practical baseline. The DPAPI operations still require Windows at runtime.

Then import the namespace:

```csharp
using DPAPIerLib;
```

## File Encryption

Use file methods when the entire file should be protected as a single DPAPI blob.

```csharp
DPAPIer.EncryptFileUser("settings.json", "settings.dpapi");
DPAPIer.EncryptFileMachine("settings.json", "machine-settings.dpapi");
```

If a delimiter is supplied, file encryption prepends value-file metadata before encrypting:

```csharp
DPAPIer.EncryptFileUser("values.txt", "values.dpapi", delimiter: "=");
```

The decrypted text then starts with `@scope` and `@delimiter`, so key/value methods can read it as a value file.

Decrypting does not require choosing user or machine:

```csharp
DPAPIer.DecryptFile("settings.dpapi", "settings.json");
```

If the target file is omitted or empty, the source file is also the target file. In that case `inPlace` must be `true`:

```csharp
DPAPIer.EncryptFileUser("settings.json", inPlace: true);
DPAPIer.DecryptFile("settings.dpapi", inPlace: true);
```

If a target file is supplied, `inPlace` must be `false`.

The console utility uses its `override` option to decide when it is allowed to call these in-place library methods.

## Re-Encrypting Files

Use re-encryption when an existing DPAPIer value file should be rewritten with the opposite protection choice. If the file has an `@scope u` or `@scope m` marker, it must match the method name. If the marker is missing, the method trusts the requested direction and writes the opposite marker into the result.

```csharp
DPAPIer.ReEncryptUserToMachine("user-secret.dpapi", "machine-secret.dpapi");
DPAPIer.ReEncryptMachineToUser("machine-secret.dpapi", "user-secret.dpapi");
```

The method name describes the expected current protection and the new opposite protection. If the file marker does not match the method name, the method throws.

## Byte Arrays And Strings

Use byte methods when the application wants to manage storage itself.

```csharp
byte[] encrypted = DPAPIer.EncryptBytesUser(plainBytes);
byte[] plain = DPAPIer.DecryptBytes(encrypted);
```

String methods encode and decode text as UTF-8:

```csharp
byte[] encrypted = DPAPIer.EncryptStringUser("secret text");
string plain = DPAPIer.DecryptString(encrypted);
```

Machine protection is available for encrypting:

```csharp
DPAPIer.EncryptBytesMachine(...)
DPAPIer.EncryptStringMachine(...)
```

## Encrypted Key/Value Files

The library can store simple key/value pairs inside an encrypted file. The plaintext format before encryption is one pair per line:

```text
@scope u
@delimiter =
Key=Value
AnotherKey=AnotherValue
```

The first line records the protection choice. The second line records the delimiter. If a delimiter argument is omitted, an existing file uses its `@delimiter` marker. If the marker is missing, `=` is assumed. If a delimiter is supplied and does not match the marker, the method throws.

Store values with a protection choice when creating a new file:

```csharp
DPAPIer.StoreValueUser("values.dpapi", "Password", "correct horse battery staple");
DPAPIer.StoreValueMachine("values.dpapi", "Password", "correct horse battery staple");
```

For existing value files, `StoreValue` and `RemoveValue` require the first decrypted line to be `@scope u` or `@scope m`. The file is rewritten using that marker:

```csharp
DPAPIer.StoreValue("values.dpapi", "Password", "updated secret");
```

If `StoreValueUser` or `StoreValueMachine` is used with an existing value file, the method name must match the file marker.

Read values without choosing user or machine:

```csharp
string password = DPAPIer.GetValue("values.dpapi", "Password", defaultValue: "");
```

Check, try-read, remove, and list values:

```csharp
bool exists = DPAPIer.ValueExists("values.dpapi", "Password");

if (DPAPIer.TryGetValue("values.dpapi", "Password", out string value)) {
    Console.WriteLine(value);
}

bool removed = DPAPIer.RemoveValue("values.dpapi", "Password");
Dictionary<string, string> allValues = DPAPIer.GetAllValues("values.dpapi");
List<KeyValuePair<string, string>> allPairs = DPAPIer.GetValuePairs("values.dpapi", out string delimiter);
```

`RemoveValueUser` and `RemoveValueMachine` verify the file marker before rewriting. `RemoveValue` simply uses the marker.

## Key/Value Rules

Keys:

- cannot be null, empty, or whitespace
- cannot contain the delimiter
- cannot contain line breaks
- are trimmed when stored or read
- are compared case-insensitively

Values:

- cannot contain line breaks
- may be null when passed to storage; null is stored as an empty string
- are returned exactly as stored

Delimiters:

- may be null when passed to key/value methods; null means use `@delimiter`, or `=` when no marker exists
- cannot be empty when explicitly provided or stored in metadata
- cannot contain line breaks

Empty lines and non key/value text are ignored when retrieving values and preserved when storing or removing values.

For writing value files, the first decrypted line must be exactly the file marker:

```text
@scope u
```

or:

```text
@scope m
```

Files created or rewritten by the key/value methods include delimiter metadata as the second decrypted text line:

```text
@delimiter =
```

When storing a key that already exists, the existing value is replaced. If duplicate keys already exist in the encrypted file, storage keeps one record and removes the duplicates.

## Reading Whole Encrypted Text Files

Use this method to decrypt a file and return the plaintext as UTF-8 text:

```csharp
string text = DPAPIer.GetDecrypted("secret.dpapi");
```

## Testing Whether A File Can Be Decrypted

Use `CanDecrypt` to check whether DPAPI can unprotect a file:

```csharp
bool canRead = DPAPIer.CanDecrypt("secret.dpapi");
```

This only verifies the DPAPI operation. It does not prove the decrypted content is valid UTF-8 text or a valid key/value file.

## Exceptions

The library generally lets normal .NET and Win32-related exceptions reach the caller.

Common cases include:

- `ArgumentNullException` for null file names, byte arrays, or strings where null is invalid.
- `ArgumentException` for invalid keys, delimiters, values, or in-place/target combinations.
- `FileNotFoundException` when a source file does not exist.
- `PlatformNotSupportedException` outside Windows.
- `System.ComponentModel.Win32Exception` when Windows DPAPI fails to protect or unprotect the data.

## Security Notes

DPAPI protects data using Windows credentials and local system secrets. User-protected data is tied to the current Windows user. Machine-protected data is less restrictive because permitted users on the same machine may decrypt it.

This library does not add optional entropy, passphrases, prompting, compression, signing, or application-level access control.

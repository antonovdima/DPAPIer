# DPAPIer Console

DPAPIer is a Windows command-line utility for encrypting and decrypting files or key/value entries with Windows DPAPI.

DPAPI stands for Data Protection API. It protects local data using Windows-managed secrets, so an application can encrypt data without storing its own encryption key. DPAPIer can protect data for the current Windows user or for permitted users on the same machine.

***Attention!** DPAPI-encrypted files are not transferable to other computers and may not be recoverable after Windows is reinstalled, even on the same computer.*

***Attention!** DPAPI does not protect data from someone who can log in as the protected Windows user. Machine-protected data may also be readable by other permitted users on the same computer.*

The console app is built on top of `DPAPIerLib`, the library project in this solution.

## Platform

DPAPI is available only on Windows. The executable may build with .NET elsewhere, but encryption and decryption operations require Windows at runtime.

## Exit Codes

- `0`: command completed successfully.
- `1`: runtime failure, or `get` did not find the requested key.
- `2`: usage error, such as a missing required argument or invalid argument combination.

Successful commands print `Done`, except `get`, which prints the retrieved value. If `get` cannot find the key, it prints:

```text
*** not found ***
```

## User Or Machine

For commands that write encrypted data, choose who can decrypt it later:

- `u` or `user`: only the current Windows user account.
- `m` or `machine`: permitted Windows user accounts on the same machine.

This choice is stored inside the encrypted file. `decrypt` and `get` do not accept `user` or `machine`; Windows DPAPI reads the needed information from the encrypted data.

DPAPIer value files also store their protection choice as the first decrypted text line:

```text
@scope u
```

or:

```text
@scope m
```

DPAPIer value files created or rewritten by this utility store their key/value delimiter as the second decrypted text line:

```text
@delimiter =
```

If `-d` or `--delimiter` is omitted, an existing file uses its `@delimiter` marker. If the marker is missing, `=` is assumed. If a delimiter is provided and does not match the marker, the command fails.

## Commands

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

`put`, `set`, and `store` are synonyms. They create or overwrite a value.

`es` encrypts a provided string and prints the encrypted Base64 string. If `cc`, `-cc`, or `/cc` is included, the encrypted string is copied to the Clipboard instead and a reminder is printed. `ds` decrypts that encrypted string and prints the plaintext. `es` requires `user` or `machine`; `ds` does not accept a scope.

For `put`, `set`, and `store`, `user` or `machine` is required only when creating a value file. Existing value files use their `@scope` marker. If `user` or `machine` is provided for an existing file, it must match `@scope`.

For `remove`, `user` or `machine` is optional. If provided, it must match `@scope`.

`keys` prints all distinct keys found in the encrypted value file, one per line, in alphabetical order. Duplicate keys are printed once.

`values` and `vals` print recognized key/value pairs from the encrypted value file, one per line, in alphabetical order by key. Keys are trimmed, the file delimiter is printed between key and value, and values are printed exactly as stored.

For `reencrypt`, `user` or `machine` is the current protection. If `@scope` exists, it must match. If `@scope` is missing, DPAPIer trusts the command and writes the opposite marker into the result.

## Positional Forms

```text
DPAPIer e  <user|machine> [o] <file> [target]
DPAPIer d  [o] <file> [target]
DPAPIer es <user|machine> [cc] <string>
DPAPIer ds <encrypted-string>
DPAPIer re <user|machine> [o] <file> [target]
DPAPIer g  <file> <key> [delimiter]
DPAPIer keys|-keys <file> [delimiter]
DPAPIer values|vals|-values|-vals <file> [delimiter]
DPAPIer p  [user|machine] <file> <key> <value> [delimiter]
DPAPIer s  [user|machine] <file> <key> <value> [delimiter]
DPAPIer r  [user|machine] <file> <key> [delimiter]
```

Named value/file arguments and positional value/file arguments cannot be mixed in the same command. Delimiter is an exception and may be mixed with positional arguments.

## Named Arguments

```text
-f, --file, /file           Source file or encrypted value-store file.
-t, --target, /target       Target file for file encrypt/decrypt/reencrypt.
-k, --key, /key             Key name for value commands.
-v, --value, /value         Value to store.
cc, -cc, /cc                Copy encrypt-string result to Clipboard.
-d, --delimiter, /delimiter Key/value delimiter. For encrypt, adds value metadata. For value commands, uses @delimiter when omitted.
```

## Examples

```text
DPAPIer e user -f "plain file.txt" -t "secret file.dpapi"
DPAPIer es user "secret text"
DPAPIer -es -u "secret text"
DPAPIer es user cc "secret text"
DPAPIer ds "<encrypted string>"
DPAPIer e user -f "plain values.txt" -t "values.dpapi" -d "="
DPAPIer e user = "plain values.txt" "values.dpapi"
DPAPIer d -f "secret file.dpapi" -t "plain file.txt"
DPAPIer e u o "plain file.txt"
DPAPIer s user -f "values.dpapi" -k "Password" -v "correct horse battery staple"
DPAPIer s -f "values.dpapi" -k "Password" -v "updated value"
DPAPIer g -f "values.dpapi" -k "Password"
DPAPIer keys "values.dpapi"
DPAPIer -keys "values.dpapi"
DPAPIer values "values.dpapi"
DPAPIer vals "values.dpapi"
DPAPIer r -f "values.dpapi" -k "Password"
DPAPIer re user -f "values.dpapi" -t "machine-values.dpapi"
```

## Key/Value Data Rules

Keys:

- cannot be empty
- cannot contain the delimiter
- cannot contain line breaks
- are compared case-insensitively
- ignore surrounding spaces

Values:

- cannot contain line breaks
- are stored and returned exactly as supplied

Empty lines and non key/value text are ignored when retrieving values and preserved when storing or removing values.

Files created or rewritten by DPAPIer include `@delimiter` on the second decrypted text line. Older files without this marker are read with `=` unless a delimiter is explicitly provided.

For `encrypt`, delimiter may be supplied with `-d`, `--delimiter`, or `/delimiter`. As a shorthand, any one- or two-character delimiter made only from punctuation or symbols, such as `=`, `:`, `::`, or `=>`, is also recognized without an argument name and may be placed like `user`, `machine`, or `override`. When provided, DPAPIer adds `@scope` and `@delimiter` lines before encrypting the source file, so the encrypted result can be read by value commands such as `get`, `set`, and `remove`.

## Build

From the solution root:

```text
dotnet build
```

The debug executable is produced under:

```text
DPAPIer\bin\Debug\net10.0\DPAPIer.exe
```

# DPAPIer User Guide

DPAPIer protects local files and stored values with Windows DPAPI.

Use it when you want a local secret file that Windows can decrypt later without storing the secret as plain text.

DPAPI stands for Data Protection API. It protects local data using Windows-managed secrets, so an application can encrypt data without storing its own encryption key. DPAPIer can protect data for your current Windows user account or for permitted users on the same computer.

***Attention!** DPAPI-encrypted files are not transferable to other computers and may not be recoverable after Windows is reinstalled, even on the same computer.*

***Attention!** DPAPI does not protect data from someone who can log in as the protected Windows user. Machine-protected data may also be readable by other permitted users on the same computer.* 

## User Or Machine

When you encrypt or rewrite encrypted data, choose who can decrypt it later:

- `user`: only your current Windows user account.
- `machine`: permitted Windows user accounts on this same computer.

Short forms also work:

- `u` means `user`
- `m` means `machine`

This choice is stored inside the encrypted data. When you decrypt a file or read a stored value, do not provide `user` or `machine`; Windows reads the needed information from the encrypted file.

DPAPIer value files store this choice as their first decrypted text line, such as `@scope u` or `@scope m`. They also store the key/value delimiter as the second decrypted text line, such as `@delimiter =`.

When unsure, use `user` for writing commands.

## Encrypt a File

Create an encrypted copy:

```text
DPAPIer e user -f "plain.txt" -t "secret.dpapi"
```

This reads `plain.txt` and writes encrypted data to `secret.dpapi`.

To encrypt an existing text file as a value file, provide a delimiter:

```text
DPAPIer e user -f "plain-values.txt" -t "values.dpapi" -d "="
```

This adds `@scope` and `@delimiter` lines before encryption, so commands such as `get`, `store`, and `remove` can use the encrypted result.

For `encrypt`, short punctuation delimiters can also be used without `-d` and placed like other recognized options:

```text
DPAPIer e user = "plain-values.txt" "values.dpapi"
DPAPIer e user :: -f "plain-values.txt" -t "values.dpapi"
```

## Decrypt a File

Create a decrypted copy:

```text
DPAPIer d -f "secret.dpapi" -t "plain.txt"
```

No `user` or `machine` argument is used for decrypting.

## Replace the Original File

To encrypt or decrypt the original file directly, add `o` and omit `-t`:

```text
DPAPIer e user o -f "plain.txt"
DPAPIer d o -f "secret.dpapi"
```

`o` means override. It is required when the input file is replaced, or when a separate target file already exists.

## Store a Named Secret

Store one value by key:

```text
DPAPIer s user -f "values.dpapi" -k "Password" -v "correct horse battery staple"
```

This creates `values.dpapi` if it does not already exist. If `Password` already exists, it is updated. The written file records `@scope u`.

For an existing value file, omit `user` or `machine` unless you want DPAPIer to verify that your command matches the file:

```text
DPAPIer s -f "values.dpapi" -k "Password" -v "updated secret"
```

If you do not provide a delimiter, DPAPIer uses the file's `@delimiter` marker. Older files without that marker use `=`.

## Read a Named Secret

```text
DPAPIer g -f "values.dpapi" -k "Password"
```

The value is printed to the console. No `user` or `machine` argument is used for reading.

If the key is missing, DPAPIer prints:

```text
*** not found ***
```

## List Keys

```text
DPAPIer keys "values.dpapi"
```

or:

```text
DPAPIer -keys "values.dpapi"
```

This prints all distinct keys found in the encrypted value file, one per line, in alphabetical order. Duplicate keys are printed once.

## List Values

```text
DPAPIer values "values.dpapi"
```

or:

```text
DPAPIer vals "values.dpapi"
DPAPIer -values "values.dpapi"
DPAPIer -vals "values.dpapi"
```

This prints recognized key/value pairs from the encrypted value file, one per line, in alphabetical order by key. Keys are trimmed, the file delimiter is printed between key and value, and values are printed exactly as stored.

## Remove a Named Secret

```text
DPAPIer r user -f "values.dpapi" -k "Password"
```

Removing rewrites the encrypted file using the file's `@scope` marker. If you provide `user` or `machine`, it must match that marker. Removing a key that is not there is still considered successful.

## Re-Encrypt

Change a user-protected value file into a machine-protected value file:

```text
DPAPIer re user -f "values.dpapi" -t "machine-values.dpapi"
```

Change a machine-protected value file into a user-protected value file:

```text
DPAPIer re machine -f "values.dpapi" -t "user-values.dpapi"
```

For `re`, the `user` or `machine` argument describes the existing file's protection. If the file has `@scope`, it must match. If the file does not have `@scope`, DPAPIer trusts what you typed and writes the opposite marker into the result.

## Common Short Commands

```text
DPAPIer e u -f "plain.txt" -t "secret.dpapi"
DPAPIer d -f "secret.dpapi" -t "plain.txt"
DPAPIer s u -f "values.dpapi" -k "Password" -v "secret"
DPAPIer g -f "values.dpapi" -k "Password"
DPAPIer keys "values.dpapi"
DPAPIer values "values.dpapi"
DPAPIer r -f "values.dpapi" -k "Password"
```

## Tips

- Put quotes around file names or values that contain spaces.
- Delimiter may be mixed with positional arguments.
- For `encrypt`, one- or two-character delimiters made only from punctuation or symbols, such as `=`, `:`, `::`, or `=>`, can be used without `-d`.
- Use `user` for writing unless another Windows account on the same computer needs to decrypt the data.
- Do not provide `user` or `machine` for `decrypt` or `get`.
- For existing value files, `store` and `remove` use the file's `@scope`.
- For existing value files, omitted delimiter means the file's `@delimiter`; older files without it use `=`.
- Key names are not case-sensitive.
- Spaces around keys are ignored.
- Stored values cannot contain line breaks.

## Get Help

```text
DPAPIer -help
DPAPIer ?
DPAPIer -?
DPAPIer /?
DPAPIer --?
DPAPIer ? v
```

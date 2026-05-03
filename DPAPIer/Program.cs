using DPAPIerLib;

namespace DPAPIer;

internal static class Program {
    private const string NotFound = "*** not found ***";

    private static int Main(string[] args) {
        return DoIt(args);
    }

    //TestIt(@"e u C:\Temp\DPAPItest\Src.txt C:\Temp\DPAPItest\Encr.txt")
    //TestIt(@"g,u,C:\Temp\DPAPItest\Encr.txt,my url", ",")
    //TestIt(@"s,u,C:\Temp\DPAPItest\Encr.txt,my url,that url", ",")
    //TestIt(@"d u C:\Temp\DPAPItest\Encr.txt C:\Temp\DPAPItest\Decr.txt")

    public static int TestIt(string args, string delimiter=" ") {
        var a=args.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
        return DoIt(a);
    }

    private static int DoIt(string[] args) {
        try {
            if (args.Length == 0 || IsHelp(args[0])) {
                ShowHelp(IsVerboseHelp(args));
                return 0;
            }

            ParsedArguments parsed = ParsedArguments.Parse(args);

            return parsed.Action switch {
                ActionKind.Encrypt => Encrypt(parsed),
                ActionKind.Decrypt => Decrypt(parsed),
                ActionKind.ReEncrypt => ReEncrypt(parsed),
                ActionKind.Get => Get(parsed),
                ActionKind.Keys => Keys(parsed),
                ActionKind.Values => Values(parsed),
                ActionKind.Store => Store(parsed),
                ActionKind.Remove => Remove(parsed),
                _ => Fail("Unknown action.")
            };
        } catch (UsageException ex) {
            Console.Error.WriteLine($"Usage error: {ex.Message}");
            Console.Error.WriteLine();
            //ShowHelp(Console.Error);
            return 2;
        } catch (Exception ex) {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static int Encrypt(ParsedArguments args) {
        RequireFile(args);
        RequireScope(args);
        FileTarget target = ResolveFileTarget(args);

        if (args.Scope == Scope.Machine) DPAPIerLib.DPAPIer.EncryptFileMachine(args.File, target.TargetFile, target.InPlace, args.Delimiter);
        else DPAPIerLib.DPAPIer.EncryptFileUser(args.File, target.TargetFile, target.InPlace, args.Delimiter);

        Console.WriteLine("Done");
        return 0;
    }

    private static int Decrypt(ParsedArguments args) {
        RequireFile(args);
        ForbidScope(args);
        ForbidDelimiter(args);
        FileTarget target = ResolveFileTarget(args);

        DPAPIerLib.DPAPIer.DecryptFile(args.File, target.TargetFile, target.InPlace);

        Console.WriteLine("Done");
        return 0;
    }

    private static int ReEncrypt(ParsedArguments args) {
        RequireFile(args);
        RequireScope(args);
        ForbidDelimiter(args);
        FileTarget target = ResolveFileTarget(args);

        if (args.Scope == Scope.Machine) DPAPIerLib.DPAPIer.ReEncryptMachineToUser(args.File, target.TargetFile, target.InPlace);
        else DPAPIerLib.DPAPIer.ReEncryptUserToMachine(args.File, target.TargetFile, target.InPlace);

        Console.WriteLine("Done");
        return 0;
    }

    private static int Get(ParsedArguments args) {
        RequireFile(args);
        RequireKey(args);
        ForbidScope(args);
        ForbidValue(args);
        ForbidOverride(args);
        ForbidTarget(args);

        string? value = DPAPIerLib.DPAPIer.GetValue(args.File, args.Key, args.Delimiter, NotFound);

        Console.WriteLine(value);
        return string.Equals(value, NotFound, StringComparison.Ordinal) ? 1 : 0;
    }

    private static int Keys(ParsedArguments args) {
        RequireFile(args);
        ForbidScope(args);
        ForbidKey(args);
        ForbidValue(args);
        ForbidOverride(args);
        ForbidTarget(args);

        foreach (string key in DPAPIerLib.DPAPIer.GetAllValues(args.File, args.Delimiter).Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)) {
            Console.WriteLine(key);
        }

        return 0;
    }

    private static int Values(ParsedArguments args) {
        RequireFile(args);
        ForbidScope(args);
        ForbidKey(args);
        ForbidValue(args);
        ForbidOverride(args);
        ForbidTarget(args);

        List<KeyValuePair<string, string>> values = DPAPIerLib.DPAPIer.GetValuePairs(args.File, out string delimiter, args.Delimiter);
        foreach (KeyValuePair<string, string> pair in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
            Console.WriteLine(pair.Key + delimiter + pair.Value);
        }

        return 0;
    }

    private static int Store(ParsedArguments args) {
        RequireFile(args);
        RequireKey(args);
        RequireValue(args);
        RequireScopeWhenCreatingValueFile(args);
        ForbidOverride(args);
        ForbidTarget(args);

        if (args.Scope == Scope.Machine) DPAPIerLib.DPAPIer.StoreValueMachine(args.File, args.Key, args.Value, args.Delimiter);
        else if (args.Scope == Scope.User) DPAPIerLib.DPAPIer.StoreValueUser(args.File, args.Key, args.Value, args.Delimiter);
        else DPAPIerLib.DPAPIer.StoreValue(args.File, args.Key, args.Value, args.Delimiter);

        Console.WriteLine("Done");
        return 0;
    }

    private static int Remove(ParsedArguments args) {
        RequireFile(args);
        RequireKey(args);
        ForbidValue(args);
        ForbidOverride(args);
        ForbidTarget(args);

        if (args.Scope == Scope.Machine) DPAPIerLib.DPAPIer.RemoveValueMachine(args.File, args.Key, args.Delimiter);
        else if (args.Scope == Scope.User) DPAPIerLib.DPAPIer.RemoveValueUser(args.File, args.Key, args.Delimiter);
        else DPAPIerLib.DPAPIer.RemoveValue(args.File, args.Key, args.Delimiter);

        Console.WriteLine("Done");
        return 0;
    }

    private static FileTarget ResolveFileTarget(ParsedArguments args) {
        ForbidKey(args);
        ForbidValue(args);

        string targetFile = args.Target ?? string.Empty;
        bool inPlace = args.Target is null || SamePath(args.File!, args.Target);

        if (inPlace && !args.Override) throw new UsageException("Override is required when the source file is replaced.");
        if (!inPlace && File.Exists(args.Target) && !args.Override) throw new UsageException("Override is required when target file exists.");

        return new FileTarget(targetFile, inPlace);
    }

    private static bool SamePath(string left, string? right) {
        return !string.IsNullOrWhiteSpace(right)
            && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static void RequireFile(ParsedArguments args) {
        if (string.IsNullOrEmpty(args.File)) throw new UsageException("File is required.");
    }

    private static void RequireKey(ParsedArguments args) {
        if (string.IsNullOrEmpty(args.Key)) throw new UsageException("Key is required.");
    }

    private static void RequireValue(ParsedArguments args) {
        if (args.Value is null) throw new UsageException("Value is required.");
    }

    private static void RequireScopeWhenCreatingValueFile(ParsedArguments args) {
        if (args.Scope is null && !File.Exists(args.File)) {
            throw new UsageException("Protection choice is required when creating a value file: use u/user or m/machine.");
        }
    }

    private static void RequireScope(ParsedArguments args) {
        if (args.Scope is null) throw new UsageException("Protection choice is required: use u/user or m/machine.");
    }

    private static void ForbidScope(ParsedArguments args) {
        if (args.Scope is not null) throw new UsageException("Protection choice is not valid for this action.");
    }

    private static void ForbidKey(ParsedArguments args) {
        if (args.Key is not null) throw new UsageException("Key is not valid for this action.");
    }

    private static void ForbidValue(ParsedArguments args) {
        if (args.Value is not null) throw new UsageException("Value is not valid for this action.");
    }

    private static void ForbidTarget(ParsedArguments args) {
        if (args.Target is not null) throw new UsageException("Target is not valid for this action.");
    }

    private static void ForbidOverride(ParsedArguments args) {
        if (args.Override) throw new UsageException("Override is not valid for this action.");
    }

    private static void ForbidDelimiter(ParsedArguments args) {
        if (args.Delimiter is not null) throw new UsageException("Delimiter is not valid for this action.");
    }

    private static bool IsHelp(string value) {
        string normalized = NormalizeToken(value);
        return normalized is "h" or "hlp" or "help" or "?";
    }

    private static string NormalizeToken(string value) {
        string token = value.Trim();
        while (token.Length > 0 && (token[0] == '-' || token[0] == '/')) token = token[1..];
        return token.ToLowerInvariant();
    }

    private static int Fail(string message) {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static bool IsVerboseHelp(string[] args) {
        return args.Length > 1 && NormalizeToken(args[1]) is "v" or "verbose";
    }

    private static void ShowHelp(bool verbose = false) {
        ShowHelp(Console.Out, verbose);
    }

    private static void ShowHelp(TextWriter writer, bool verbose = false) {
        if (verbose) ShowVerboseHelp(writer);
        else ShowBriefHelp(writer);
    }

    private static void ShowBriefHelp(TextWriter writer) {
        writer.WriteLine("""
DPAPIer. (c) Dmitriy Antonov. All rights reserved.
Utility to encrypt/decrypt files or values with Windows DPAPI.

Usage:
  DPAPIer e|encrypt  u|user|m|machine [o|override] -f <file> [-t <target>] [-d <delimiter>]
  DPAPIer d|decrypt  [o|override] -f <file> [-t <target>]
  DPAPIer re|reencrypt u|user|m|machine [o|override] -f <file> [-t <target>]
  DPAPIer p|put      [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
  DPAPIer s|set|store [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
  DPAPIer g|get      -f <file> -k <key> [-d <delimiter>]
  DPAPIer keys|-keys -f <file> [-d <delimiter>]
  DPAPIer values|vals|-values|-vals -f <file> [-d <delimiter>]
  DPAPIer r|remove   [u|user|m|machine] -f <file> -k <key> [-d <delimiter>]

Arguments:
  u/user, m/machine     Who can decrypt later. Required for encrypt/reencrypt
                        and for creating value files.
  o/override           Allows replacing input file or existing target file.
  -f, --file, /file    Source or encrypted value file.
  -t, --target, /target Target file. Omit for in-place file operation.
  -k, --key, /key      Value key.
  -v, --value, /value  Value to store.
  -d, --delimiter      Key/value delimiter. Can be mixed with positional args.
                       For encrypt, adds value metadata. For value commands,
                       uses stored delimiter when omitted.

Examples:
  DPAPIer e u -f "plain file.txt" -t "secret file.dpapi"
  DPAPIer e u = "plain values.txt" "values.dpapi"
  DPAPIer d -f "secret file.dpapi" -t "plain file.txt"
  DPAPIer s user -f "values.dpapi" -k "Password" -v "correct horse battery staple"
  DPAPIer g -f "values.dpapi" -k "Password"
  DPAPIer keys "values.dpapi"
  DPAPIer values "values.dpapi"
  DPAPIer -keys "values.dpapi"
  DPAPIer r -f "values.dpapi" -k "Password"
  DPAPIer re user -f "user-values.dpapi" -t "machine-values.dpapi"

More detailed (verbose) help:
  DPAPIer -h v
  DPAPIer -hlp v
  DPAPIer -help v
  DPAPIer ? v
  DPAPIer -? v
  DPAPIer /? v
  DPAPIer --? v
""");
    }

    private static void ShowVerboseHelp(TextWriter writer) {
        writer.WriteLine("""
DPAPIer. (c) Dmitriy Antonov. All rights reserved.
Utility to encrypt/decrypt files or values with Windows DPAPI.

DPAPI is Windows Data Protection API. It protects local data using
Windows-managed secrets, either for the current user or for permitted users on
this machine. DPAPI-encrypted files are not transferable to other computers and
may not be recoverable after Windows is reinstalled, even on the same computer.
DPAPI does not protect data from someone who can log in as the protected
Windows user. Machine-protected data may also be readable by other permitted
users on the same computer.

When encrypting or rewriting encrypted data, choose who can decrypt it later:
  user     only current Windows user account
  machine  any permitted Windows user account on this machine
This choice is stored inside the encrypted data. When decrypting or getting a
stored value, DPAPI reads that information automatically, so no choice is needed.
DPAPIer value files also store this choice as the first decrypted text line:
  @scope u
  @scope m

Values are stored (a.k.a. set or put) into encrypted files as key/value pairs
with delimiter in between key and value. DPAPIer value files created or
rewritten by this utility store their delimiter as the second decrypted text
line, for example:
  @delimiter =

If delimiter is omitted, an existing file uses its @delimiter marker. If the
file has no marker, equal sign ("=") is assumed. If delimiter is provided and
does not match the file marker, the command fails.

All commands, options, argument names, keys, and file names are case-insensitive.
Spaces around keys are ignored. When values are stored, key is trimmed. Values
are stored as given and retrieved as stored, including spaces after delimiter.
Empty lines and non key/value text are ignored when retrieving values and
preserved when values are stored or removed.

Normal exit code is zero. All commands, if successfully completed, print "Done",
except "get", which prints requested value. Abnormal exit code is non-zero with
some explanation printed. If "get" cannot find a value, it prints
"*** not found ***" and returns exit code 1.

Usage:
  DPAPIer e|encrypt  u|user|m|machine [o|override] -f <file> [-t <target>] [-d <delimiter>]
    Encrypt entire file into target file. Use user or machine to choose who can
    decrypt it later. If delimiter is provided, @scope and @delimiter metadata
    are added before encryption, so the result can be used as a value file.

  DPAPIer d|decrypt  [o|override] -f <file> [-t <target>]
    Decrypt entire file into target file. No user/machine choice is accepted;
    DPAPI reads that from the encrypted file.

    If target file is omitted, result is saved into source file (in-place).
    In this case argument o or override is required, because source file is
    completely rewritten.

  DPAPIer re|reencrypt u|user|m|machine [o|override] -f <file> [-t <target>]
    Re-encrypt given value file from the provided current protection to the
    opposite one. If @scope is present, it must match. If @scope is missing,
    DPAPIer trusts the command and adds the opposite marker to the result.

  DPAPIer p|put      [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
  DPAPIer s|set|store [u|user|m|machine] -f <file> -k <key> -v <value> [-d <delimiter>]
    Put, set and store are synonyms here to save given key/value into given
    encrypted file. Record for identical key, if present, is replaced by this
    new one. User or machine is required only when creating the file. For an
    existing file, protection is read from @scope. If user or machine is still
    provided, it must match @scope.

  DPAPIer g|get      -f <file> -k <key> [-d <delimiter>]
    Get (obtain) value for given key from the given file. The value was likely
    previously stored by above put/set/store command. No user/machine choice is
    accepted; DPAPI reads that from the encrypted file.

  DPAPIer keys|-keys -f <file> [-d <delimiter>]
    Output all distinct keys found in the given encrypted value file. Keys are
    listed alphabetically; duplicate keys are printed once.

  DPAPIer values|vals|-values|-vals -f <file> [-d <delimiter>]
    Output all recognized key/value pairs found in the given encrypted value
    file. Keys are normalized and listed alphabetically; values are printed
    verbatim after the file delimiter.

  DPAPIer r|remove   [u|user|m|machine] -f <file> -k <key> [-d <delimiter>]
    Remove line for the given key from the given file. If such key is not
    present, then nothing is done - still successful return. Protection is read
    from @scope. If user or machine is provided, it must match @scope.

Positional forms:
  DPAPIer e  <user|machine> [o] <file> [target]
  DPAPIer d  [o] <file> [target]
  DPAPIer re <user|machine> [o] <file> [target]
  DPAPIer g  <file> <key> [delimiter]
  DPAPIer keys|-keys <file> [delimiter]
  DPAPIer values|vals|-values|-vals <file> [delimiter]
  DPAPIer p  [user|machine] <file> <key> <value> [delimiter]
  DPAPIer s  [user|machine] <file> <key> <value> [delimiter]
  DPAPIer r  [user|machine] <file> <key> [delimiter]

Named arguments:
  -f, --file, /file           Source or value-store file.
  -t, --target, /target       Target file.
  -k, --key, /key             Value key.
  -v, --value, /value         Value to store.
  -d, --delimiter, /delimiter Key/value delimiter. For encrypt, adds value
                              metadata. For value commands, uses @delimiter
                              when omitted.

Rules:
  u or user means only current Windows user account can decrypt later.
  m or machine means permitted Windows users on this machine can decrypt later.
  user/machine is required for encrypt and reencrypt.
  user/machine is required for put, set and store only when file does not exist.
  user/machine is optional for existing put, set, store and remove. If provided,
  it must match the file @scope marker.
  user/machine is not accepted for decrypt or get.
  o or override allows replacing the input file or an existing target file.
  Named arguments require -, --, or / prefixes.
  File, target, key, and value may be positional or named, but not mixed.
  Delimiter may be mixed with positional arguments.
  For encrypt, any one- or two-character delimiter made only from punctuation
  or symbols, such as =, :, ::, or =>, is recognized without -d and may be
  placed like user/machine or override.
  Values with spaces must be double-quoted.
  For encrypt, decrypt, and reencrypt, override is required when target is
  missing, target is source, or target file already exists.
  For get and remove, value should not be presented.

Examples:
  DPAPIer e u -f "plain file.txt" -t "secret file.dpapi"
  DPAPIer d "secret file.dpapi" "plain file.txt"
  DPAPIer e u o "plain file.txt"
  DPAPIer s user -f "values.dpapi" -k "Password" -v "correct horse battery staple"
  DPAPIer s -f "values.dpapi" -k "Password" -v "updated value"
  DPAPIer g -f "values.dpapi" -k "Password"
  DPAPIer keys "values.dpapi"
  DPAPIer -keys "values.dpapi"
  DPAPIer values "values.dpapi"
  DPAPIer vals "values.dpapi"
  DPAPIer r -f "values.dpapi" -k "Password"
  DPAPIer re user -f "user-values.dpapi" -t "machine-values.dpapi"

Help (v is this verbose help):
  DPAPIer
  DPAPIer -h
  DPAPIer -h v
  DPAPIer -hlp
  DPAPIer -hlp v
  DPAPIer -help
  DPAPIer -help v
  DPAPIer ?
  DPAPIer ? v
  DPAPIer -?
  DPAPIer -? v
  DPAPIer /?
  DPAPIer /? v
  DPAPIer --?
  DPAPIer --? v
""");
    }

    private sealed class ParsedArguments {
        public required ActionKind Action { get; init; }
        public Program.Scope? Scope { get; private set; }
        public bool Override { get; private set; }
        public string? File { get; private set; }
        public string? Target { get; private set; }
        public string? Key { get; private set; }
        public string? Value { get; private set; }
        public string? Delimiter { get; private set; }

        public static ParsedArguments Parse(string[] args) {
            ActionKind action = ParseAction(args[0]);
            ParsedArguments parsed = new() { Action = action };
            List<string> positional = [];
            bool sawNamed = false;
            bool sawPositionalData = false;

            for (int i = 1; i < args.Length; i++) {
                string arg = args[i];
                string token = NormalizeToken(arg);

                if (!sawNamed && !sawPositionalData && TryParseScope(token, out Program.Scope scope)) {
                    if (parsed.Scope is not null) throw new UsageException("Scope is presented more than once.");
                    parsed.Scope = scope;
                    continue;
                }

                if (!sawNamed && !sawPositionalData && (token is "o" or "override")) {
                    parsed.Override = true;
                    continue;
                }

                if (action == ActionKind.Encrypt && IsDelimiterShorthand(arg)) {
                    parsed.SetDelimiter(arg);
                    continue;
                }

                if (IsNamedArgument(arg)) {
                    if (token is "d" or "delimiter") {
                        if (++i >= args.Length) throw new UsageException($"Missing value for {arg}.");
                        parsed.SetDelimiter(args[i]);
                        continue;
                    }

                    sawNamed = true;
                    if (sawPositionalData) throw new UsageException("File, target, key, and value must be either all positional or all named.");
                    if (++i >= args.Length) throw new UsageException($"Missing value for {arg}.");
                    parsed.SetNamed(token, args[i]);
                    continue;
                }

                if (sawNamed) throw new UsageException("File, target, key, and value must be either all positional or all named.");
                sawPositionalData = true;
                positional.Add(arg);
            }

            parsed.ApplyPositionals(positional);
            return parsed;
        }

        private static ActionKind ParseAction(string value) {
            return NormalizeToken(value) switch {
                "e" or "encrypt" => ActionKind.Encrypt,
                "d" or "decrypt" => ActionKind.Decrypt,
                "re" or "reencrypt" => ActionKind.ReEncrypt,
                "g" or "get" => ActionKind.Get,
                "keys" => ActionKind.Keys,
                "values" or "vals" => ActionKind.Values,
                "p" or "put" => ActionKind.Store,
                "s" or "set" or "store" => ActionKind.Store,
                "r" or "remove" => ActionKind.Remove,
                _ => throw new UsageException($"Unknown action '{value}'.")
            };
        }

        private static bool TryParseScope(string token, out Program.Scope scope) {
            switch (token) {
                case "u":
                case "user":
                    scope = Program.Scope.User;
                    return true;
                case "m":
                case "machine":
                    scope = Program.Scope.Machine;
                    return true;
                default:
                    scope = Program.Scope.User;
                    return false;
            }
        }

        private static bool IsNamedArgument(string value) => value.StartsWith('-') || value.StartsWith('/');

        private static bool IsDelimiterShorthand(string value) {
            if (value.Length is < 1 or > 2) return false;

            foreach (char c in value) {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '/') return false;
            }

            return true;
        }

        private void SetDelimiter(string value) {
            if (Delimiter is not null) throw new UsageException("Duplicate delimiter argument.");
            Delimiter = value;
        }

        private void SetNamed(string token, string value) {
            switch (token) {
                case "f":
                case "file":
                    if (File is not null) throw new UsageException("Duplicate file argument.");
                    File = value;
                    break;
                case "t":
                case "target":
                    if (Target is not null) throw new UsageException("Duplicate target argument.");
                    Target = value;
                    break;
                case "k":
                case "key":
                    if (Key is not null) throw new UsageException("Duplicate key argument.");
                    Key = value;
                    break;
                case "v":
                case "value":
                    if (Value is not null) throw new UsageException("Duplicate value argument.");
                    Value = value;
                    break;
                default:
                    throw new UsageException($"Unknown argument '{token}'.");
            }
        }

        private void ApplyPositionals(List<string> values) {
            switch (Action) {
                case ActionKind.Encrypt:
                case ActionKind.Decrypt:
                case ActionKind.ReEncrypt:
                    if (values.Count > 2) throw new UsageException("Too many positional arguments for file action.");
                    if (values.Count > 0) File = values[0];
                    if (values.Count > 1) Target = values[1];
                    break;
                case ActionKind.Get:
                case ActionKind.Keys:
                case ActionKind.Values:
                case ActionKind.Remove:
                    int maxCount = Action is ActionKind.Keys or ActionKind.Values ? 2 : 3;
                    if (values.Count > maxCount) throw new UsageException("Too many positional arguments for value action.");
                    if (values.Count > 0) File = values[0];
                    if (Action is ActionKind.Keys or ActionKind.Values) {
                        if (values.Count > 1) SetDelimiter(values[1]);
                    } else {
                        if (values.Count > 1) Key = values[1];
                        if (values.Count > 2) SetDelimiter(values[2]);
                    }
                    break;
                case ActionKind.Store:
                    if (values.Count > 4) throw new UsageException("Too many positional arguments for store action.");
                    if (values.Count > 0) File = values[0];
                    if (values.Count > 1) Key = values[1];
                    if (values.Count > 2) Value = values[2];
                    if (values.Count > 3) SetDelimiter(values[3]);
                    break;
            }
        }

    }

    private enum ActionKind {
        Encrypt,
        Decrypt,
        ReEncrypt,
        Get,
        Keys,
        Values,
        Store,
        Remove
    }

    private enum Scope {
        User,
        Machine
    }

    private sealed record FileTarget(string TargetFile, bool InPlace);

    private sealed class UsageException(string message) : Exception(message);
}

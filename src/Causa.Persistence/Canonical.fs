// Owner: persistence. Strict JSON reader and RFC 8785 (JCS) canonical writer for causa-storage v1.0.0 (CONTRACT
// section 2), digests and the event-id derivation (section 5.2). The reader and writer are ported from the
// causa-data reference consumer (kemiller2002/causa-data, tools/consumers/clean-fsharp/Json.fs), same owner.
namespace Causa.Persistence

open System
open System.Globalization
open System.Security.Cryptography
open System.Text

module Canonical =
    type JsonValue =
        | JNull
        | JBool of bool
        | JInt of int64
        | JString of string
        | JArray of JsonValue list
        /// Members in document order (the parser rejects duplicates).
        | JObject of (string * JsonValue) list

    /// §2.3: integers are restricted to [-(2^53-1), 2^53-1].
    let maxSafeInteger = 9007199254740991L

    type private Parsed = Result<JsonValue * int, string>

    let private isWs (c: char) = c = ' ' || c = '\t' || c = '\n' || c = '\r'

    let rec private skipWs (s: string) i = if i < s.Length && isWs s[i] then skipWs s (i + 1) else i

    let private literal (s: string) i (lit: string) value : Parsed =
        if i + lit.Length <= s.Length && String.CompareOrdinal(s, i, lit, 0, lit.Length) = 0 then Ok(value, i + lit.Length)
        else Error $"invalid token at offset {i}"

    let private hex4 (s: string) i : Result<int, string> =
        if i + 4 <= s.Length && s.Substring(i, 4) |> Seq.forall Uri.IsHexDigit then
            Ok(Convert.ToInt32(s.Substring(i, 4), 16))
        else Error $"invalid \\u escape at offset {i}"

    let private isHigh (cp: int) = cp >= 0xD800 && cp <= 0xDBFF
    let private isLow (cp: int) = cp >= 0xDC00 && cp <= 0xDFFF

    /// Parses the body of a string after the opening quote. `acc` holds chars in reverse.
    let rec private stringBody (s: string) i (acc: char list) : Result<string * int, string> =
        if i >= s.Length then Error "unterminated string"
        else
            match s[i] with
            | '"' -> Ok(String(acc |> List.rev |> Array.ofList), i + 1)
            | '\\' when i + 1 >= s.Length -> Error "unterminated escape"
            | '\\' ->
                let simple c = stringBody s (i + 2) (c :: acc)
                match s[i + 1] with
                | '"' -> simple '"'
                | '\\' -> simple '\\'
                | '/' -> simple '/'
                | 'b' -> simple '\b'
                | 'f' -> simple '\f'
                | 'n' -> simple '\n'
                | 'r' -> simple '\r'
                | 't' -> simple '\t'
                | 'u' ->
                    hex4 s (i + 2)
                    |> Result.bind (fun cp ->
                        if isHigh cp then
                            if i + 7 < s.Length && s[i + 6] = '\\' && s[i + 7] = 'u' then
                                hex4 s (i + 8)
                                |> Result.bind (fun lo ->
                                    if isLow lo then stringBody s (i + 12) (char lo :: char cp :: acc)
                                    else Error $"unpaired high surrogate escape at offset {i}")
                            else Error $"unpaired high surrogate escape at offset {i}"
                        elif isLow cp then Error $"unpaired low surrogate escape at offset {i}"
                        elif cp = 0xFEFF then Error $"U+FEFF at offset {i} (only allowed as a forbidden leading BOM, §2.3)"
                        else stringBody s (i + 6) (char cp :: acc))
                | c -> Error $"invalid escape '\\{c}' at offset {i}"
            | '\uFEFF' -> Error $"U+FEFF at offset {i} (only allowed as a forbidden leading BOM, §2.3)"
            | c when c < ' ' -> Error $"unescaped control character U+{int c:X4} in string at offset {i}"
            | c when Char.IsHighSurrogate c ->
                if i + 1 < s.Length && Char.IsLowSurrogate s[i + 1] then stringBody s (i + 2) (s[i + 1] :: c :: acc)
                else Error $"unpaired surrogate at offset {i}"
            | c when Char.IsLowSurrogate c -> Error $"unpaired surrogate at offset {i}"
            | c -> stringBody s (i + 1) (c :: acc)

    let rec private digitsEnd (s: string) k = if k < s.Length && Char.IsAsciiDigit s[k] then digitsEnd s (k + 1) else k

    let private number (s: string) i : Parsed =
        let start = if i < s.Length && s[i] = '-' then i + 1 else i
        let stop = digitsEnd s start
        if stop = start then Error $"invalid number at offset {i}"
        elif s[start] = '0' && stop - start > 1 then Error $"leading zero in number at offset {i}"
        elif stop < s.Length && (s[stop] = '.' || s[stop] = 'e' || s[stop] = 'E') then
            Error $"non-integer number at offset {i} (floating point is not allowed)"
        else
            match Int64.TryParse(s.Substring(i, stop - i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) with
            | true, v when v >= -maxSafeInteger && v <= maxSafeInteger -> Ok(JInt v, stop)
            | _ -> Error $"integer outside [-(2^53-1), 2^53-1] at offset {i}"

    /// §2.3: readers accept nesting up to depth 128 (each object or array adds one level).
    let maxDepth = 128

    let rec private value (s: string) (depth: int) i : Parsed =
        let i = skipWs s i
        if i >= s.Length then Error "unexpected end of input"
        else
            match s[i] with
            | '{' | '[' when depth + 1 > maxDepth -> Error $"nesting deeper than {maxDepth} at offset {i}"
            | '{' -> objectMembers s (depth + 1) (skipWs s (i + 1)) [] Set.empty true
            | '[' -> arrayItems s (depth + 1) (skipWs s (i + 1)) [] true
            | '"' -> stringBody s (i + 1) [] |> Result.map (fun (str, j) -> JString str, j)
            | 't' -> literal s i "true" (JBool true)
            | 'f' -> literal s i "false" (JBool false)
            | 'n' -> literal s i "null" JNull
            | c when c = '-' || Char.IsAsciiDigit c -> number s i
            | c -> Error $"unexpected character '{c}' at offset {i}"

    and private arrayItems (s: string) (depth: int) i (acc: JsonValue list) (first: bool) : Parsed =
        if i < s.Length && s[i] = ']' && first then Ok(JArray [], i + 1)
        else
            value s depth i
            |> Result.bind (fun (v, j) ->
                let j = skipWs s j
                if j < s.Length && s[j] = ',' then arrayItems s depth (skipWs s (j + 1)) (v :: acc) false
                elif j < s.Length && s[j] = ']' then Ok(JArray(List.rev (v :: acc)), j + 1)
                else Error $"expected ',' or ']' at offset {j}")

    and private objectMembers (s: string) (depth: int) i (acc: (string * JsonValue) list) (seen: Set<string>) (first: bool) : Parsed =
        if i < s.Length && s[i] = '}' && first then Ok(JObject [], i + 1)
        elif i >= s.Length || s[i] <> '"' then Error $"expected object key at offset {i}"
        else
            stringBody s (i + 1) []
            |> Result.bind (fun (key, j) ->
                if seen.Contains key then Error $"duplicate object key \"{key}\" at offset {i}"
                else
                    let j = skipWs s j
                    if j >= s.Length || s[j] <> ':' then Error $"expected ':' at offset {j}"
                    else
                        value s depth (j + 1)
                        |> Result.bind (fun (v, k) ->
                            let k = skipWs s k
                            let acc = (key, v) :: acc
                            if k < s.Length && s[k] = ',' then objectMembers s depth (skipWs s (k + 1)) acc (seen.Add key) false
                            elif k < s.Length && s[k] = '}' then Ok(JObject(List.rev acc), k + 1)
                            else Error $"expected ',' or '}}' at offset {k}"))

    /// Parses exactly one JSON text (insignificant whitespace is syntactically legal here;
    /// canonical form is a separate, later check: CONTRACT §11.1 stages 3 and 4).
    let parse (text: string) : Result<JsonValue, string> =
        value text 0 0
        |> Result.bind (fun (v, j) ->
            let j = skipWs text j
            if j = text.Length then Ok v else Error $"trailing content at offset {j}")

    // ---------------------------------------------------------------- RFC 8785 writer

    let private escapeChar (c: char) =
        match c with
        | '"' -> "\\\""
        | '\\' -> "\\\\"
        | '\b' -> "\\b"
        | '\f' -> "\\f"
        | '\n' -> "\\n"
        | '\r' -> "\\r"
        | '\t' -> "\\t"
        | c when c < ' ' -> $"\\u{int c:x4}"
        | c -> string c

    let private quote (s: string) = "\"" + (s |> Seq.map escapeChar |> String.concat "") + "\""

    /// RFC 8785: members sorted by UTF-16 code units, no whitespace, JCS string escaping.
    /// Integers in the safe range serialize as plain decimal (the ES6 Number form of an integer).
    let rec canonical (v: JsonValue) : string =
        match v with
        | JNull -> "null"
        | JBool true -> "true"
        | JBool false -> "false"
        | JInt n -> n.ToString(CultureInfo.InvariantCulture)
        | JString s -> quote s
        | JArray xs -> "[" + (xs |> List.map canonical |> String.concat ",") + "]"
        | JObject members ->
            let body =
                members
                |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
                |> List.map (fun (k, x) -> quote k + ":" + canonical x)
                |> String.concat ","
            "{" + body + "}"

    // ---------------------------------------------------------------- accessors

    let field (name: string) (v: JsonValue) : JsonValue option =
        match v with
        | JObject members -> members |> List.tryFind (fun (k, _) -> k = name) |> Option.map snd
        | _ -> None

    let str (name: string) (v: JsonValue) : string option =
        match field name v with
        | Some(JString s) -> Some s
        | _ -> None

    let integer (name: string) (v: JsonValue) : int64 option =
        match field name v with
        | Some(JInt n) -> Some n
        | _ -> None

    let arr (name: string) (v: JsonValue) : JsonValue list option =
        match field name v with
        | Some(JArray xs) -> Some xs
        | _ -> None

    let typeName (v: JsonValue) =
        match v with
        | JNull -> "null"
        | JBool _ -> "boolean"
        | JInt _ -> "integer"
        | JString _ -> "string"
        | JArray _ -> "array"
        | JObject _ -> "object"

    /// The canonical text of a JSON document (no trailing LF) as UTF-8 bytes.
    let bytesOf (v: JsonValue) : byte[] = Encoding.UTF8.GetBytes(canonical v)

    /// A JSON file: one canonical text followed by exactly one LF (section 2.2).
    let fileOf (v: JsonValue) : byte[] = Encoding.UTF8.GetBytes(canonical v + "\n")

    let sha256Hex (bytes: byte[]) : string = SHA256.HashData(bytes) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

    /// The digest of a JSON document: SHA-256 of its canonical text without the trailing LF (section 2.11).
    let digest (v: JsonValue) : string = sha256Hex (bytesOf v)

    /// RFC 4122 name-based UUID (version 5, SHA-1).
    let uuidV5 (ns: Guid) (name: string) : Guid =
        let nsBytes = ns.ToByteArray(true)
        let hash = SHA1.HashData(Array.append nsBytes (Encoding.UTF8.GetBytes name))
        let b = hash[0..15]
        b[6] <- (b[6] &&& 0x0Fuy) ||| 0x50uy
        b[8] <- (b[8] &&& 0x3Fuy) ||| 0x80uy
        Guid(ReadOnlySpan<byte>(b), true)

    /// N of section 5.2: UUIDv5(NAMESPACE_URL, "https://github.com/kemiller2002/causa-data/contract/v1/event-id").
    let eventIdNamespace = uuidV5 (Guid "6ba7b811-9dad-11d1-80b4-00c04fd430c8") "https://github.com/kemiller2002/causa-data/contract/v1/event-id"

    let uuidText (g: Guid) = g.ToString("D")

    /// eventId = UUIDv5(N, investigationId + "/" + aggregateVersion + "/" + correlationId) (section 5.2).
    let eventId (investigation: Guid) (version: int64) (correlation: Guid) : Guid =
        uuidV5 eventIdNamespace (sprintf "%s/%d/%s" (uuidText investigation) version (uuidText correlation))

    /// RFC 3339 UTC with exactly three fractional digits (section 2.9); None for a sub-millisecond instant, which a
    /// writer refuses (CAUSA.UNREPRESENTABLE) rather than truncate.
    let timestamp (t: DateTimeOffset) : string option =
        let u = t.ToUniversalTime()
        if u.Ticks % TimeSpan.TicksPerMillisecond <> 0L then None
        else Some(u.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))

    let parseTimestamp (s: string) : DateTimeOffset option =
        if Text.RegularExpressions.Regex.IsMatch(s, "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{3}Z\\z") then
            match DateTimeOffset.TryParseExact(s, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal) with
            | true, v -> Some v
            | _ -> None
        else None

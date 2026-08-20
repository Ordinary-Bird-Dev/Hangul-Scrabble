# Imports a vocab list into Assets/Resources in the schema WordValidator
# expects: {"words": [...]} wrapping, UTF-8, one entry per sense.
#
#   powershell -ExecutionPolicy Bypass -File Tools\import_vocab.ps1 `
#       -In C:\path\to\vocabB_words.json -Out vocabB_words
#
# Accepts either a bare JSON array or an already-wrapped {"words": [...]}
# file. Duplicate triage, per the corpus rules:
#  - identical word + identical english  -> exact duplicate, dropped
#    (first occurrence wins)
#  - identical word + differing english  -> genuine homonym, kept as a
#    separate sense entry (WordValidator groups senses per word at load)
#
# Writes <Out>.json next to topik1_words.json and a minimal .meta with a
# fresh guid if one does not already exist (re-imports keep the guid).

param(
    [Parameter(Mandatory = $true)][string]$In,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'Assets\Resources'
$outPath = Join-Path $outDir ($Out + '.json')
$metaPath = $outPath + '.meta'

$raw = Get-Content $In -Raw -Encoding UTF8
$parsed = $raw | ConvertFrom-Json
if ($parsed -is [System.Array]) {
    $entries = $parsed
} elseif ($null -ne $parsed.words) {
    $entries = $parsed.words
} else {
    throw "Unrecognized JSON shape in $In - expected a bare array or {""words"": [...]}."
}

# Duplicate triage. Key: word + US (unit separator) + english.
$seen = @{}
$wordGloss = @{}
$kept = New-Object System.Collections.Generic.List[object]
$droppedExact = 0
$homonymWords = @{}

foreach ($e in $entries) {
    $key = $e.word + [char]0x1F + $e.english
    if ($seen.ContainsKey($key)) { $droppedExact++; continue }
    $seen[$key] = $true
    if ($wordGloss.ContainsKey($e.word)) { $homonymWords[$e.word] = $true }
    $wordGloss[$e.word] = $true
    $kept.Add($e)
}

# Hand-rolled JSON writer: ConvertTo-Json in PS 5.1 escapes all non-ASCII
# to \uXXXX, which would make the Korean unreadable in diffs. Only the
# characters JSON requires are escaped; Hangul stays raw.
function Esc([string]$s) {
    if ($null -eq $s) { return '' }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        switch ($ch) {
            '\' { [void]$sb.Append('\\') }
            '"' { [void]$sb.Append('\"') }
            "`n" { [void]$sb.Append('\n') }
            "`r" { [void]$sb.Append('\r') }
            "`t" { [void]$sb.Append('\t') }
            default {
                if ([int]$ch -lt 0x20) { [void]$sb.Append('\u{0:x4}' -f [int]$ch) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    return $sb.ToString()
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("{`n  ""words"": [`n")
for ($i = 0; $i -lt $kept.Count; $i++) {
    $e = $kept[$i]
    [void]$sb.Append("    {`n")
    [void]$sb.Append('      "word": "' + (Esc $e.word) + "`",`n")
    [void]$sb.Append('      "english": "' + (Esc $e.english) + "`",`n")
    [void]$sb.Append('      "romanization": "' + (Esc $e.romanization) + "`",`n")
    [void]$sb.Append('      "example": "' + (Esc $e.example) + "`",`n")
    [void]$sb.Append('      "syllable_count": ' + [int]$e.syllable_count + "`n")
    if ($i -lt $kept.Count - 1) { [void]$sb.Append("    },`n") } else { [void]$sb.Append("    }`n") }
}
[void]$sb.Append("  ]`n}`n")

[System.IO.File]::WriteAllText($outPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

if (-not (Test-Path $metaPath)) {
    $guid = [guid]::NewGuid().ToString('N')
    $meta = "fileFormatVersion: 2`nguid: $guid`n"
    [System.IO.File]::WriteAllText($metaPath, $meta, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "created $metaPath (guid $guid)"
}

Write-Output ("{0}: {1} entries in, {2} kept, {3} exact duplicates dropped, {4} homonym words retained as multiple senses" -f `
    $Out, $entries.Count, $kept.Count, $droppedExact, $homonymWords.Count)

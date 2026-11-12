# Aiko runs this after the person's profile, in PowerShell 7 and Windows PowerShell. It adds the marks
# of shell integration (OSC 133) around the person's own prompt: D the end of the last command with
# its exit code, A the prompt, B the command, and C when the command starts. The window cuts the
# output into one block per command by these marks (D-412).

if (-not $global:__aikoMarks) {
    $global:__aikoMarks = $true
    $global:__aikoPrompt = $function:prompt

    function global:prompt {
        $ok = $?
        $code = if ($ok) { 0 } elseif ($global:LASTEXITCODE) { $global:LASTEXITCODE } else { 1 }
        $e = [char]27
        $b = [char]7
        $text = & $global:__aikoPrompt
        "$e]133;D;$code$b$e]133;A$b$text$e]133;B$b"
    }

    if (Get-Command PSConsoleHostReadLine -ErrorAction SilentlyContinue) {
        $global:__aikoReadLine = $function:PSConsoleHostReadLine
        function global:PSConsoleHostReadLine {
            $line = & $global:__aikoReadLine
            [Console]::Write("$([char]27)]133;C$([char]7)")
            $line
        }
    }
}

# The person's .zshrc, read from their own ZDOTDIR (see .zshenv here), then the marks of shell
# integration (OSC 133) on top of whatever prompt it set.
ZDOTDIR="$AIKO_USER_ZDOTDIR"
[[ -r "$ZDOTDIR/.zshrc" ]] && . "$ZDOTDIR/.zshrc"
ZDOTDIR="$AIKO_ZDOTDIR"

# D ends the last command with its exit code, A starts the prompt. First in the list, so the code is
# the one of the command and not of another hook.
__aiko_prompt_start() {
    local code=$?
    printf '\e]133;D;%s\a\e]133;A\a' "$code"
    return $code
}

# B after the prompt, where the typed command starts. Last in the list, after a theme rebuilt the
# prompt; added again only when the prompt lost it.
__aiko_prompt_end() {
    [[ "$PS1" == *$'\e]133;B\a'* ]] || PS1="${PS1}%{"$'\e]133;B\a'"%}"
}

# C: the command runs, its output starts here.
__aiko_output() {
    printf '\e]133;C\a'
}

precmd_functions=(__aiko_prompt_start $precmd_functions __aiko_prompt_end)
preexec_functions+=(__aiko_output)

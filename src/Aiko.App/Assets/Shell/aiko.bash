# Aiko starts Git Bash with this file instead of the login files, reads them itself, and then adds the
# marks of shell integration (OSC 133): A the prompt, B the command, C the output, D the end with its
# exit code. The window cuts the output into one block per command by these marks (D-412).

[ -r /etc/profile ] && . /etc/profile
if [ -r ~/.bash_profile ]; then
    . ~/.bash_profile
elif [ -r ~/.bash_login ]; then
    . ~/.bash_login
elif [ -r ~/.profile ]; then
    . ~/.profile
fi

__aiko_prompt() {
    local code=$?
    printf '\e]133;D;%s\a\e]133;A\a' "$code"
    return $code
}

PROMPT_COMMAND="__aiko_prompt${PROMPT_COMMAND:+; $PROMPT_COMMAND}"
PS1="${PS1}"'\[\e]133;B\a\]'
PS0='\e]133;C\a'

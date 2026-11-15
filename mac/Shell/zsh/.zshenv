# Aiko starts zsh with ZDOTDIR pointing here, so zsh reads these four files instead of the person's.
# Each one reads the person's own file of the same name first, from their own ZDOTDIR, and the last
# one gives ZDOTDIR back. .zshrc then adds the marks of shell integration (OSC 133): A the prompt,
# B the command, C the output, D the end with its exit code. The window cuts the output into one
# block per command by these marks (D-412). The twin of Assets/Shell/aiko.bash on Windows.

AIKO_ZDOTDIR="$ZDOTDIR"
ZDOTDIR="${AIKO_USER_ZDOTDIR:-$HOME}"
[[ -r "$ZDOTDIR/.zshenv" ]] && . "$ZDOTDIR/.zshenv"
# The person's .zshenv may move ZDOTDIR; the other files are read from where it points now.
AIKO_USER_ZDOTDIR="$ZDOTDIR"
ZDOTDIR="$AIKO_ZDOTDIR"

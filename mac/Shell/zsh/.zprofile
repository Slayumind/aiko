# The person's .zprofile, read from their own ZDOTDIR (see .zshenv here).
ZDOTDIR="$AIKO_USER_ZDOTDIR"
[[ -r "$ZDOTDIR/.zprofile" ]] && . "$ZDOTDIR/.zprofile"
ZDOTDIR="$AIKO_ZDOTDIR"

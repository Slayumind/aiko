# The person's .zlogin, read from their own ZDOTDIR (see .zshenv here). The last file of a login
# shell: ZDOTDIR goes back to the person's, so what runs in the shell sees their own.
ZDOTDIR="$AIKO_USER_ZDOTDIR"
[[ -r "$ZDOTDIR/.zlogin" ]] && . "$ZDOTDIR/.zlogin"
unset AIKO_ZDOTDIR AIKO_USER_ZDOTDIR

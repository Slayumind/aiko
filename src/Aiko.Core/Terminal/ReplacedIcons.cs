namespace Aiko.Core.Terminal;

/// The icons given to the taskbar button of the terminal window, and which of them may be
/// destroyed. Explorer reads the icon of a button a little after it changes, and later still when
/// the machine is busy, for example with a game. An icon destroyed before that is drawn as a black
/// square. So an icon lives a while after a newer one took its place.
///
/// The time counts from the replacement, not from the moment the icon was given. Counted from
/// the moment it was given, the icon that stood on the button between two faces was already old
/// when the next face came, and it was destroyed the moment it was replaced.
public sealed class ReplacedIcons<T>
{
    private readonly long _keep;
    private readonly Queue<(T Icon, long Replaced)> _replaced = new();
    private T? _current;
    private bool _hasCurrent;

    public ReplacedIcons(TimeSpan keep) => _keep = (long)keep.TotalMilliseconds;

    /// A new icon on the button, at a time in milliseconds. Gives back the icons that may be
    /// destroyed now, oldest first. The icon on the button is never among them.
    public IReadOnlyList<T> Give(T icon, long now)
    {
        if (_hasCurrent)
        {
            _replaced.Enqueue((_current!, now));
        }

        _current = icon;
        _hasCurrent = true;

        var gone = new List<T>();
        while (_replaced.Count > 0 && now - _replaced.Peek().Replaced > _keep)
        {
            gone.Add(_replaced.Dequeue().Icon);
        }

        return gone;
    }

    /// The window is closed: every icon may go.
    public IReadOnlyList<T> TakeAll()
    {
        var all = _replaced.Select(entry => entry.Icon).ToList();
        if (_hasCurrent)
        {
            all.Add(_current!);
        }

        _replaced.Clear();
        _current = default;
        _hasCurrent = false;
        return all;
    }
}

using System.Text;

namespace Aiko.Core.Terminal;

/// What the parser found in the output of a terminal (D-412). A sequence split between two chunks
/// is kept until its end comes, so the receiver sees whole pieces only.
public interface IVtReceiver
{
    /// One printable character, a whole code point.
    void Print(int codePoint);

    /// A control character: CR, LF, BS, TAB and the like.
    void Control(char control);

    /// A CSI sequence: its parameters (missing ones are -1), the private marker ('?', '>' or '\0')
    /// and the final character.
    void Csi(IReadOnlyList<int> parameters, char marker, char final);

    /// An escape sequence with no CSI: ESC 7, ESC 8, ESC M and the like.
    void Escape(char final);

    /// An operating system command: its text without ESC ] and the terminator.
    void Osc(string text);
}

/// Splits the output of a terminal into characters and sequences: the states of the parser in
/// the VT500 diagram, cut down to what a shell and ConPTY send. DCS, SOS, PM and APC strings are
/// skipped whole. Rows: spec/cases/shell-screen.
public sealed class VtParser
{
    private enum State { Ground, Escape, EscapeIntermediate, Csi, Osc, OscEscape, Skip, SkipEscape }

    private const int MaxParameters = 32;
    private const int MaxOscLength = 4096;

    private readonly IVtReceiver _receiver;
    private readonly List<int> _parameters = [];
    private readonly StringBuilder _osc = new();
    private State _state = State.Ground;
    private int _current = -1;
    private char _marker;
    private char _highSurrogate;

    public VtParser(IVtReceiver receiver) => _receiver = receiver;

    public void Feed(string chunk)
    {
        foreach (var c in chunk)
        {
            Step(c);
        }
    }

    private void Step(char c)
    {
        switch (_state)
        {
            case State.Ground:
                Ground(c);
                break;
            case State.Escape:
                AfterEscape(c);
                break;
            case State.EscapeIntermediate:
                // ESC ( B and the like choose a character set: one more character, nothing to do.
                _state = State.Ground;
                break;
            case State.Csi:
                InCsi(c);
                break;
            case State.Osc:
                InOsc(c);
                break;
            case State.OscEscape:
                if (c == '\\')
                {
                    EndOsc();
                }
                else
                {
                    _state = State.Osc;
                    InOsc(c);
                }

                break;
            case State.Skip:
                if (c == '\u001b')
                {
                    _state = State.SkipEscape;
                }
                else if (c == '\u0007')
                {
                    _state = State.Ground;
                }

                break;
            case State.SkipEscape:
                _state = c == '\\' ? State.Ground : State.Skip;
                break;
        }
    }

    private void Ground(char c)
    {
        if (c == '\u001b')
        {
            _state = State.Escape;
            return;
        }

        if (c < 0x20 || c == 0x7f)
        {
            if (c != 0x7f)
            {
                _receiver.Control(c);
            }

            return;
        }

        if (char.IsHighSurrogate(c))
        {
            _highSurrogate = c;
            return;
        }

        if (char.IsLowSurrogate(c) && _highSurrogate != '\0')
        {
            _receiver.Print(char.ConvertToUtf32(_highSurrogate, c));
            _highSurrogate = '\0';
            return;
        }

        _highSurrogate = '\0';
        _receiver.Print(c);
    }

    private void AfterEscape(char c)
    {
        switch (c)
        {
            case '[':
                _parameters.Clear();
                _current = -1;
                _marker = '\0';
                _state = State.Csi;
                break;
            case ']':
                _osc.Clear();
                _state = State.Osc;
                break;
            case 'P' or 'X' or '^' or '_':
                _state = State.Skip;
                break;
            case '(' or ')' or '*' or '+' or '#' or '%':
                _state = State.EscapeIntermediate;
                break;
            default:
                _state = State.Ground;
                _receiver.Escape(c);
                break;
        }
    }

    private void InCsi(char c)
    {
        if (c is >= '0' and <= '9')
        {
            _current = (_current < 0 ? 0 : _current) * 10 + (c - '0');
            if (_current > 99_999)
            {
                _current = 99_999;
            }
        }
        else if (c is ';' or ':')
        {
            AddParameter();
        }
        else if (c is '?' or '>' or '<' or '=')
        {
            _marker = c;
        }
        else if (c is >= ' ' and <= '/')
        {
            // An intermediate byte, as in CSI ? 2026 $ p: the final byte still ends it.
        }
        else if (c is >= '@' and <= '~')
        {
            AddParameter();
            _state = State.Ground;
            _receiver.Csi(_parameters, _marker, c);
        }
        else if (c == '\u001b')
        {
            _state = State.Escape;
        }
        else
        {
            // A control in the middle of a sequence acts at once, as in a real terminal.
            _receiver.Control(c);
        }
    }

    private void AddParameter()
    {
        if (_parameters.Count < MaxParameters)
        {
            _parameters.Add(_current);
        }

        _current = -1;
    }

    private void InOsc(char c)
    {
        if (c == '\u0007')
        {
            EndOsc();
        }
        else if (c == '\u001b')
        {
            _state = State.OscEscape;
        }
        else if (_osc.Length < MaxOscLength)
        {
            _osc.Append(c);
        }
    }

    private void EndOsc()
    {
        _state = State.Ground;
        _receiver.Osc(_osc.ToString());
    }
}

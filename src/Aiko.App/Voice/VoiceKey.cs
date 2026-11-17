using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace Aiko.App;

/// The person's own OpenAI key, kept in Windows Credential Manager (D-313).
///
/// Never in a file of Aiko, the log or the settings. Windows keeps it encrypted for this user on
/// this computer only (not roaming). It is read into memory right before a recording goes to
/// OpenAI and nowhere else; removing Aiko erases it.
static unsafe class VoiceKey
{
    /// The name it has in Credential Manager, where a person can find and delete it by hand.
    private const string Target = "Aiko: OpenAI key for voice input";

    public static bool Stored
    {
        get
        {
            using var key = new Read();
            return key.Text is not null;
        }
    }

    /// Null when no key is stored.
    public static string? Load()
    {
        using var key = new Read();
        return key.Text;
    }

    /// False when Windows did not take it.
    public static bool Save(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        fixed (char* target = Target)
        fixed (byte* blob = bytes)
        {
            var credential = new CREDENTIALW
            {
                Type = CRED_TYPE.CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
            };
            var saved = PInvoke.CredWrite(&credential, 0);
            Array.Clear(bytes);
            return saved;
        }
    }

    public static void Remove()
    {
        fixed (char* target = Target)
        {
            PInvoke.CredDelete(target, CRED_TYPE.CRED_TYPE_GENERIC, 0);
        }
    }

    /// One read, with the memory Windows gave back freed at the end.
    private readonly ref struct Read
    {
        private readonly CREDENTIALW* _credential;

        public Read()
        {
            fixed (char* target = Target)
            {
                CREDENTIALW* credential;
                if (PInvoke.CredRead(target, CRED_TYPE.CRED_TYPE_GENERIC, 0, &credential))
                {
                    _credential = credential;
                }
            }
        }

        public string? Text => _credential is null || _credential->CredentialBlobSize == 0
            ? null
            : Encoding.UTF8.GetString(_credential->CredentialBlob, (int)_credential->CredentialBlobSize);

        public void Dispose()
        {
            if (_credential is not null)
            {
                PInvoke.CredFree(_credential);
            }
        }
    }
}

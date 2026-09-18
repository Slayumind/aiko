global using static Aiko.Core.Tests.TestPlatform;

using Aiko.Core;

namespace Aiko.Core.Tests;

/// The tests speak about the Windows build, the only one there is today.
public static class TestPlatform
{
    public static readonly PlatformConventions Windows = PlatformConventions.Windows;
}

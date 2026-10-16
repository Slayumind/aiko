import Testing

@testable import AikoKit

/// The twin of CompactSizeTests.cs.
struct CompactSizeTests {
    @Test(arguments: [
        (46425, 46), (6302, 6), (1500, 2), (1499, 1), (120, 1), (0, 1), (1_000_000, 1000),
    ])
    func tokensShowInThousandsAndNeverAsZero(tokens: Int, thousands: Int) {
        #expect(CompactSize.thousands(tokens) == thousands)
    }

    @Test(arguments: [
        (46425, 6302, true), (46425, 0, false), (0, 6302, false), (0, 0, false),
    ])
    func aSizeNeedsBothNumbers(before: Int, after: Int, known: Bool) {
        #expect((CompactSize.of(before: before, after: after) != nil) == known)
    }
}

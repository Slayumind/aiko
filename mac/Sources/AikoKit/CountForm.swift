import Foundation

/// Which form of a word goes with a number. Russian has three (1 строка, 2 строки, 5 строк); English
/// has two and takes few as many. The twin of CountForm.cs.
public enum CountForm: Sendable, Equatable {
    case one
    case few
    case many

    /// The Russian rule: the last digit decides, except for the teens, which always take many.
    public static func of(_ count: Int64) -> CountForm {
        let n = count.magnitude
        let lastTwo = n % 100
        if (11...14).contains(lastTwo) {
            return .many
        }

        switch n % 10 {
        case 1: return .one
        case 2, 3, 4: return .few
        default: return .many
        }
    }
}

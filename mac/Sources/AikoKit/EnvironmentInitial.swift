import Foundation

/// The letter of an environment beside its ring on the open island (D-421): two rings with only
/// numbers did not say which account is which. The twin of EnvironmentInitial.cs.
public enum EnvironmentInitial {
    /// The first letter or digit of the name, in capitals; a name without one gets none.
    public static func of(_ name: String) -> String {
        guard let first = name.first(where: { $0.isLetter || $0.isNumber }) else {
            return ""
        }

        return String(first).uppercased()
    }
}

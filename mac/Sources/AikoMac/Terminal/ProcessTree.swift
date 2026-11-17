import Darwin
import Foundation

/// Ends a program of a session with everything it started, the twin of ProcessJob.cs (D-351). The
/// terminal starts each program as the leader of a session of its own, and what it starts stays in
/// that session even in a group of its own, as a server run in the background is. SwiftTerm ends
/// only the leader; this ends the whole session first.
///
/// Every process of the session gets the hang-up a closed terminal gives and a request to end. An
/// interactive shell ignores the request, so what is still there a second later is killed, as the
/// job of Windows kills everything it holds.
enum ProcessTree {
    /// How long a program has to end by itself before it is killed.
    static let grace: TimeInterval = 1

    static func endSession(of leader: pid_t) {
        guard leader > 0 else { return }
        for pid in members(of: leader) {
            kill(pid, SIGHUP)
            kill(pid, SIGTERM)
        }
        DispatchQueue.global().asyncAfter(deadline: .now() + grace) {
            // Only what is still in the same session: a number freed in the meantime is not in it.
            for pid in members(of: leader) {
                kill(pid, SIGKILL)
            }
        }
    }

    /// The same for Aiko on its way out, which would not live to kill what is left later: it waits
    /// for the sessions here, at most the grace, and kills what is still there.
    static func endNow(_ leaders: [pid_t]) {
        let all = leaders.filter { $0 > 0 }
        for pid in all.flatMap(members(of:)) {
            kill(pid, SIGHUP)
            kill(pid, SIGTERM)
        }
        let until = Date().addingTimeInterval(grace)
        while Date() < until, all.contains(where: { !members(of: $0).isEmpty }) {
            usleep(50_000)
        }
        for pid in all.flatMap(members(of:)) {
            kill(pid, SIGKILL)
        }
    }

    /// Every process whose session is the one the leader opened.
    static func members(of leader: pid_t) -> [pid_t] {
        let room = proc_listallpids(nil, 0)
        guard room > 0 else { return [] }

        var pids = [pid_t](repeating: 0, count: Int(room) * 2)
        let found = proc_listallpids(&pids, Int32(pids.count * MemoryLayout<pid_t>.size))
        guard found > 0 else { return [] }

        return pids.prefix(Int(found)).filter { $0 > 0 && getsid($0) == leader }
    }
}

"""Game balance numbers from one anchor resource.

Plain Python 3.8+, no dependencies. Four commands:

    python balance.py curve --anchor-price 4 --growth 1.3 --min 3 --max 6 --steps 8
    python balance.py curve --anchor-price 4 --growth 1.3 --min 3 --max 6 --steps 8 \
        --round 5 --name Axe --spike "Old axe:3:6:20" --csv axes.csv
    python balance.py audit items.csv --values values.csv
    python balance.py audit items.csv --derive
    python balance.py budget classes.csv --tolerance 5
    python balance.py depth runs.csv --ladder random,greedy,search --floor nothing

curve: every step is `growth` times stronger than the one before. The price of
an item is the anchor it moves (its max, min or average damage) times the price
of one unit of the anchor, rounded to a multiple of --round. A spike is a
hand-made item that leaves the curve on purpose; it is placed after the named
row and priced by the same rule, so it costs what it is really worth.

audit: items.csv has a `name` column, one column per stat and an optional
`cost` column. values.csv has `stat,value` rows: what one point of the stat is
worth in the anchor. With --derive the values come from the items themselves:
the item that gives only one stat sets the price of that stat, the way League
of Legends players price stats by gold efficiency. The report gives each item
its worth, its efficiency (worth / cost), items far from the curve, items that
another item beats in every stat for the same cost or less, and items whose
worth is too close to tell apart. Only items that give the same stats are
compared for closeness: a sword and a shield of equal worth are fine.

budget: classes.csv has a `name` column and one column per stat. Each class
gets its total (stats times weights from --values, or 1 each) and its distance
from the median. The total only says which class to look at first; abilities
and play style still need a person.

depth: runs.csv has one row per game played by an agent: `variant`, `agent`,
`result` (win, loss or draw; or 1, 0, 0.5) and optional `ticks` (how long the
game lasted) and `rules` (the share of rules that fired, 0 to 1). The ladder
names the agents from weak to strong. For each variant of the game the report
gives each agent's win rate with its margin, and the depth: the smallest gap
between neighbours on the ladder, so a variant is deep only if every step up
in skill wins more (Kunanusont et al. 2017, Khalifa et al. 2017). Guards catch
the holes an optimiser finds: the floor agent (one that does nothing) wins,
nobody can lose or nobody can win, a win comes too soon, few rules fire, too
few runs, or a gap smaller than the noise.
"""

import argparse
import csv
import math
import statistics
import sys


# ---------- numbers ----------

def round_half_up(value, step=1):
    """Round to the nearest multiple of step; halves go up, like players expect."""
    if step <= 0:
        raise ValueError("step must be positive")
    return step * math.floor(value / step + 0.5)


def show(value):
    """An int when the number is whole, else two decimals."""
    return str(int(value)) if float(value).is_integer() else f"{value:.2f}"


# ---------- curve ----------

def parse_spike(text):
    """"name:after:min:max" -> dict. `after` is the 1-based row the spike follows."""
    parts = text.rsplit(":", 3)
    if len(parts) != 4:
        raise ValueError(f"spike must be name:after:min:max, got {text!r}")
    name, after, low, high = parts
    return {"name": name, "after": int(after), "min": float(low), "max": float(high)}


def basis_of(low, high, basis):
    return {"max": high, "min": low, "avg": (low + high) / 2}[basis]


def curve(anchor_price, growth, low, high, steps, price_round=1, basis="max", name="Item", spikes=()):
    """Rows of a progression curve. Each row: name, min, max, cost, spike (x times the curve or None)."""
    if steps < 1 or growth <= 0 or anchor_price <= 0 or low < 0 or high < low:
        raise ValueError("need steps >= 1, growth > 0, anchor price > 0 and 0 <= min <= max")
    rows = []
    for k in range(steps):
        lo, hi = low * growth ** k, high * growth ** k
        rows.append({
            "name": name if k == 0 else f"{name} +{k}",
            "min": round_half_up(lo),
            "max": round_half_up(hi),
            "cost": round_half_up(anchor_price * basis_of(lo, hi, basis), price_round),
            "spike": None,
            "_basis": basis_of(lo, hi, basis),
        })
    for spike in sorted(spikes, key=lambda s: s["after"], reverse=True):
        if not 1 <= spike["after"] <= len(rows):
            raise ValueError(f"spike {spike['name']!r} must follow a row from 1 to {len(rows)}")
        before = rows[spike["after"] - 1]
        own = basis_of(spike["min"], spike["max"], basis)
        rows.insert(spike["after"], {
            "name": spike["name"],
            "min": round_half_up(spike["min"]),
            "max": round_half_up(spike["max"]),
            "cost": round_half_up(anchor_price * own, price_round),
            "spike": own / before["_basis"] if before["_basis"] else None,
            "_basis": own,
        })
    for row in rows:
        del row["_basis"]
    return rows


# ---------- audit ----------

def read_table(path):
    with open(path, newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        raise ValueError(f"{path} has no rows")
    if "name" not in rows[0]:
        raise ValueError(f"{path} needs a 'name' column")
    return rows


def number(text):
    text = (text or "").strip()
    return float(text) if text else 0.0


def stat_columns(rows, skip=("name", "cost", "order", "note")):
    return [column for column in rows[0] if column not in skip]


def read_values(path):
    values = {}
    with open(path, newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            values[row["stat"].strip()] = float(row["value"])
    return values


def derive_values(rows, stats):
    """Price each stat by the cheapest single-stat item: cost / amount."""
    values = {}
    for stat in stats:
        best = None
        for row in rows:
            amount = number(row.get(stat))
            others = [s for s in stats if s != stat and number(row.get(s))]
            cost = number(row.get("cost"))
            if amount > 0 and not others and cost > 0:
                price = cost / amount
                best = price if best is None else min(best, price)
        if best is None:
            raise ValueError(f"no item gives only '{stat}' and has a cost; pass --values instead")
        values[stat] = best
    return values


def audit(rows, values, tolerance=0.15, close=0.05):
    """Worth, efficiency and warnings for each item."""
    stats = stat_columns(rows)
    missing = [s for s in stats if s not in values]
    if missing:
        raise ValueError("no value for: " + ", ".join(missing))
    has_cost = "cost" in rows[0]
    items = []
    for row in rows:
        worth = sum(number(row.get(stat)) * values[stat] for stat in stats)
        cost = number(row.get("cost")) if has_cost else None
        items.append({
            "name": row["name"],
            "stats": {stat: number(row.get(stat)) for stat in stats},
            "worth": worth,
            "cost": cost,
            "efficiency": worth / cost if cost else None,
        })

    off_curve = [i for i in items if i["efficiency"] is not None and abs(i["efficiency"] - 1) > tolerance]

    dominated = []
    for item in items:
        for other in items:
            if other is item:
                continue
            not_worse = all(other["stats"][s] >= item["stats"][s] for s in stats)
            better = any(other["stats"][s] > item["stats"][s] for s in stats)
            cheaper = has_cost and other["cost"] < item["cost"]
            not_dearer = not has_cost or other["cost"] <= item["cost"]
            if not_worse and not_dearer and (better or cheaper):
                dominated.append((item["name"], other["name"]))
                break

    ordered = sorted(items, key=lambda i: i["worth"])
    too_close = []
    for a, b in zip(ordered, ordered[1:]):
        same_line = {s for s in stats if a["stats"][s]} == {s for s in stats if b["stats"][s]}
        if same_line and b["worth"] and (b["worth"] - a["worth"]) / b["worth"] < close:
            too_close.append((a["name"], b["name"]))

    return {"values": values, "items": items, "off_curve": off_curve, "dominated": dominated, "too_close": too_close}


# ---------- budget ----------

def budget(rows, values=None, tolerance=0.05):
    """Weighted stat totals of classes and their distance from the median total."""
    stats = stat_columns(rows)
    weights = {stat: (values or {}).get(stat, 1.0) for stat in stats}
    totals = [(row["name"], sum(number(row.get(s)) * weights[s] for s in stats)) for row in rows]
    median = statistics.median(total for _, total in totals)
    result = []
    for name, total in totals:
        gap = (total - median) / median if median else 0.0
        result.append({"name": name, "total": total, "gap": gap, "outside": abs(gap) > tolerance})
    return {"median": median, "classes": result}


# ---------- depth ----------

RESULTS = {"win": 1.0, "w": 1.0, "1": 1.0, "loss": 0.0, "lose": 0.0, "l": 0.0, "0": 0.0,
           "draw": 0.5, "d": 0.5, "0.5": 0.5}


def result_of(text):
    key = (text or "").strip().lower()
    if key not in RESULTS:
        raise ValueError(f"result must be win, loss or draw (or 1, 0, 0.5), got {text!r}")
    return RESULTS[key]


def margin(rate, runs):
    """Half the width of a 95% interval around a win rate (normal approximation)."""
    return 1.96 * math.sqrt(rate * (1 - rate) / runs) if runs else 1.0


def optional(row, column):
    text = (row.get(column) or "").strip()
    return float(text) if text else None


def depth(rows, ladder, floor=None, min_runs=30, min_ticks=None, min_rules=None):
    """Win rates per agent and the depth of each variant, with the guards that failed."""
    if len(ladder) < 2:
        raise ValueError("the ladder needs at least two agents, weak to strong")
    if floor in ladder:
        raise ValueError("the floor agent stays off the ladder")
    known = set(ladder) | ({floor} if floor else set())
    variants = {}
    for row in rows:
        agent = (row.get("agent") or "").strip()
        if agent not in known:
            raise ValueError(f"agent {agent!r} is not on the ladder or the floor")
        variant = (row.get("variant") or "").strip() or "game"
        variants.setdefault(variant, {}).setdefault(agent, []).append({
            "result": result_of(row.get("result")),
            "ticks": optional(row, "ticks"),
            "rules": optional(row, "rules"),
        })

    report = []
    for variant, runs in variants.items():
        rates = {}
        for agent in [*ladder, *([floor] if floor else [])]:
            played = runs.get(agent, [])
            rate = sum(r["result"] for r in played) / len(played) if played else None
            rates[agent] = {"runs": len(played), "rate": rate,
                            "margin": margin(rate, len(played)) if rate is not None else None}

        guards = []
        missing = [a for a in ladder if not rates[a]["runs"]]
        if missing:
            guards.append("no runs for " + ", ".join(missing))
        few = [a for a in ladder if 0 < rates[a]["runs"] < min_runs]
        if few:
            guards.append(f"under {min_runs} runs: " + ", ".join(few))

        gaps = []
        for weak, strong in zip(ladder, ladder[1:]):
            if rates[weak]["rate"] is None or rates[strong]["rate"] is None:
                continue
            gap = rates[strong]["rate"] - rates[weak]["rate"]
            noise = rates[weak]["margin"] + rates[strong]["margin"]
            gaps.append({"weak": weak, "strong": strong, "gap": gap, "noise": noise})
            if gap < 0:
                guards.append(f"{weak} beats {strong}: the ladder is upside down here")
            elif gap <= noise:
                guards.append(f"{weak} and {strong} are not apart beyond the noise ({gap:.0%} vs {noise:.0%})")

        played = [r for agent in ladder for r in runs.get(agent, [])]
        if played and all(r["result"] == 1 for r in played):
            guards.append("nobody on the ladder ever loses")
        if played and all(r["result"] == 0 for r in played):
            guards.append("nobody on the ladder ever wins")
        if floor and rates[floor]["rate"]:
            guards.append(f"{floor} wins {rates[floor]['rate']:.0%} of its games")
        everyone = [r for agent_runs in runs.values() for r in agent_runs]
        if min_ticks is not None:
            quick = [r for r in everyone if r["result"] == 1 and r["ticks"] is not None and r["ticks"] < min_ticks]
            if quick:
                guards.append(f"{len(quick)} win(s) in under {show(min_ticks)} ticks")
        if min_rules is not None:
            shares = [r["rules"] for r in everyone if r["rules"] is not None]
            if shares and max(shares) < min_rules:
                guards.append(f"at most {max(shares):.0%} of the rules fire")

        report.append({
            "variant": variant,
            "rates": rates,
            "gaps": gaps,
            "depth": min(g["gap"] for g in gaps) if len(gaps) == len(ladder) - 1 else None,
            "guards": guards,
        })

    # Variants that pass every guard first, the deepest first.
    report.sort(key=lambda v: (bool(v["guards"]), -(v["depth"] if v["depth"] is not None else -2)))
    return report


# ---------- output ----------

def print_table(header, rows):
    widths = [max(len(str(x)) for x in column) for column in zip(header, *rows)]
    for line in [header, *rows]:
        print("  ".join(str(cell).ljust(width) for cell, width in zip(line, widths)).rstrip())


def write_csv(path, header, rows):
    with open(path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)


def run_curve(args):
    rows = curve(args.anchor_price, args.growth, args.min, args.max, args.steps,
                 args.round, args.basis, args.name, [parse_spike(s) for s in args.spike])
    header = ["name", "min", "max", "cost", "note"]
    table = [[r["name"], show(r["min"]), show(r["max"]), show(r["cost"]),
              f"spike, {r['spike']:.1f}x the item before" if r["spike"] else ""] for r in rows]
    print(f"anchor price {show(args.anchor_price)}, growth x{args.growth}, cost from {args.basis} damage,"
          f" rounded to {show(args.round)}")
    print_table(header, table)
    if args.csv:
        write_csv(args.csv, header, table)
        print(f"written: {args.csv}")


def run_audit(args):
    rows = read_table(args.items)
    stats = stat_columns(rows)
    values = read_values(args.values) if args.values else derive_values(rows, stats)
    report = audit(rows, values, args.tolerance / 100, args.close / 100)
    print("value of one point: " + ", ".join(f"{s} {values[s]:.2f}" for s in stats))
    header = ["name", "worth", "cost", "efficiency"]
    table = [[i["name"], f"{i['worth']:.0f}", show(i["cost"]) if i["cost"] is not None else "-",
              f"{i['efficiency']:.0%}" if i["efficiency"] is not None else "-"] for i in report["items"]]
    print_table(header, table)
    print()
    if report["off_curve"]:
        print(f"Off the curve by more than {show(args.tolerance)}%:")
        for i in report["off_curve"]:
            side = "too strong for its cost" if i["efficiency"] > 1 else "too weak for its cost"
            print(f"  {i['name']}: {i['efficiency']:.0%}, {side}")
    for name, by in report["dominated"]:
        print(f"Dead item: {name}. {by} is at least as good in every stat and costs no more.")
    for a, b in report["too_close"]:
        print(f"Too close to tell apart (under {show(args.close)}%): {a} and {b}.")
    if not (report["off_curve"] or report["dominated"] or report["too_close"]):
        print("No item is off the curve, dead or too close to another.")
    if args.csv:
        write_csv(args.csv, header, table)
        print(f"written: {args.csv}")


def run_budget(args):
    rows = read_table(args.classes)
    values = read_values(args.values) if args.values else None
    report = budget(rows, values, args.tolerance / 100)
    header = ["name", "total", "vs median"]
    table = [[c["name"], show(round(c["total"], 2)), f"{c['gap']:+.1%}" + ("  <- look here" if c["outside"] else "")]
             for c in report["classes"]]
    print(f"median total {show(round(report['median'], 2))}, tolerance {show(args.tolerance)}%")
    print_table(header, table)


def read_runs(path):
    """runs.csv must have the columns agent and result; variant, ticks and rules are optional."""
    with open(path, newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        raise ValueError(f"{path} has no rows")
    missing = [c for c in ("agent", "result") if c not in rows[0]]
    if missing:
        raise ValueError(f"{path} needs the column(s): " + ", ".join(missing))
    return rows


def run_depth(args):
    rows = read_runs(args.runs)
    ladder = [a.strip() for a in args.ladder.split(",") if a.strip()]
    report = depth(rows, ladder, args.floor, args.min_runs, args.min_ticks,
                   args.min_rules / 100 if args.min_rules is not None else None)
    agents = ladder + ([args.floor] if args.floor else [])
    header = ["variant", *agents, "depth"]
    table = []
    for v in report:
        cells = [v["variant"]]
        for agent in agents:
            r = v["rates"][agent]
            cells.append("-" if r["rate"] is None else f"{r['rate']:.0%} +-{r['margin']:.0%} ({r['runs']})")
        cells.append("-" if v["depth"] is None else f"{v['depth']:.0%}")
        table.append(cells)
    print(f"ladder {' < '.join(ladder)}; depth is the smallest gap between neighbours")
    print_table(header, table)
    print()
    for v in report:
        if v["guards"]:
            print(f"{v['variant']}: " + "; ".join(v["guards"]) + ".")
    passing = [v for v in report if not v["guards"] and v["depth"] is not None]
    if passing:
        print(f"Deepest variant that passes every guard: {passing[0]['variant']}, depth {passing[0]['depth']:.0%}.")
    else:
        print("No variant passes every guard.")
    if args.csv:
        write_csv(args.csv, header, table)
        print(f"written: {args.csv}")


def main(argv):
    parser = argparse.ArgumentParser(prog="balance.py", description="Game balance numbers from one anchor.")
    commands = parser.add_subparsers(dest="command", required=True)

    c = commands.add_parser("curve", help="a progression curve priced in the anchor")
    c.add_argument("--anchor-price", type=float, required=True, help="price of one unit of the anchor")
    c.add_argument("--growth", type=float, required=True, help="each step is this many times stronger, e.g. 1.25")
    c.add_argument("--min", type=float, required=True, help="low end of the first item's range")
    c.add_argument("--max", type=float, required=True, help="high end of the first item's range")
    c.add_argument("--steps", type=int, required=True, help="how many items on the curve")
    c.add_argument("--round", type=float, default=1, help="round prices to a multiple of this")
    c.add_argument("--basis", choices=["max", "min", "avg"], default="max", help="which damage sets the price")
    c.add_argument("--name", default="Item", help="name of the first item; the next are +1, +2 ...")
    c.add_argument("--spike", action="append", default=[], help="name:after:min:max, a hand-made item")
    c.add_argument("--csv", help="also write the table to this CSV file")
    c.set_defaults(run=run_curve)

    a = commands.add_parser("audit", help="price items in the anchor and find the odd ones")
    a.add_argument("items", help="CSV: name, one column per stat, optional cost")
    source = a.add_mutually_exclusive_group(required=True)
    source.add_argument("--values", help="CSV: stat,value - one point of the stat in the anchor")
    source.add_argument("--derive", action="store_true", help="price stats from single-stat items")
    a.add_argument("--tolerance", type=float, default=15, help="percent off the curve before a warning")
    a.add_argument("--close", type=float, default=5, help="percent gap under which two items look the same")
    a.add_argument("--csv", help="also write the table to this CSV file")
    a.set_defaults(run=run_audit)

    b = commands.add_parser("budget", help="compare stat totals of classes")
    b.add_argument("classes", help="CSV: name, one column per stat")
    b.add_argument("--values", help="CSV: stat,value - weights; 1 each when missing")
    b.add_argument("--tolerance", type=float, default=5, help="percent from the median before a warning")
    b.set_defaults(run=run_budget)

    d = commands.add_parser("depth", help="win rates of a ladder of agents and the depth of each variant")
    d.add_argument("runs", help="CSV: variant, agent, result (win/loss/draw), optional ticks and rules")
    d.add_argument("--ladder", required=True, help="agents from weak to strong, comma separated")
    d.add_argument("--floor", help="an agent that does nothing; it must never win")
    d.add_argument("--min-runs", type=int, default=30, help="fewer runs per agent than this is a warning")
    d.add_argument("--min-ticks", type=float, help="a win shorter than this many ticks is a warning")
    d.add_argument("--min-rules", type=float, help="percent of rules that must fire in at least one run")
    d.add_argument("--csv", help="also write the table to this CSV file")
    d.set_defaults(run=run_depth)

    try:
        args = parser.parse_args(argv[1:])
    except SystemExit as stop:
        return stop.code if isinstance(stop.code, int) else 2
    try:
        args.run(args)
    except (ValueError, OSError, KeyError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

"""Tests for balance.py. Run from this folder: python -m unittest"""

import contextlib
import csv
import io
import os
import tempfile
import unittest

import balance


def table(text):
    return list(csv.DictReader(io.StringIO(text)))


ITEMS = table(
    "name,attack,armor,hp,cost\n"
    "Dagger,10,0,0,300\n"
    "Cloth,0,15,0,300\n"
    "Ruby,0,0,150,400\n"
    "Plate,0,30,100,800\n"
    "Cheap blade,20,0,150,700\n"
    "Blade,20,0,150,1100\n"
    "Rusty,5,0,0,300\n"
)


class RoundingTests(unittest.TestCase):
    def test_halves_go_up(self):
        self.assertEqual(3, balance.round_half_up(2.5))
        self.assertEqual(25, balance.round_half_up(22.5, 5))
        self.assertEqual(20, balance.round_half_up(22.4, 5))


class CurveTests(unittest.TestCase):
    def test_each_step_grows_and_price_follows_max_damage(self):
        rows = balance.curve(anchor_price=4, growth=1.3, low=3, high=6, steps=3)
        self.assertEqual([(3, 6), (4, 8), (5, 10)], [(r["min"], r["max"]) for r in rows])
        # 6 * 1.3 = 7.8 -> 31.2, 6 * 1.69 = 10.14 -> 40.56
        self.assertEqual([24, 31, 41], [r["cost"] for r in rows])
        self.assertEqual(["Item", "Item +1", "Item +2"], [r["name"] for r in rows])

    def test_prices_round_to_a_step(self):
        rows = balance.curve(4, 1.3, 3, 6, 3, price_round=5)
        self.assertEqual([25, 30, 40], [r["cost"] for r in rows])

    def test_a_spike_sits_after_its_row_and_says_how_far_it_jumps(self):
        spike = balance.parse_spike("Old axe:2:6:20")
        rows = balance.curve(4, 1.3, 3, 6, 3, spikes=[spike])
        self.assertEqual("Old axe", rows[2]["name"])
        self.assertEqual(80, rows[2]["cost"])
        self.assertAlmostEqual(20 / 7.8, rows[2]["spike"], places=6)
        self.assertIsNone(rows[1]["spike"])

    def test_a_spike_name_may_hold_a_colon(self):
        self.assertEqual("Axe: old", balance.parse_spike("Axe: old:1:2:3")["name"])

    def test_bad_input_is_refused(self):
        with self.assertRaises(ValueError):
            balance.curve(4, 1.3, 6, 3, 3)
        with self.assertRaises(ValueError):
            balance.curve(4, 1.3, 3, 6, 3, spikes=[balance.parse_spike("X:9:1:2")])
        with self.assertRaises(ValueError):
            balance.parse_spike("X:1:2")


class AuditTests(unittest.TestCase):
    def setUp(self):
        stats = balance.stat_columns(ITEMS)
        self.values = balance.derive_values(ITEMS, stats)
        self.report = balance.audit(ITEMS, self.values)

    def test_single_stat_items_set_the_price_of_a_stat(self):
        self.assertAlmostEqual(30, self.values["attack"])
        self.assertAlmostEqual(20, self.values["armor"])
        self.assertAlmostEqual(400 / 150, self.values["hp"])

    def test_efficiency_is_worth_over_cost(self):
        plate = next(i for i in self.report["items"] if i["name"] == "Plate")
        self.assertAlmostEqual((600 + 100 * 400 / 150) / 800, plate["efficiency"])

    def test_items_far_from_the_curve_are_named(self):
        self.assertEqual({"Cheap blade", "Rusty"}, {i["name"] for i in self.report["off_curve"]})

    def test_an_item_beaten_in_everything_is_dead(self):
        dead = dict(self.report["dominated"])
        self.assertEqual("Cheap blade", dead["Blade"])
        self.assertEqual("Dagger", dead["Rusty"])
        self.assertNotIn("Cloth", dead)

    def test_only_items_of_the_same_kind_are_too_close(self):
        self.assertEqual([("Cheap blade", "Blade")], self.report["too_close"])

    def test_a_stat_without_a_value_is_an_error(self):
        with self.assertRaises(ValueError):
            balance.audit(ITEMS, {"attack": 1})

    def test_derive_needs_a_single_stat_item(self):
        rows = table("name,attack,armor,cost\nMix,1,1,10\n")
        with self.assertRaises(ValueError):
            balance.derive_values(rows, ["attack", "armor"])


class BudgetTests(unittest.TestCase):
    def test_a_class_far_from_the_median_is_marked(self):
        rows = table("name,str,int\nKnight,9,3\nMage,3,7\nRogue,6,6\n")
        report = balance.budget(rows)
        self.assertEqual(12, report["median"])
        marked = {c["name"] for c in report["classes"] if c["outside"]}
        self.assertEqual({"Mage"}, marked)

    def test_weights_change_the_total(self):
        rows = table("name,str,int\nKnight,9,3\n")
        report = balance.budget(rows, {"str": 2})
        self.assertEqual(21, report["classes"][0]["total"])


class CommandLineTests(unittest.TestCase):
    def run_main(self, *args):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
            code = balance.main(["balance.py", *args])
        return code, out.getvalue()

    def test_curve_writes_a_csv(self):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "axes.csv")
            code, _ = self.run_main("curve", "--anchor-price", "4", "--growth", "1.3", "--min", "3",
                                    "--max", "6", "--steps", "2", "--csv", path)
            self.assertEqual(0, code)
            with open(path, encoding="utf-8") as handle:
                self.assertEqual("name,min,max,cost,note", handle.readline().strip())

    def test_audit_reads_files(self):
        with tempfile.TemporaryDirectory() as folder:
            items = os.path.join(folder, "items.csv")
            values = os.path.join(folder, "values.csv")
            with open(items, "w", encoding="utf-8") as handle:
                handle.write("name,attack,cost\nA,10,100\nB,10,200\n")
            with open(values, "w", encoding="utf-8") as handle:
                handle.write("stat,value\nattack,10\n")
            code, out = self.run_main("audit", items, "--values", values)
            self.assertEqual(0, code)
            self.assertIn("B: 50%, too weak for its cost", out)
            self.assertIn("Dead item: B.", out)

    def test_bad_arguments_exit_with_2(self):
        self.assertEqual(2, self.run_main("curve", "--growth", "1.3")[0])
        self.assertEqual(2, self.run_main("audit", "no-such-file.csv", "--derive")[0])


if __name__ == "__main__":
    unittest.main()

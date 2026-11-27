using System;
using System.Collections.Generic;
using System.Linq;

namespace Deterrence
{
    public class MeterNode
    {
        public string Key, Label, State, Change, Tip;
        public double Credit;
        public int TargetId = -1;
        public List<MeterNode> Children = new List<MeterNode>();
    }

    public class MeterRow
    {
        public MeterNode Node;
        public int Depth;
    }

    public static class MeterTree
    {
        private static string Money(double amount) => CreditMath.Positive(amount).ToString("N0");
        private static string Left(LedgerValues entry, double now)
            => Math.Max(0, entry.EndDay + entry.DecayDuration - now).ToString("0.0") + " days left";

        private static string State(MeterGroup group)
            => group.Present > 0 && group.Fading > 0 ? "Mixed" : group.Present > 0
                ? group.Kind == MeterKind.HostileRemains ? "Present" : "Active" : group.Fading > 0 ? "Fading" : "None";

        private static string Change(MeterGroup group, double now)
        {
            double gain = group.Contributors.Where(e => e.Active).Sum(e => CreditMath.Positive(e.DailyGain));
            if (gain > 0) return "+" + Money(gain) + "/day" + (group.Fading > 0 ? "; " + group.Fading + " fading" : "");
            if (group.Fading > 0) return group.Contributors.Count == 1 ? Left(group.Contributors[0], now) : group.Fading + " fading";
            return group.Present > 0 ? "Kept" : "";
        }

        private static MeterNode EntryNode(LedgerValues entry, double now)
            => new MeterNode { Key = entry.Key, Label = (entry.Label ?? "Source").Replace(" (remains)", ""), Credit = CreditMath.Positive(entry.Balance),
                TargetId = entry.Active ? entry.CorpseId : -1,
                State = !entry.Active ? "Fading" : entry.Buried ? "Buried" : entry.CorpseId >= 0 || entry.SourceKey == "organs:local" ? "Present" : "Active",
                Change = !entry.Active ? Left(entry, now) : entry.DailyGain > 0 ? "+" + Money(entry.DailyGain) + "/day" : "Kept",
                Tip = entry.CorpseId >= 0 ? "Combat power: " + entry.CombatPower.ToString("0") + "."
                    + (entry.Active && entry.RawCredit > 0 ? " Worth " + Money(entry.RawCredit) + " before the remains curve; counts as " + Money(entry.Balance) + "." : "")
                    + (entry.Buried ? " Buried bodies keep full deterrence." : "")
                    : !entry.Active ? "This contribution fades independently." : "Current contribution from this source." };

        // Compare the kept bodies' value before the remains curve with what counts.
        private static string CurveNote(IEnumerable<LedgerValues> entries)
        {
            var kept = entries.Where(e => e.Active && e.CorpseId >= 0 && e.RawCredit > 0).ToList();
            if (kept.Count == 0) return "";
            double raw = kept.Sum(e => e.RawCredit), counted = kept.Sum(e => CreditMath.Positive(e.Balance));
            return " Kept bodies are worth " + Money(raw) + " before the remains curve and count as " + Money(counted)
                + " (" + (counted / raw).ToString("P0") + ").";
        }

        public static List<MeterNode> Build(IEnumerable<MeterGroup> groups, IEnumerable<OrganStockValues> organStock,
            double now, Func<LedgerValues, IEnumerable<ShareValues>> shares = null)
        {
            var result = new List<MeterNode>();
            var stock = organStock.ToList();
            foreach (var group in groups)
            {
                var root = new MeterNode { Key = group.Key, Label = group.Label, Credit = group.Balance,
                    State = State(group), Change = Change(group, now) };
                if (group.Kind == MeterKind.HostileRemains)
                {
                    root.Tip = "Bodies and locally harvested prisoner organs. Expand for individual contributions." + CurveNote(group.Contributors);
                    foreach (string kind in new[] { "human", "mech", "animal", "other", "organs" })
                    {
                        var entries = group.Contributors.Where(e => MeterGrouping.BodyKind(e) == kind).ToList();
                        if (entries.Count == 0) continue;
                        string label = kind == "human" ? "Human bodies" : kind == "mech" ? "Mechanoid remains"
                            : kind == "animal" ? "Animal remains" : kind == "organs" ? "Prisoner organs" : "Other bodies";
                        var bucket = new MeterGroup { Kind = MeterKind.HostileRemains, Contributors = entries };
                        var child = new MeterNode { Key = root.Key + ":" + kind, Label = label, Credit = bucket.Balance,
                            State = State(bucket), Change = Change(bucket, now), Tip = kind == "organs"
                                ? "Kept organ value times your multiplier. Stock deterrence ends when the organs leave."
                                : "Includes buried bodies and any remains already fading." + CurveNote(entries) };
                        if (kind == "organs" && stock.Count > 0)
                            foreach (var item in stock.OrderBy(s => s.Label).ThenBy(s => s.Key))
                                child.Children.Add(new MeterNode { Key = item.Key, Label = item.Label, Credit = item.Credit, TargetId = item.TargetId, State = "Present", Change = "Kept",
                                    Tip = "Market value: " + Money(item.MarketValue) + ". Quantity: " + item.Count + "." });
                        else foreach (var entry in entries.OrderByDescending(e => e.Active).ThenBy(e => e.Label).ThenBy(e => e.Key))
                            child.Children.Add(EntryNode(entry, now));
                        root.Children.Add(child);
                    }
                }
                else if (group.Kind == MeterKind.Violence)
                {
                    root.Tip = "One shared fading reputation. Expand to see each act's remaining share.";
                    foreach (var entry in group.Contributors)
                    {
                        var parts = (shares?.Invoke(entry) ?? Enumerable.Empty<ShareValues>()).Where(s => s != null && s.Amount > 0).ToList();
                        double weights = parts.Sum(s => s.Amount);
                        if (weights <= 0)
                            root.Children.Add(new MeterNode { Key = entry.Key + ":earlier", Label = "Earlier reputation", Credit = entry.Balance,
                                State = "Fading", Change = "Shared fade", Tip = "Individual records are unavailable." });
                        else foreach (var part in parts.AsEnumerable().Reverse())
                            root.Children.Add(new MeterNode { Key = entry.Key + ":" + part.Key, Label = part.Label,
                                Credit = ShareMath.Current(part.Amount, weights, entry.Balance), State = "Fading", Change = "Shared fade",
                                Tip = part.Key == "earlier" ? "Combined share of older acts." : "Game day " + part.Day.ToString("0.0") + ". Fades with the shared reputation meter." });
                    }
                }
                else
                {
                    root.Tip = "Expand for active and fading contributions.";
                    foreach (var entry in group.Contributors.OrderByDescending(e => e.Active).ThenBy(e => e.Key)) root.Children.Add(EntryNode(entry, now));
                }
                result.Add(root);
            }
            var hazards = groups.Where(g => g.Kind == MeterKind.Environment).ToList();
            var hazardEntries = hazards.SelectMany(g => g.Contributors).ToList();
            var summary = new MeterGroup { Kind = MeterKind.Environment, Contributors = hazardEntries };
            var hazardRoot = new MeterNode { Key = "hazards", Label = "Hazards",
                Credit = summary.Balance, State = State(summary), Change = Change(summary, now),
                Tip = "Ongoing environmental danger and its fading effects." };
            hazardRoot.Children.AddRange(result.Where(n => n.Key.StartsWith("environment:", StringComparison.Ordinal)));
            result.RemoveAll(n => n.Key.StartsWith("environment:", StringComparison.Ordinal));
            result.Add(hazardRoot);
            return result;
        }

        public static List<MeterRow> Visible(IEnumerable<MeterNode> roots, ISet<string> expanded)
        {
            var result = new List<MeterRow>();
            foreach (var root in roots) Append(root, 0, expanded, result);
            return result;
        }

        private static void Append(MeterNode node, int depth, ISet<string> expanded, List<MeterRow> result)
        {
            result.Add(new MeterRow { Node = node, Depth = depth });
            if (expanded.Contains(node.Key)) foreach (var child in node.Children) Append(child, depth + 1, expanded, result);
        }

        public static IEnumerable<MeterNode> All(IEnumerable<MeterNode> roots)
        {
            foreach (var root in roots)
            {
                yield return root;
                foreach (var child in All(root.Children)) yield return child;
            }
        }
    }
}

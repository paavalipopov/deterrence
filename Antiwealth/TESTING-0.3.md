# Antiwealth 0.3 in-game checklist

Use a separate test save. Close RimWorld before running the updater; restart afterward. Enable Development mode. Use actual simulation (speed 4) rather than jumping the clock. Allow one game hour for normal refreshes, or use Apply & save.

| Done | Check | Expected |
| --- | --- | --- |
| [ ] | Startup and old save | No new load/patch errors; earlier balances and recovery survive. |
| [ ] | Combat power help | Explains fixed pawn-kind rating versus individual gear/value. |
| [ ] | Human direct death | Kill an armed hostile directly. Tooltip records weapons and worn apparel. |
| [ ] | Downed human | Down an armed hostile, strip it, then kill it. Original loadout is retained in the snapshot. |
| [ ] | Save a downed enemy | Save/reload before killing it; original equipment snapshot survives. |
| [ ] | Strip the corpse | Stripping/selling gear does not change the frozen equipment contribution. |
| [ ] | Gear fraction | 0% gives only base combat credit; 100% adds all recorded gear value before population/rot factors. |
| [ ] | No duplicate corpses | Moving bodies between home maps or resurrecting/killing the same pawn does not stack entries. |
| [ ] | Rot default | Fresh, rotting, and dessicated bodies retain equal credit with weighting off. |
| [ ] | Ground pollution | Pollute (rect) creates a separate balance. Cleanup lowers gains; complete cleanup begins recovery. |
| [ ] | Hostile hives | Visible hostile hives accumulate credit. Removing all hives begins recovery. |
| [ ] | Volcanic winter | Absent from source settings; an old balance enters recovery. |
| [ ] | Heat waves/cold snaps | Named events still work. Optional ambient temperature can stack with them. |
| [ ] | Ambient temperature | Outdoor excess changes the rate; indoor heating/cooling alone does not. |
| [ ] | Terrain rules | Poor and swamp fractions are distinct. Placing floors does not create poor soil. |
| [ ] | Biome multiplier | Formula shows weight and multiplier. Exponent 0 gives no reduction; 0.5 gives square root. |
| [ ] | Adjustment order | Effective wealth matches max(0, incoming threat wealth - credit) times biome multiplier. |
| [ ] | Illness default | Off by default; enabling restores per-disease credit. Explicit saved choices remain readable. |
| [ ] | Help | Hover and click ? buttons; explanations match current units. |
| [ ] | Preview curves | Change population, duration, exposure, gear, and recovery settings; graph updates and ends at zero. |
| [ ] | Wealth history | After half a day, all four curves have matching timestamps; no earlier credit is fabricated. |
| [ ] | History save/load | Save/reload and switch maps; each retains its history and original timestamps. |

For threat comparisons, pause under Cassandra and use Debug output > Current threat points. Minimum threat points can hide small changes. Manually forcing a raid-point budget bypasses normal scaling; use it to create test enemies only.

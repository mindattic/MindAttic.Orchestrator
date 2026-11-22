# Work units: eat minnows, not whales (M.ORCH-T1901)

The owner, 2026-11-21: "Tickets are nodes within nodes ... I've noticed subagents accumulating to like 200-500 thousand
tokens before finishing ... that's a failure to plan; it should be nodes for each unit of work so that you can build up a
feature with lego like pieces ... stop biting off whale sized tasks and instead eat minnows until you have a whale's weight
of work completed."

## The evidence

`orchestrator.db` `ticket_costs`, 605 ticketed subagent runs (uncached tokens each): median **158k**, p75 **347k**,
p90 **690k**, p95 **1.06M**; 253 runs over 200k. One subagent per whole ticket, however big the ticket, is what produced
those numbers.

## The tree

Tickets are already nodes in a tree: a `part-of` link (M.TIX-T314) makes one ticket the child of another, to any depth.
Each node carries one **level** label, in standard agile terms:

| Level | Label | What it is | Worked? |
|---|---|---|---|
| Epic | `level:epic` (and `epic`) | A large goal spanning features, often repos | Never: it closes when its children are done |
| Feature | `level:feature` | A shippable capability, made of stories | Never |
| Story | `level:story` | One user-visible behaviour, made of tasks | Never, once it has children |
| Task | `level:task` | **The unit of work**: one subagent, one commit, one budget | Yes, the only thing dispatched |

Rules:

1. **Only leaves are work.** A ticket with an open `part-of` child is a *container*. The Orchestrator never hands one to
   a subagent, whatever its label (M.ORCH-T1904, generalising M.ORCH-T1146's epic rule).
2. **A task fits a budget.** `UnitTokenBudget`, by default 80k uncached tokens, is a setting (M.ORCH-T1905). A task names
   its area, ideally its files, and one outcome a build can check.
3. **Plan before working.** A leaf that isn't `level:task` yet is *unplanned*. Dispatching it starts a **plan pass**: read
   the ticket and only the code needed to size it. If it fits one budget, mark it `level:task` and do it. If not, run
   `tickets ticket split` into tasks and stop. The plan pass costs little; the tasks are dispatched as they come up
   (M.ORCH-T1906).
4. **Land, then split, when a task runs long.** The context gate watches each ticketed subagent's own tokens. Past the
   budget, it is told to commit and land what is complete, split what's left into child tasks, close or leave its ticket
   accordingly, and stop. At 2× the budget it is told to stop at once (M.ORCH-T1907). No half-million tokens sit unlanded.
5. **Roll up.** Closing a parent's last open child closes the parent, all the way up (M.TIX-T364). Features and epics
   finish because their minnows did.
6. **New work is filed as tasks.** A request that is bigger than one task is filed as a story (or feature) with its tasks
   under it, using `tickets ticket split` (MAT-T171).

## Commands (MindAttic.Tickets)

```
tickets ticket level <ID> epic|feature|story|task       # the one level:<x> label
tickets ticket new ... --parent <ID> [--level task]        # child, part-of the parent, approved like it
tickets ticket split <ID> --task "..." --task "..." [--sequential]   # or --file plan.txt, one task per line
tickets ticket list --unplanned                            # open leaves not yet level:task
tickets ticket list --containers                           # open tickets with open children
```

## Backfill (MAT-T173)

1. Mechanical (MAT-T174): label every open ticket's level from the tree that exists. `epic` label → epic; an open ticket
   with open children → feature if its parent is an epic, else story; leaves are left for step 2.
2. Sizing (MAT-T175): every open leaf, in batches of about 20 per agent (each agent is itself a minnow): mark it
   `level:task` when one unit fits, else split it into tasks.

Anything the backfill misses is caught by rule 3 when it is dispatched.

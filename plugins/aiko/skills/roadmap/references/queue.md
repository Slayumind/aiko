# The queue to the scarce resource

Priorities matter where domains compete for the same scarce resource: a team that serves everyone
(IT, design, data), a budget, one expert, a test bench. Most releases of a domain are done by the
domain itself; only the asks to the scarce resource compete. So the owner of the shared backlog
orders those asks, not the releases.

## What goes in

Every open ask from every domain to that resource, in one list:

- rows of the shared backlog marked as work of the resource;
- tasks the resource already runs in its own tracker for these domains;
- asks that live only in chats (a bug report, a DNS request, a booking);
- rows that clearly need the resource but have no "who does it" mark: in the list, with a visible
  "not marked" badge, so their owner confirms.

Each item: title as in the source, domain, who asks, status (in progress, not started, to approve,
paused), the source's own priority, due date, the release it holds, the goal, a link. A paused ask
carries the reason and who paused it. Each source carries its own snapshot date; the page shows it.

## The starting order

Three questions, in this order, and no weighted formula:

1. Does the ask hold a release that is already in work?
2. What priority does its source give it?
3. When is it due?

Asks waiting for approval and paused asks go last. The order is a proposal; the owner of the
shared backlog changes it.

## Priority marks

When the user asks for "a standard prioritisation", pick one, say in one sentence why it fits what
is already there, and keep the choice easy to undo. MoSCoW worked well: it is a mark, not a
formula, and a capacity line can later read it.

**Must** (the cycle fails without it), **Should** (important, there is a workaround), **Could**
(if there is room), **Won't** (not this time). The starting mark comes from the source's own
priority (for example P0 → Must, P1 → Should, P2 → Could, P3 → Won't). On the item it is the same
plain mark as one the owner set: no "from P1" note, so the page works the same for any source. The
mapping is written once, in the page's explanation. Offer a filter by mark and a "sort by priority"
action that keeps the order inside each mark.

## Capacity and the line

A line at the resource's capacity ("what fits in the next sprint") is a strong mechanic, but it
depends on how the resource plans its work. Do not ship it until the group agrees how the line
follows the resource's sprint or budget. Until then keep it switched off and show only the order
and the marks.

When it is agreed: the line is a number of places, so moving an ask across it does not move it; the
meeting discusses the line and what crosses it; a Must below the line and a Won't above it are
highlighted as questions; each domain owner names their top three asks before the meeting; a
conflict between domains goes to the person with the veto. The resource gives each ask a size: S, M
or L.

## Where it lives

As its own part of the roadmap page. The agent ships the items and the starting order with the page
data. The owner's order, marks and sizes are kept in a store only the owner can write, so a weekly
rebuild of the items never overwrites them: new items get a "new" badge, finished ones drop out.

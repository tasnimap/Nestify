# Nestify — Initial Project Distribution (Frontend Phase)

**Four contributors · four modules · `src/Nestify.Web` only.**

| | |
|---|---|
| **Companion document** | `Implementation_Plan.md` — every §-reference below points into it |
| **Phase scope** | **Frontend only.** Blazor WebAssembly pages, components and client services. No API, no EF Core, no migrations in this phase |
| **Tier** | Frontend tier of the three-tier split (§1.1) |
| **Backend status** | Greenfield — **no endpoint exists yet** (§0.2). Every page in this phase is built against a **mock service**, see §3 |

---

## 1 — Module ownership

| # | Owner | Module | Plan section | API contract | Route prefix |
|---|---|---|---|---|---|
| 1 | **Shreoshi** | **M1 · Housing & seat listings** | §5 | §3.6, §3.7 | `/housing`, `/bookings` |
| 2 | **Prapty** | **M2 · Domestic help (Khala/Bua) directory** | §6 | §3.8, §3.9 | `/helpers`, `/engagements` |
| 3 | **Obonti** | **M3 · Shared expense & meal settlement** | §7 | §3.5, §3.10 | `/houses` |
| 4 | **Ishmam** | **M4 · Second-hand marketplace** | §8 | §3.11 | `/marketplace`, `/buy-interests` |

**Not assigned in this phase:** M5 verification and M6 admin (§9), and the ML price suggestion (§10, owner: **Ishmam**, backend phase). These are picked up after the four module frontends are standing.

**Why one module per person and not one layer per person:** a "you do pages, I do services" split makes every feature need two people and two PRs. One person owning a module end to end inside the frontend means each PR is independently reviewable and independently demo-able.

---

## 2 — Shared foundation (built first, one owner each)

These are used by more than one module, so each has exactly **one** owner. Everyone else consumes them and asks the owner instead of editing them.

| Component / area | Owner | Lives in | Consumed by |
|---|---|---|---|
| **App shell** — `MainLayout`, `NavMenu`, `@page "/"` home, route table, 404 page | **Prapty** | `Layout/`, `Pages/Home.razor` | Everyone |
| **`AreaCascade`** — division → district → upazila dropdowns (§5.5) | **Shreoshi** | `Components/AreaCascade.razor` | M1, M2, M4 |
| **`ContactDisclosureCard`** — renders `ContactDisclosureDto` after a disclosure transition (§11.4) | **Shreoshi** | `Components/` | M1, M2, M4 |
| **UI kit** — CSS tokens, buttons, cards, badges, `Pagination`, `EmptyState`, `Spinner`, `ConfirmDialog` | **Ishmam** | `Components/Ui/`, `wwwroot/css/` | Everyone |
| **`ImageUploader` + `ImageGallery`** (§8.5) | **Ishmam** | `Components/` | M4, later M5 |
| **`ReportDialog`** — shared report flow (§3.13) | **Ishmam** | `Components/` | M1, M2, M4 |
| **Form primitives + formatters** — validated text/number/select fields, `৳` money formatting, Asia/Dhaka date display (§0.3) | **Obonti** | `Components/Forms/`, `Utils/` | Everyone |
| **`RatingStars`** — read + input | **Prapty** | `Components/` | M2, later M4 |
| **Toast + notification bell** — polling stub for now (§1.5) | **Obonti** | `Components/`, `Services/NotificationService.cs` | Everyone |

**Rule:** the `AreaCascade` is built **once**. §5.5 says so explicitly — four independent cascades are four homes for the same "clear the child selection" bug.

---

## 3 — How to build a page with no backend

The API does not exist yet, so every module follows the same three-file pattern. This is the single most important convention of this phase.

```
Nestify.Web/Services/
├── IHousingService.cs        ← interface: the contract your pages depend on
├── MockHousingService.cs     ← in-memory fixtures — THIS phase
└── HousingService.cs         ← HttpClient implementation — NEXT phase (stub for now)
```

1. **Define the interface** against the endpoints in §3.x of the plan — same operations, same DTOs, same return shapes.
2. **Write the mock**, returning hardcoded data that includes the awkward cases: empty list, one item, a full page, a not-found, a conflict.
3. **Pages depend only on the interface**, injected through DI in `Program.cs`.
4. When the API lands, one line in `Program.cs` swaps `Mock…Service` for the real one and **no page changes**.

**DTOs go in `Nestify.Shared`** (§1.1) — DTOs and enums only, no EF types. **Each person creates their own DTO file**; nobody edits someone else's. `Nestify.Shared` is the highest-conflict folder in the repo, and one file per module keeps merges trivial.

---

## 4 — Shreoshi · M1 Housing & seat listings

**Reference:** §5 · §3.6 · §3.7 · §5.7

### Pages

| Route | Page | What it does |
|---|---|---|
| `/housing` | Browse | Post list + filter panel: `AreaCascade`, listing type (seat / seats / whole house), max rent, paging. Card shows area, rent, seats, listing type |
| `/housing/{id}` | Detail | Full post, eligibility requirements as chips, **Book** button, **Report** button. A not-found renders a plain "This post is not available" page — **not** "you are not eligible" |
| `/housing/new` | Create post | House selector (from `houses/mine`), listing type, seat count, rent, description, **eligibility requirement builder** |
| `/housing/{id}/edit` | Edit post | Same form, owner-only. **No house field** — a post cannot be reparented |
| `/housing/mine` | My posts | Owner's posts including closed ones, with close / reopen / delete |
| `/housing/{id}/bookings` | Requester list | Manager view: who requested, when, status, accept / reject. **Contact block renders only on `Accepted` rows** |
| `/bookings/mine` | My bookings | Seeker view: status per request, withdraw, contact once accepted |

### Also owns
`AreaCascade` and `ContactDisclosureCard` (§2), plus the post/booking status badge vocabulary that M4 will mirror.

### What must be right on your pages
- **Eligibility is invisible in the UI.** You never render "you don't match this post" — the list simply does not contain it, and the detail page returns not-found (§5.3). Do not build a client-side eligibility filter.
- **Two state machines** — post `Active`/`Closed`, and booking `Pending`/`Accepted`/`Rejected`/`Withdrawn` (§5.2). Every button must be disabled in the states where its transition is illegal.
- **`Pending → Accepted` is the disclosure transition.** No contact markup exists anywhere on the requester list outside the accepted branch — before it, the DTO has no contact property at all (§11.4.3).
- Post description and requirement text render as **plain text**, never `MarkupString` (§11.5.2).

---

## 5 — Prapty · M2 Domestic help directory

**Reference:** §6 · §3.8 · §3.9 · §6.6

### Pages

| Route | Page | What it does |
|---|---|---|
| `/helpers` | Browse | Filters: `AreaCascade`, service type, max monthly rate, minimum rating; sort by rating / rate / distance. Card shows name, services, rate, `RatingStars`, **area name and a coarse distance band** |
| `/helpers/{id}` | Helper detail | Services, availability window, monthly rate, rating summary, paged review list, **Request engagement** button |
| `/helpers/register` | Become a helper | Services multi-select, availability window, monthly rate, `AreaCascade`, location capture — map pin or manual entry (D-10) |
| `/helpers/me` | Edit my profile | Same form, and **no id in the route at all** |
| `/engagements/mine` | My engagements | Both roles in one list — as client and as helper. Per-row actions driven by the state machine |
| — | Review form (modal on a completed engagement) | `RatingStars` input + comment. **Rating and comment only** — there is no helper selector |

### Also owns
The **app shell** (`MainLayout`, `NavMenu`, home page, route table, 404) and `RatingStars` (§2). `NavMenu` currently links to `counter` and `weather`, which do not exist and make login land on a 404 — fixing that is your first task and it unblocks everyone.

### What must be right on your pages
- **Never render raw coordinates.** List and detail show the upazila name and a band ("within 2 km"). Exact location appears only after helper confirmation (§6.6). This is a physical-safety requirement, not a preference.
- **Five states, two-sided completion** (§6.3). `Requested → HelperConfirmed` is helper-only; `Active → Completed` requires **both** parties, so "you marked complete · waiting for the other side" is a real UI state you must render.
- **The review button appears only on `Completed` engagements**, and never on an engagement with your own helper profile. Your checks are cosmetic — the server runs all five (§6.4) — but the UI must not offer a button that is guaranteed to fail.
- Review text renders as plain text (§11.5.2).

---

## 6 — Obonti · M3 Shared expense & meal settlement

**Reference:** §7 · §3.5 · §3.10 · §7.7 — **the heaviest UI in the project.**

### Pages

| Route | Page | What it does |
|---|---|---|
| `/houses/mine` | My houses | Houses the user belongs to, with their role in each |
| `/houses/new` | Create house | Name, address, `AreaCascade`. The creator becomes Manager |
| `/houses/{id}` | House detail | Member list with roles; add member, change role (Manager only), remove member |
| `/houses/{id}/expenses` | Expenses | Month selector; list split into **equally-split** and **meal-based**; add expense; **correct** an expense — never edit (§7.6) |
| `/houses/{id}/contributions` | Contributions | Who paid in how much this month; add contribution (recipient must be an active member) |
| `/houses/{id}/meals` | **Meal sheet** | Month × member grid of meal counts, editable per cell, with the conflict UX below |
| `/houses/{id}/meals/audit` | Audit trail | Who changed whose count, when, from what to what — visible to every member |
| `/houses/{id}/settlement` | Settlement | Preview (repeatable, read-only) → net position table → transfer list → **Finalize**. A finalized month renders read-only |

### Also owns
Form primitives, `৳` money formatting, Asia/Dhaka date display, and the toast / notification bell (§2).

### What must be right on your pages
- **The meal sheet carries a concurrency token per cell.** The read returns a `RowVersion` for every cell and the write sends it back for every changed cell. On conflict the sheet shows **only the conflicting cells** — *"15 Sep · Tanvir — you entered **2**, Sadia saved **3** at 12:04"* with **[Keep mine] [Keep theirs]** — then resubmits with fresh tokens, leaving unaffected edits intact (§7.5). **Build this from day one**; retrofitting it into a finished grid is painful.
- **Money is `decimal` everywhere and displays as `৳1,234.56`.** No `float`, no `double`, not even in a display helper (§11.6.1).
- **Nothing is edited in place.** Expenses and contributions get correcting entries; meal edits insert superseding rows. Your buttons say **"Correct"**, not "Edit" (§7.6).
- **A finalized period is locked** — the whole month renders read-only behind a clear banner, and every write control disappears (§7.4).
- Use the **§7.3 worked example** as your fixture data. It is a full four-member September including the ৳0.01 residual; if your settlement screen reproduces it exactly, the screen is right.

---

## 7 — Ishmam · M4 Second-hand marketplace

**Reference:** §8 · §3.11 · §8.6 — **the structural twin of M1** (§8.1), so build after Shreoshi's patterns land and mirror them.

### Pages

| Route | Page | What it does |
|---|---|---|
| `/marketplace` | Browse | Filters: `AreaCascade`, category, condition, price range; sort by newest / price ↑ / price ↓ from a fixed enum. Grid of item cards — thumbnail, title, price, condition |
| `/marketplace/items/{id}` | Item detail | `ImageGallery`, description, seller display name + verified badge, **Buy** button, **Report** button |
| `/marketplace/sell` | Post an item | Title, description, category, condition, price, `AreaCascade`, `ImageUploader`. Leave a slot for the **ML price suggestion** panel (§10.8) — the model advises, the seller decides |
| `/marketplace/items/{id}/edit` | Edit item | Owner-only. No seller field, no status field |
| `/marketplace/mine` | My listings | Including `Sold` and `Removed`; mark sold, delete |
| `/marketplace/items/{id}/interests` | Buyer list | Seller-only: buyer name, message, timestamp, verified badge, accept / decline. **Contact only on `Accepted`** |
| `/buy-interests/mine` | My buy requests | Status per request, withdraw, contact once accepted |

### Also owns
The **UI kit and CSS tokens**, `ImageUploader` / `ImageGallery`, and `ReportDialog` (§2). The UI kit is everyone's dependency, so it ships in the first days — small and boring on purpose.

### What must be right on your pages
- **Mirror M1 deliberately.** Items ↔ housing posts, buy interests ↔ booking requests, accept ↔ accept. Where the shapes match, reuse Shreoshi's component instead of writing a second one — that symmetry is why the flow is written once and reviewed once (§8.1).
- **The one asymmetry: no eligibility filtering.** Every active item is visible to every authenticated user. Do not copy `VisibleTo` semantics from M1.
- **Sort options come from a fixed enum**, never a column name in a query string (§11.5.1).
- **Self-purchase is impossible** — the Buy button is absent on your own item.
- `ImageUploader` shows extension and size limits as UX only; the real validation pipeline runs server-side later (§11.5.3). Never present client validation as a guarantee.
- Title, description and buy-interest message render as plain text (§11.5.2).

---

## 8 — Build sequence

| Phase | Who does what | Done when |
|---|---|---|
| **F0 · Unblock** | **Prapty:** app shell, home page, fix `NavMenu`. **Ishmam:** UI kit + CSS tokens. **Obonti:** form primitives + `৳`/date formatters. **Shreoshi:** `AreaCascade` over a static mock area dataset | Login lands on a real home page, nav works, and everyone has buttons and inputs to build with |
| **F1 · Browse + detail** | All four in parallel: the list page with filters, and the detail page, for your own module | Four modules are clickable end to end on fake data |
| **F2 · Create + mine** | All four: the create/edit form and the "mine" page for your module | A user can post a house, a helper profile, an expense, an item |
| **F3 · Two-party flows** | **Shreoshi:** bookings + `ContactDisclosureCard`. **Prapty:** engagement state machine + reviews. **Ishmam:** buy interests, reusing Shreoshi's card. **Obonti:** meal sheet conflict UX + settlement | Every disclosure transition and every state machine is demonstrable |
| **F4 · Polish** | Responsive pass; empty, loading and error states; `ReportDialog` wired into M1/M2/M4 | Ready to swap mocks for the real API |

**F0 blocks everything.** Nobody starts F1 pages before the shell and the UI kit exist, or four people will each invent a button.

---

## 9 — Working agreement

| Rule | Detail |
|---|---|
| **Branches** | `feat/<name>/<topic>` — e.g. `feat/shreoshi/housing-browse` (§12.3) |
| **`main` is protected** | PR plus at least one review. No direct pushes |
| **File ownership** | Stay inside your module's folders. Need a change in a shared component? Ask its owner — do not edit it inside your PR |
| **`Nestify.Shared`** | One DTO file per module, named for the module. Never edit another module's DTO file |
| **`Program.cs` and `NavMenu`** | The two files everyone must touch. Keep those edits to one line per PR, appended at the end of the list, so the merge stays mechanical |
| **`.gitignore` first** | ~1,223 `bin/`+`obj/` files are currently tracked (§12.2, M0). Until that is fixed, every PR conflicts on DLLs before it conflicts on code. **This is the first commit of the phase** |
| **No backend work** | If a task needs an endpoint, extend the mock service and note it. Do not start on `Nestify.Api` in this phase |

### Frontend pre-merge checklist

- [ ] No `MarkupString` on any user-supplied text
- [ ] No contact detail rendered outside its disclosure branch (§11.4)
- [ ] Money is `decimal`, displayed as `৳0.00` — no `float`/`double` anywhere
- [ ] Pages depend on the service **interface**, never on `HttpClient` directly
- [ ] Empty, loading and error states exist on every list page
- [ ] New DTOs live in your own file in `Nestify.Shared`
- [ ] Nothing added to `Nestify.Web/wwwroot/appsettings.json` except `ApiBaseUrl` — it is publicly downloadable (§1.1)

**One standing reminder:** every authorization decision made in the UI is **UX only** (§11.3.6). Hiding a button is a courtesy to the user, never a security control — the server re-checks all of it. Build every page as if the user can reach every route, because they can.

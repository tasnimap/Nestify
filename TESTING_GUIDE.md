# Nestify — Lab 6 Testing Guide (give this file to Claude)

Instruction to Claude: the student is short on time. Follow this file top to bottom. Help them run the app, execute each test case, record the result, and then generate the final report (`TESTING_REPORT.md`, converted to .docx/.pdf at the end). Do not invent results — every result must come from what the student actually saw. If a test fails, that is fine: failures are required content for the report.

---

## 0. What is implemented (only test these)

| Module | Implemented features |
|---|---|
| Auth | Register, Login, Logout, token refresh |
| Home | Create a home, add members, home capacity/seats |
| Housing | Post a house (seat), browse, view detail, edit, close/reopen, delete, request a seat (booking), owner accepts/rejects, requester withdraws, report a post, contact disclosure after acceptance |
| Marketplace | Post an item, browse, detail, edit, mark sold-to, delete, price suggestion, express interest, owner accepts/declines, withdraw interest, report |
| Settlement | Home expense settlement between members (add expense, view who owes whom) |
| Admin | Dashboard, create admin (Team), Fees & posting plans (Pricing), Housing moderation, Marketplace moderation, Audit log |

Do NOT test: domestic-helper pages, verifications, reviews — they are not done.

---

## 1. Setup (5 minutes)

1. Make sure PostgreSQL is running and the schema + seed SQL from `src/Nestify.Database/` are loaded.
2. Terminal 1: `dotnet run --project src/Nestify.Api` → API on `http://localhost:5293` (Swagger: `http://localhost:5293/swagger` if enabled).
3. Terminal 2: `dotnet run --project src/Nestify.Web` → Web on `http://localhost:5290`.
4. Open the browser, press **F12** and keep the **Network** and **Console** tabs open — API status codes (200/400/401/404/500) are evidence for integration tests.
5. Create a folder `report/screenshots/` in the project. Every screenshot goes there.

---

## 2. How to take screenshots (teacher requires them)

- Press **Win + Shift + S**, drag over the relevant part of the screen (the form + the result message, or the Swagger response, or the DevTools Network row), then paste it into Paint / save it.
- Name each file by test id: `TC-AUTH-01.png`, `TC-AUTH-02.png`, … One screenshot per test case, taken **after** the result appears (success toast, error message, red validation text, empty state, 500 page…).
- For API-level cases, screenshot the Swagger/Postman request + response body + status code.
- For boundary/invalid cases, make sure the **input value is visible** in the screenshot (e.g. the 7-character password) as well as the error.
- For bugs, also screenshot the DevTools Console / the exception page.

Tip: to go fast, do one module at a time — run all its cases, take all its screenshots, then move on.

---

## 3. Test case format (use in the report)

| ID | Feature | Level | Type | Input / Steps | Expected result | Actual result | Status | Screenshot |
|---|---|---|---|---|---|---|---|---|

- **Level**: Unit / Integration / System / Acceptance
- **Type**: Normal / Boundary / Exceptional / Invalid
- **Status**: Pass / Fail

How the four levels map to what the student can do manually:
- **Unit** – test one method/endpoint in isolation via Swagger or Postman (e.g. `POST /api/v1/auth/register` with a 7-char password → 400). Validation rules, single service methods.
- **Integration** – Web UI → API → DB working together (fill form on the Blazor page, watch the Network tab show the API call and the DB row appear).
- **System** – complete end-to-end flow across modules (register → create home → post house → another user requests → owner accepts → contact revealed).
- **Acceptance** – does it satisfy the user story from the point of view of a bachelor looking for a seat / a seller / an admin? Judge usability and whether the requirement is met.

---

## 4. Test cases to execute

Run each case, fill Actual + Status + screenshot. Suggested test data: user A = `arif@test.com`, user B = `sakib@test.com`, password `Pass1234`.

### 4.1 Authentication

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-AUTH-01 | Normal | Register with valid name, email, 8+ char password | Account created, redirected/logged in |
| TC-AUTH-02 | Invalid | Register with empty name / empty email | Error "Name, email and an 8+ character password are required." |
| TC-AUTH-03 | Boundary | Password exactly 8 chars | Accepted |
| TC-AUTH-04 | Boundary | Password 7 chars | Rejected with the 8+ message |
| TC-AUTH-05 | Exceptional | Register with an email already used | "An account with this email already exists." |
| TC-AUTH-06 | Invalid | Email without `@` (e.g. `arif.test.com`) | Should be rejected — note if it is accepted (bug) |
| TC-AUTH-07 | Normal | Login with correct credentials | Token returned, lands on `/home` |
| TC-AUTH-08 | Invalid | Login with wrong password | Error, stays on login |
| TC-AUTH-09 | Invalid | Login with non-existent email | Error, no hint whether the email exists |
| TC-AUTH-10 | Normal | Logout | Token cleared, protected page redirects to login |
| TC-AUTH-11 | Exceptional | Open `/housing/mine` without logging in | Redirect to `/login` or 401 |
| TC-AUTH-12 | Unit | Swagger `POST /api/v1/auth/refresh` with garbage token | 401, not 500 |
| TC-AUTH-13 | Boundary | Name with 1 character; name with 200+ characters | Note what happens |

### 4.2 Home (create home, members, capacity)

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-HOME-01 | Normal | Logged in as A, create a home with name, area, capacity 4 | Home created, shown on `/home` |
| TC-HOME-02 | Invalid | Create home with empty name | Validation error |
| TC-HOME-03 | Boundary | Capacity 0 | Rejected |
| TC-HOME-04 | Boundary | Capacity 1 | Accepted |
| TC-HOME-05 | Invalid | Capacity negative / non-numeric | Rejected |
| TC-HOME-06 | Normal | Add member B to the home | B appears in the members list |
| TC-HOME-07 | Exceptional | Add the same member twice | Error, not duplicated |
| TC-HOME-08 | Exceptional | Add a member email that does not exist | Clear error message |
| TC-HOME-09 | Boundary | Add members until capacity is full, then add one more | Last add is rejected (capacity) |
| TC-HOME-10 | Exceptional | User with no home opens `/settlement` | "No home" message, no crash |

### 4.3 Housing posts

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-HOU-01 | Normal | A posts a house with title, rent, seats, area, photo | Appears in `/housing` and `/housing/mine` |
| TC-HOU-02 | Invalid | Post with empty title | Validation error |
| TC-HOU-03 | Boundary | Rent = 0 | Rejected (or note if accepted) |
| TC-HOU-04 | Invalid | Rent negative | Rejected |
| TC-HOU-05 | Boundary | Seats offered > free seats in the home | Rejected |
| TC-HOU-06 | Exceptional | Post a house without owning a home | Clear error |
| TC-HOU-07 | Normal | Browse and filter by area / price | Correct results |
| TC-HOU-08 | Normal | Open `/housing/{id}` for a real post | Detail shown |
| TC-HOU-09 | Exceptional | Open `/housing/999999` | Not-found message, no crash |
| TC-HOU-10 | Invalid | Open `/housing/abc` | Handled, not a blank page |
| TC-HOU-11 | Normal | Edit own post, change rent | Updated |
| TC-HOU-12 | Exceptional | B tries `PUT /api/v1/housing/posts/{A's id}` via Swagger | 403/401 |
| TC-HOU-13 | Normal | Close post, then reopen | Status toggles, closed post hidden from browse |
| TC-HOU-14 | Normal | Delete post | Removed from lists |
| TC-HOU-15 | Normal | B requests a seat on A's post | Booking appears in `/bookings/mine` and `/housing/{id}/bookings` |
| TC-HOU-16 | Exceptional | B requests the same post twice | Second request blocked |
| TC-HOU-17 | Exceptional | A requests a seat on own post | Blocked |
| TC-HOU-18 | Normal | A accepts B's request | Status accepted; contact info now visible to B |
| TC-HOU-19 | Normal | A rejects a request | Status rejected; contact stays hidden |
| TC-HOU-20 | Normal | B withdraws a pending request | Status withdrawn |
| TC-HOU-21 | Exceptional | Withdraw an already accepted request | Blocked or handled |
| TC-HOU-22 | Exceptional | Request a closed post | Blocked |
| TC-HOU-23 | Boundary | Accept requests until seats are full, accept one more | Rejected (no seats left) |
| TC-HOU-24 | Normal | B reports a post with a reason | Report saved, visible in admin housing moderation |
| TC-HOU-25 | Invalid | Report with empty reason | Validation error |
| TC-HOU-26 | Exceptional | `GET /bookings/{id}/contact` before acceptance (Swagger) | 403/404, contact not leaked |

### 4.4 Marketplace

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-MKT-01 | Normal | A posts item with title, price, condition, photos | Appears in `/marketplace` |
| TC-MKT-02 | Invalid | Empty title | Validation error |
| TC-MKT-03 | Boundary | Price 0 | Note behaviour |
| TC-MKT-04 | Invalid | Price negative / text | Rejected |
| TC-MKT-05 | Boundary | Upload max allowed photos + 1 | Extra rejected |
| TC-MKT-06 | Invalid | Upload a non-image file (.txt) as a photo | Rejected |
| TC-MKT-07 | Normal | Price suggestion for a category | Suggestion returned |
| TC-MKT-08 | Exceptional | Price suggestion for unknown category (Swagger) | Empty/404, not 500 |
| TC-MKT-09 | Normal | Browse, filter, search | Correct results |
| TC-MKT-10 | Exceptional | `/marketplace/items/999999` | Not-found, no crash |
| TC-MKT-11 | Normal | Edit own item | Updated |
| TC-MKT-12 | Exceptional | B edits A's item via Swagger | Forbidden |
| TC-MKT-13 | Normal | B expresses interest | Shows in `/buy-interests/mine` and `/marketplace/items/{id}/interests` |
| TC-MKT-14 | Exceptional | B expresses interest twice | Blocked |
| TC-MKT-15 | Exceptional | A expresses interest in own item | Blocked |
| TC-MKT-16 | Normal | A accepts B's interest | Contact card revealed to B |
| TC-MKT-17 | Normal | A declines interest | Declined status |
| TC-MKT-18 | Normal | B withdraws interest | Withdrawn |
| TC-MKT-19 | Normal | A marks item sold to B | Listing status Sold, hidden from browse |
| TC-MKT-20 | Exceptional | Express interest on a Sold item | Blocked |
| TC-MKT-21 | Normal | Delete item | Removed |
| TC-MKT-22 | Normal | Report an item | Appears in admin marketplace moderation |
| TC-MKT-23 | Invalid | Report with empty reason | Validation error |

### 4.5 Settlement

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-SET-01 | Normal | A (home with A, B) adds expense 1000 split equally | B owes A 500 |
| TC-SET-02 | Boundary | Expense amount 0 | Rejected |
| TC-SET-03 | Invalid | Negative amount / text | Rejected |
| TC-SET-04 | Boundary | Amount with decimals, e.g. 333.33 split 3 ways | Rounding handled, totals add up |
| TC-SET-05 | Invalid | Empty description | Validation error |
| TC-SET-06 | Normal | Multiple expenses from both members | Net balance correct (calculate by hand and compare) |
| TC-SET-07 | Normal | Mark a balance as settled/paid | Balance becomes 0 |
| TC-SET-08 | Exceptional | Settle an amount larger than owed | Blocked |
| TC-SET-09 | Exceptional | Non-member opens settlement of another home (Swagger) | Forbidden |
| TC-SET-10 | Normal | Add member button from settlement page | Works, member listed |

### 4.6 Admin

| ID | Type | Steps | Expected |
|---|---|---|---|
| TC-ADM-01 | Normal | Login as admin, open `/admin` | Dashboard stats shown |
| TC-ADM-02 | Exceptional | Normal user opens `/admin` | Redirected / forbidden |
| TC-ADM-03 | Exceptional | Normal user calls admin API via Swagger | 401/403 |
| TC-ADM-04 | Normal | Team: create a new admin | Admin listed, can log in |
| TC-ADM-05 | Invalid | Create admin with empty email / short password | Validation error |
| TC-ADM-06 | Exceptional | Create admin with an existing email | Duplicate error |
| TC-ADM-07 | Normal | Pricing: change a fee / plan price | Saved, reflected after reload |
| TC-ADM-08 | Invalid | Fee negative / empty | Rejected |
| TC-ADM-09 | Boundary | Fee 0 | Accepted (free plan) or note |
| TC-ADM-10 | Normal | Housing moderation: see reported post from TC-HOU-24, take action (remove/dismiss) | Post state changes, user side reflects it |
| TC-ADM-11 | Normal | Marketplace moderation: same for TC-MKT-22 | Same |
| TC-ADM-12 | Normal | Audit log shows the actions from ADM-04, 07, 10, 11 | Entries with actor, action, time |
| TC-ADM-13 | Exceptional | Audit log with no entries / filter with no match | Empty state, no crash |

### 4.7 System / Acceptance scenarios (end-to-end)

| ID | Scenario |
|---|---|
| TC-SYS-01 | Register A → create home → post house → register B → B requests → A accepts → B sees contact → admin sees audit |
| TC-SYS-02 | A posts item → B interested → A marks sold to B → item disappears from browse → B's interest shows accepted |
| TC-SYS-03 | A & B in one home → 3 expenses → settlement page shows correct net → settle → balance zero |
| TC-SYS-04 | B reports A's post → admin removes it → A sees it removed → audit log has entry |
| TC-ACC-01 | As a bachelor, can I find a seat in my area within 3 clicks? (usability judgement) |
| TC-ACC-02 | As a seller, is posting an item quick and are errors understandable? |
| TC-ACC-03 | As an admin, can I find and act on a report without training? |

---

## 5. Report structure (`TESTING_REPORT.md` → export to PDF/DOCX)

1. **Cover** – project name Nestify, student name/ID, course, lab 6, date.
2. **Introduction** – what Nestify is (a platform for bachelors: shared-home seats, marketplace, expense settlement, admin moderation); which modules are complete and were tested; which are excluded and why.
3. **Test environment** – Windows 11, .NET 10, Blazor Web + ASP.NET Core API, PostgreSQL, browser, tools (Swagger, DevTools, manual testing).
4. **Testing approach** – manual testing; the four levels explained in one paragraph each and how each was applied to Nestify (from section 3 above); test case types (normal, boundary, exceptional, invalid).
5. **Test cases and results** – one table per module (sections 4.1–4.7) with the full 9-column format and a screenshot under or beside each row (`![TC-AUTH-04](screenshots/TC-AUTH-04.png)`).
6. **Summary of results** – total executed, passed, failed, per module; a small table.
7. **Bugs and unexpected behaviour found** – for each failure: ID, description, steps to reproduce, screenshot, severity (High/Medium/Low), root cause, and **how to fix** (e.g. "add `[EmailAddress]` validation on RegisterRequestDto and check in AuthService", "return 404 instead of 500 from HousingService.GetPost when id is missing", "add unique check before insert").
8. **Improvements** – short list (better validation messages, server-side validation mirrored on client, pagination limits, automated xUnit tests for services next).
9. **Conclusion** – 3–4 sentences.

---

## 6. Steps for Claude to follow with the student

1. Confirm the API and Web are running (ask the student, don't start them yourself unless asked).
2. Go module by module (4.1 → 4.7). For each case tell the student exactly what to click/type, ask what happened, record Actual/Status, and remind them to take the screenshot with the exact filename.
3. When the student reports a failure or a crash, ask for the error text, then look at the relevant code in `src/Nestify.Api` / `src/Nestify.Web` and write the "how to fix" explanation for the report (do not change code unless the student asks).
4. If time is short: prioritise Auth, Housing, Marketplace, Settlement, Admin — at least 3 normal, 2 boundary, 2 invalid, 2 exceptional per module, plus TC-SYS-01 and TC-SYS-02.
5. After all cases, generate `report/TESTING_REPORT.md` with the structure in section 5, embedding the screenshots, then convert with `pandoc report/TESTING_REPORT.md -o report/TESTING_REPORT.docx` (or tell the student to paste into Word).
6. Aim for 60–80 test cases total; this is enough to show systematic coverage.

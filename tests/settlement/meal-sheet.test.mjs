import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const razor = readFileSync(
    new URL("../../src/Nestify.Web/Pages/User/Settlement.razor", import.meta.url),
    "utf8");
const styles = readFileSync(
    new URL("../../src/Nestify.Web/Pages/User/Settlement.razor.css", import.meta.url),
    "utf8");
const state = readFileSync(
    new URL("../../src/Nestify.Web/Components/Settlement/MealSheetState.cs", import.meta.url),
    "utf8");
const settlementService = readFileSync(
    new URL("../../src/Nestify.Api/Settlement/SettlementService.cs", import.meta.url),
    "utf8");

test("meal sheet exposes one stepper for each daily meal slot", () => {
    assert.match(razor, /MealSheetState\.Slot\.Breakfast/);
    assert.match(razor, /MealSheetState\.Slot\.Lunch/);
    assert.match(razor, /MealSheetState\.Slot\.Dinner/);
    assert.equal((razor.match(/class="stl__slot-step stl__slot-step--up"/g) ?? []).length, 1);
    assert.equal((razor.match(/class="stl__slot-step stl__slot-step--down"/g) ?? []).length, 1);
    assert.match(razor, /stl__chevron stl__chevron--left/);
    assert.match(razor, /stl__chevron stl__chevron--right/);
    assert.match(styles, /\.stl__slot-step--down[\s\S]*?background: var\(--nx-card\)/);
    assert.match(styles, /\.stl__slot-step--up[\s\S]*?background: #dff2ff/);
    assert.doesNotMatch(razor, /class="stl__slot-sum"/);
    assert.doesNotMatch(razor, /SlotShortLabel/);
    assert.doesNotMatch(razor, /_onlyMyColumn/);
});

test("daily totals are calculated once from each date/member cell", () => {
    assert.match(state, /public decimal Total => Breakfast \+ Lunch \+ Dinner;/);
    assert.match(state, /public decimal DayTotal\(DateOnly date\) =>\s*_cells\.Where\(c => c\.Key\.Date == date\)\.Sum\(c => c\.Value\.Total\);/);
});

test("the daily total remains in its dedicated table column", () => {
    assert.match(razor, /<th class="stl__sheet-total">@L\["Day"\]<\/th>/);
    assert.match(razor, /<td class="stl__sheet-total">@_sheet\.DayTotal\(day\)/);
});

test("members can submit their own payments while managers can choose the payer", () => {
    assert.match(razor, /@if \(IsOpen\)\s*\{\s*<aside class="stl__card stl__card--form"/);
    assert.match(razor, /@if \(_ws!\.CanManage\)/);
    assert.match(razor, /value="@CurrentMemberName" readonly/);
    assert.match(settlementService, /var canRecordForOthers = membership\.Role is HomeService\.RoleManager or HomeService\.RoleCoManager;/);
    assert.match(settlementService, /request\.UserId = userId;/);
    assert.match(razor, /@onclick="FinalizePeriod"/);
    assert.match(razor, /_ws is not null && _ws\.CanManage && IsOpen/);
});

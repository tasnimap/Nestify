import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const razor = readFileSync(
    new URL("../../src/Nestify.Web/Pages/User/Settlement.razor", import.meta.url),
    "utf8");
const state = readFileSync(
    new URL("../../src/Nestify.Web/Components/Settlement/MealSheetState.cs", import.meta.url),
    "utf8");

test("meal sheet exposes one stepper for each daily meal slot", () => {
    assert.match(razor, /MealSheetState\.Slot\.Breakfast/);
    assert.match(razor, /MealSheetState\.Slot\.Lunch/);
    assert.match(razor, /MealSheetState\.Slot\.Dinner/);
    assert.equal((razor.match(/class="stl__slot-step stl__slot-step--up"/g) ?? []).length, 1);
    assert.equal((razor.match(/class="stl__slot-step stl__slot-step--down"/g) ?? []).length, 1);
    assert.doesNotMatch(razor, /class="stl__slot-sum"/);
    assert.doesNotMatch(razor, /SlotShortLabel/);
});

test("daily totals are calculated once from each date/member cell", () => {
    assert.match(state, /public decimal Total => Breakfast \+ Lunch \+ Dinner;/);
    assert.match(state, /public decimal DayTotal\(DateOnly date\) =>\s*_cells\.Where\(c => c\.Key\.Date == date\)\.Sum\(c => c\.Value\.Total\);/);
});

test("the daily total remains in its dedicated table column", () => {
    assert.match(razor, /<th class="stl__sheet-total">@L\["Day"\]<\/th>/);
    assert.match(razor, /<td class="stl__sheet-total">@_sheet\.DayTotal\(day\)/);
});

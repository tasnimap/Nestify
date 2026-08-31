# Core Utilities & Form Primitives — Implementation Guide

## Overview

This document describes the core utilities and reusable form components built for the Nestify M3 module (Shared expense & meal settlement). These utilities implement the requirements from §2, §7, and §11.6 of the Implementation Plan.

**Demo page:** Visit `/demo/forms` to see all components in action.

---

## Table of Contents

1. [Services](#services)
2. [Form Components](#form-components)
3. [CSS Styling](#css-styling)
4. [Usage Examples](#usage-examples)
5. [Architecture Notes](#architecture-notes)

---

## Services

### MoneyFormatterService

**Purpose:** Format decimal values as Bangladeshi Taka (৳) currency.

**Key requirement (§11.6.1):** Money is `decimal` everywhere and displays as `৳1,234.56`. Binary floating point cannot represent 0.10 exactly, so `decimal` is used to prevent silent drifting in settlements.

#### Methods

```csharp
// Format with currency symbol
string Format(decimal amount)
// Example: 1234.56m → "৳1,234.56"

// Format without currency symbol (for input display)
string FormatNumber(decimal amount)
// Example: 1234.56m → "1,234.56"

// Parse string input to decimal
bool TryParse(string input, out decimal result)
// Handles: "১২,৩৪৫.৬৭", "৳12,345.67", or "12345.67"

// Compact format for constrained space
string FormatCompact(decimal amount)
// Example: 1234567.89m → "৳1.2M"

// Format as percentage
string FormatAsPercentage(decimal value)
// Example: 0.15m → "৳0.15" (displayed as 15%)
```

#### Injection

```csharp
@inject MoneyFormatterService MoneyFormatter

<span>@MoneyFormatter.Format(787.50m)</span> <!-- ৳787.50 -->
```

---

### DateFormatterService

**Purpose:** Display DateTime values in Asia/Dhaka timezone (UTC+6).

**Key requirement (§0.3):** All timestamps are stored in UTC. Display converts to Asia/Dhaka. Why: a settlement month boundary computed in the wrong zone silently moves expenses between months.

#### Methods

```csharp
// Convert UTC DateTime to Dhaka timezone date
string FormatDate(DateTime utcTime)
// Example: 2026-09-15T08:30:00Z → "15 Sep"

// Format with time
string FormatDateTime(DateTime utcTime)
// Example: 2026-09-15T08:30:00Z → "15 Sep · 14:30"

// Full format with seconds
string FormatFullDateTime(DateTime utcTime)
// Example: 2026-09-15T08:30:00Z → "15 September 2026, 14:30:45"

// ISO 8601 format in Dhaka timezone
string FormatIso(DateTime utcTime)
// Example: 2026-09-15T08:30:00Z → "2026-09-15T14:30:00"

// Relative time (e.g., "2 hours ago")
string FormatRelative(DateTime utcTime)
// Example: 2 hours ago → "2h ago"

// Get current date in Dhaka timezone
DateOnly GetTodayInDhaka()

// Get month boundaries in UTC
DateTime GetMonthStartUtc(int year, int month)
DateTime GetMonthEndUtc(int year, int month)
```

#### Injection

```csharp
@inject DateFormatterService DateFormatter

<span>@DateFormatter.FormatDateTime(DateTime.UtcNow)</span> <!-- 15 Sep · 14:30 -->
```

#### Month Filtering Example

When filtering expenses for September 2026:

```csharp
var startUtc = DateFormatter.GetMonthStartUtc(2026, 9);  // 2026-09-01T00:00:00Z
var endUtc = DateFormatter.GetMonthEndUtc(2026, 9);      // 2026-09-30T23:59:59Z

var expenses = dbContext.Expenses
    .Where(e => e.CreatedAtUtc >= startUtc && e.CreatedAtUtc <= endUtc)
    .ToList();
```

---

### ToastService

**Purpose:** Display temporary notification messages (toast notifications).

**Implements:** The "toast / notification bell" requirement from §2.

#### Methods

```csharp
// Show notifications by type
void ShowInfo(string message, string? title = null, int durationMs = 4000)
void ShowSuccess(string message, string? title = null, int durationMs = 4000)
void ShowWarning(string message, string? title = null, int durationMs = 5000)
void ShowError(string message, string? title = null, int durationMs = 6000)

// Generic show method
void Show(ToastType type, string message, string? title = null, int durationMs = 4000)

// Dismiss a toast by ID
void Dismiss(string toastId)

// Clear all toasts
void ClearAll()
```

#### Properties

```csharp
IReadOnlyList<Toast> Toasts { get; }  // Current active toasts
event EventHandler? OnToastsChanged;   // Fired when toasts change
```

#### Injection

```csharp
@inject ToastService ToastService

<!-- In your component -->
<button @onclick="() => ToastService.ShowSuccess('Saved successfully!')">
    Save
</button>
```

---

## Form Components

All form components are reusable, fully validated, and follow the Nestify design system.

### TextInput

Generic text input component.

```razor
<TextInput @bind-Value="@model.Name"
    Label="Full Name"
    Placeholder="Enter your name"
    Type="text"
    Autocomplete="name"
    For="@(() => model.Name)" />
```

**Parameters:**
- `@bind-Value` — The bound value
- `Label` — Display label
- `Placeholder` — Placeholder text
- `Type` — Input type (default: "text")
- `Autocomplete` — Autocomplete attribute
- `Disabled` — Disable the input
- `HelperContent` — Additional helper text
- `ActionLabel` — Button label for inline action
- `OnAction` — Action button callback

---

### MoneyInput

Specialized input for monetary values with ৳ symbol and automatic formatting.

```razor
<MoneyInput @bind-Value="@model.Amount"
    Label="Amount (৳)"
    Placeholder="0.00"
    For="@(() => model.Amount)" />
```

**Key features:**
- Auto-formats input as currency
- Displays ৳ symbol prefix
- Parses user input using `MoneyFormatterService`
- Stores value as `decimal`
- Uses `inputmode="decimal"` for mobile UX

**Parameters:** Same as `TextInput` with monetary validation built-in

---

### DateInput

Date picker with Dhaka timezone awareness.

```razor
<DateInput @bind-Value="@model.Date"
    Label="Expense Date"
    For="@(() => model.Date)" />
```

**Key features:**
- HTML5 date picker
- Handles `DateTime` values
- Validation built-in

---

### MoneyDisplay

Read-only money display component.

```razor
<MoneyDisplay Amount="2803.44m" />  <!-- ৳2,803.44 -->
<MoneyDisplay Amount="1234567m" Compact="true" />  <!-- ৳1.2M -->
<MoneyDisplay Amount="value" CssClass="positive" />
```

**Parameters:**
- `Amount` — The decimal amount
- `Compact` — Use abbreviated format (K, M)
- `CssClass` — CSS class (e.g., "positive", "negative")

---

### DateDisplay

Read-only date display with multiple format options.

```razor
<DateDisplay DateTime="DateTime.UtcNow" Format="date" />      <!-- 15 Sep -->
<DateDisplay DateTime="DateTime.UtcNow" Format="datetime" />  <!-- 15 Sep · 14:30 -->
<DateDisplay DateTime="DateTime.UtcNow" Format="relative" />  <!-- 2h ago -->
```

**Parameters:**
- `DateTime` — UTC datetime to display
- `Format` — "date", "datetime", "full", "iso", or "relative"
- `CssClass` — Additional CSS classes

---

### ToastContainer

Renders all active toast notifications. **Place once in `App.razor`** (already done).

```razor
<!-- In App.razor -->
<ToastContainer />
```

---

## CSS Styling

### Form Primitives CSS

File: `wwwroot/css/form-primitives.css`

Provides styling for:
- Money input with prefix
- Money display variants
- Date inputs
- Field helpers and error states
- Checkboxes and radios
- Textarea
- Disabled states
- Focus states

All styles follow the Nestify design system (nestify.css) and are responsive for mobile.

### Toast CSS

File: `wwwroot/css/toast.css`

Features:
- Fixed position in top-right corner
- Slide-in animation
- Type-specific colors (info, success, warning, error)
- Auto-dismiss on timer
- Dismiss button
- Mobile-responsive (bottom position on small screens)
- Respects `prefers-reduced-motion`

---

## Usage Examples

### Complete Form Example

```razor
@page "/expenses/new"
@using System.ComponentModel.DataAnnotations
@inject ToastService ToastService
@inject MoneyFormatterService MoneyFormatter

<EditForm Model="@model" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />

    <TextInput @bind-Value="@model.Description"
        Label="Description"
        For="@(() => model.Description)" />

    <MoneyInput @bind-Value="@model.Amount"
        Label="Amount (৳)"
        For="@(() => model.Amount)" />

    <DateInput @bind-Value="@model.Date"
        Label="Date"
        For="@(() => model.Date)" />

    <button type="submit" class="nx-btn nx-btn--primary">
        Add Expense
    </button>
</EditForm>

@code {
    private ExpenseModel model = new();

    private class ExpenseModel
    {
        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;
    }

    private async Task HandleSubmit()
    {
        // Save to API
        await ExpenseService.CreateAsync(model);
        
        ToastService.ShowSuccess(
            $"Created expense: {MoneyFormatter.Format(model.Amount)}",
            "Expense Added"
        );
    }
}
```

### Displaying Settlement Data

```razor
@inject MoneyFormatterService MoneyFormatter
@inject DateFormatterService DateFormatter

<table>
    <thead>
        <tr>
            <th>Member</th>
            <th>Amount</th>
            <th>Date</th>
            <th>Status</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var transfer in settlement.Transfers)
        {
            <tr>
                <td>@transfer.FromMember → @transfer.ToMember</td>
                <td>
                    <MoneyDisplay Amount="transfer.Amount" />
                </td>
                <td>
                    <DateDisplay DateTime="transfer.CreatedAtUtc" Format="date" />
                </td>
                <td>@transfer.Status</td>
            </tr>
        }
    </tbody>
</table>
```

---

## Architecture Notes

### Why These Choices?

1. **MoneyFormatterService**
   - Centralizes money formatting logic to ensure consistency
   - Prevents accidental use of `float`/`double` (§11.6.1)
   - Handles both display and parsing

2. **DateFormatterService**
   - Ensures all dates display in Dhaka timezone
   - Prevents silent month-boundary shifts (§0.3)
   - Methods for month filtering support settlement calculations

3. **Reusable Components**
   - Reduces duplication across M3 pages
   - Consistent styling and validation
   - Easier to maintain and test

4. **ToastService**
   - Non-blocking notifications
   - Auto-dismiss with manual dismiss option
   - Type-specific styling for user clarity

### Integration Points

- **Program.cs:** Services registered as `AddScoped()`
- **_Imports.razor:** Namespace imports for components
- **index.html:** CSS files loaded globally
- **App.razor:** `<ToastContainer />` renders notifications

---

## Testing the Implementation

Visit `/demo/forms` to see all components and utilities in action. The demo page includes:
- Live form validation
- Money formatting examples
- Date display in Dhaka timezone
- Toast notification examples

---

## Next Steps

With these utilities in place, the following M3 pages can be built:
1. **Expenses page** — Add/correct expenses using `MoneyInput` and `DateInput`
2. **Contributions page** — Track payments with `MoneyDisplay`
3. **Meal sheet page** — Complex grid with concurrency tokens
4. **Settlement page** — Read-only preview with settlement transfers
5. **Audit trail page** — Show who changed what when

Each page will reuse these components and services consistently.

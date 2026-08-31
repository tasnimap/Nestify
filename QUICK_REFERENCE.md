# Quick Reference — Core Utilities & Form Primitives

## Services

### MoneyFormatterService
```csharp
@inject MoneyFormatterService Money

Money.Format(787.50m)           // "৳787.50"
Money.FormatNumber(787.50m)     // "787.50"
Money.TryParse("১,২৩৪.৫৬", out var v) // v = 1234.56m
Money.FormatCompact(1234567m)   // "৳1.2M"
```

### DateFormatterService
```csharp
@inject DateFormatterService Date

Date.FormatDate(DateTime.UtcNow)           // "15 Sep"
Date.FormatDateTime(DateTime.UtcNow)       // "15 Sep · 14:30"
Date.FormatFullDateTime(DateTime.UtcNow)   // "15 September 2026, 14:30:45"
Date.FormatRelative(DateTime.UtcNow.AddHours(-2)) // "2h ago"

// For filtering expenses by month:
var start = Date.GetMonthStartUtc(2026, 9);  // Sept 1 in Dhaka → UTC
var end = Date.GetMonthEndUtc(2026, 9);      // Sept 30 in Dhaka → UTC
```

### ToastService
```csharp
@inject ToastService Toast

Toast.ShowInfo("Message", "Title");     // Blue
Toast.ShowSuccess("Message", "Title");  // Green
Toast.ShowWarning("Message", "Title");  // Yellow
Toast.ShowError("Message", "Title");    // Red
```

---

## Form Components

### TextInput
```razor
<TextInput @bind-Value="@model.Name"
    Label="Full Name"
    Placeholder="Enter name"
    For="@(() => model.Name)" />
```

### MoneyInput ⭐
```razor
<MoneyInput @bind-Value="@model.Amount"
    Label="Amount (৳)"
    Placeholder="0.00"
    For="@(() => model.Amount)" />
```

### DateInput
```razor
<DateInput @bind-Value="@model.Date"
    Label="Date"
    For="@(() => model.Date)" />
```

### MoneyDisplay
```razor
<MoneyDisplay Amount="787.50m" />           <!-- ৳787.50 -->
<MoneyDisplay Amount="1234567m" Compact="true" /> <!-- ৳1.2M -->
```

### DateDisplay
```razor
<DateDisplay DateTime="DateTime.UtcNow" Format="date" />      <!-- 15 Sep -->
<DateDisplay DateTime="DateTime.UtcNow" Format="datetime" />  <!-- 15 Sep · 14:30 -->
<DateDisplay DateTime="DateTime.UtcNow" Format="relative" />  <!-- 2h ago -->
```

---

## Complete Form Example

```razor
@page "/expenses/new"
@using System.ComponentModel.DataAnnotations
@inject ToastService Toast
@inject MoneyFormatterService Money

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

    <button type="submit" class="nx-btn nx-btn--primary">Add</button>
</EditForm>

@code {
    private class ExpenseModel
    {
        [Required] public string Description { get; set; } = "";
        [Required][Range(0.01, 1000000)] public decimal Amount { get; set; }
        [Required] public DateTime Date { get; set; } = DateTime.Today;
    }

    private readonly ExpenseModel model = new();

    private async Task HandleSubmit()
    {
        // Save to API
        Toast.ShowSuccess($"Added {Money.Format(model.Amount)}");
    }
}
```

---

## CSS Classes

### Form Fields
- `.field` — Field wrapper
- `.field__label` — Label styling
- `.field__input` — Input styling
- `.field__helper` — Helper text
- `.field__error` — Error message
- `.field__check` — Checkbox/radio wrapper

### Money Styling
- `.money-input__control` — Money input container
- `.money-input__prefix` — ৳ symbol styling
- `.money-display` — Read-only money display

### Toast
- `.toast` — Toast wrapper
- `.toast--info`, `.toast--success`, `.toast--warning`, `.toast--error` — Type variants
- `.toast__title` — Toast title
- `.toast__message` — Toast message
- `.toast__dismiss` — Close button

---

## Rules to Remember

1. **Always use `decimal` for money** — Never `float` or `double`
2. **Use UTC timestamps** — Services convert to Dhaka automatically
3. **Validate amounts** — Use `[Range(0.01, max)]` to prevent zero/negative
4. **Month filtering** — Use `DateFormatterService.GetMonthStartUtc()` and `GetMonthEndUtc()`
5. **Never edit in place** — Use "Correct" button for correcting entries (§7.6)

---

## Demo & Testing

Visit `/demo/forms` to:
- See all components working
- Test form validation
- Try toast notifications
- View formatting examples

---

## File Locations

| What | Where |
|---|---|
| Money formatting | `Services/MoneyFormatterService.cs` |
| Date formatting | `Services/DateFormatterService.cs` |
| Toast notifications | `Services/ToastService.cs` |
| Form components | `Components/Forms/*` |
| Form CSS | `wwwroot/css/form-primitives.css` |
| Toast CSS | `wwwroot/css/toast.css` |
| Demo page | `Pages/FormsDemo.razor` |
| Full docs | `CORE_UTILITIES.md` |

---

## Common Patterns

### Show error toast
```csharp
Toast.ShowError("Failed to save expense", "Error");
```

### Display settlement transfer
```razor
<div>
    <strong>@transfer.From</strong> → <strong>@transfer.To</strong>:
    <MoneyDisplay Amount="@transfer.Amount" />
    (<DateDisplay DateTime="@transfer.Date" Format="date" />)
</div>
```

### Filter expenses by month
```csharp
var start = DateFormatter.GetMonthStartUtc(2026, 9);
var end = DateFormatter.GetMonthEndUtc(2026, 9);
var expenses = db.Expenses
    .Where(e => e.CreatedAtUtc >= start && e.CreatedAtUtc <= end)
    .ToList();
```

---

*See `CORE_UTILITIES.md` for detailed documentation and examples.*

using Nestify.Shared.Dtos.Settlement;

namespace Nestify.Web.Components.Settlement;

// Holds the meal sheet cells for one month and tracks which ones were edited
// but not yet saved, so the page only sends the changed cells to the API.
public sealed class MealSheetState
{
    public enum Slot { Breakfast, Lunch, Dinner }

    public sealed record CellKey(DateOnly Date, long UserId);

    public sealed class Cell
    {
        public decimal Breakfast { get; set; }
        public decimal Lunch { get; set; }
        public decimal Dinner { get; set; }
        public bool IsDirty { get; set; }

        public decimal Total => Breakfast + Lunch + Dinner;

        public decimal this[Slot slot]
        {
            get => slot switch
            {
                Slot.Breakfast => Breakfast,
                Slot.Lunch => Lunch,
                _ => Dinner
            };
            set
            {
                switch (slot)
                {
                    case Slot.Breakfast: Breakfast = value; break;
                    case Slot.Lunch: Lunch = value; break;
                    default: Dinner = value; break;
                }
            }
        }
    }

    private readonly Dictionary<CellKey, Cell> _cells = [];

    public bool HasUnsaved => _cells.Values.Any(c => c.IsDirty);

    public void Load(IEnumerable<SettlementMealDto> meals)
    {
        _cells.Clear();
        foreach (var meal in meals)
        {
            _cells[new CellKey(DateOnly.FromDateTime(meal.MealDate), meal.UserId)] = new Cell
            {
                Breakfast = meal.Breakfast,
                Lunch = meal.Lunch,
                Dinner = meal.Dinner
            };
        }
    }

    public Cell Get(CellKey key) => _cells.TryGetValue(key, out var cell) ? cell : new Cell();

    public void Set(CellKey key, Slot slot, decimal value)
    {
        value = Math.Clamp(value, 0m, 10m);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new Cell();
            _cells[key] = cell;
        }

        if (cell[slot] == value)
        {
            return;
        }

        cell[slot] = value;
        cell.IsDirty = true;
    }

    public decimal MemberTotal(long userId) =>
        _cells.Where(c => c.Key.UserId == userId).Sum(c => c.Value.Total);

    public decimal MemberSlotTotal(long userId, Slot slot) =>
        _cells.Where(c => c.Key.UserId == userId).Sum(c => c.Value[slot]);

    public decimal DayTotal(DateOnly date) =>
        _cells.Where(c => c.Key.Date == date).Sum(c => c.Value.Total);

    public decimal Total => _cells.Values.Sum(c => c.Total);

    public List<SettlementMealChangeDto> DirtyEntries() =>
        _cells.Where(c => c.Value.IsDirty)
            .Select(c => new SettlementMealChangeDto
            {
                UserId = c.Key.UserId,
                MealDate = c.Key.Date.ToDateTime(TimeOnly.MinValue),
                Breakfast = c.Value.Breakfast,
                Lunch = c.Value.Lunch,
                Dinner = c.Value.Dinner
            })
            .ToList();
}

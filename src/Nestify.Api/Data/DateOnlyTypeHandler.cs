using System.Data;
using Dapper;

namespace Nestify.Api.Data;

/// <summary>
/// Allows Dapper to serialize and deserialize .NET <see cref="DateOnly"/> values
/// to and from database DATE columns.
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => DateOnly.Parse(value.ToString()!)
    };
}

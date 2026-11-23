using System;
using NextORM.Core;

namespace Nextorm.Extensions.Internal;

internal sealed class PostgreSqlDateConverter : ValueConverter<DateOnly, DateTime>
{
    public override DateOnly ConvertFromProvider(DateTime provider) => DateOnly.FromDateTime(provider);

    public override DateTime ConvertToProvider(DateOnly model) => model.ToDateTime(TimeOnly.MinValue);
}

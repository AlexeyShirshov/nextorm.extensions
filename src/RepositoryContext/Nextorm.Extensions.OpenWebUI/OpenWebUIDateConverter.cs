using System;
using System.Globalization;
using NextORM.Core;

namespace Nextorm.Extensions.OpenWebUI;

internal sealed class OpenWebUIDateConverter : ValueConverter<DateOnly, string>
{
    public override DateOnly ConvertFromProvider(string? provider) =>
        DateOnly.ParseExact(provider!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public override string ConvertToProvider(DateOnly model) =>
        model.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

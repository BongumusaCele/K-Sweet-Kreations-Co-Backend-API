using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

// HTML number fields submit a dot decimal separator regardless of server locale.
public class InvariantDecimalBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        if (decimal.TryParse(value.FirstValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var number)) context.Result = ModelBindingResult.Success(number);
        else context.ModelState.TryAddModelError(context.ModelName, "Enter a valid price using a decimal point.");
        return Task.CompletedTask;
    }
}

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Presentation.Swagger;

public class EnumSchemaFilter : ISchemaFilter
{
	public void Apply(OpenApiSchema schema, SchemaFilterContext context)
	{
		var type = context.Type;
		switch (type.IsEnum)
		{
			case true:
			{
				var enumNames = Enum.GetNames(type);
				var enumValues = Enum.GetValues(type).Cast<int>().ToArray();
				schema.Description +=
					"\n\nValues:\n" + string.Join("\n", enumNames.Select((n, i) => $"{enumValues[i]} = {n}"));
				break;
			}
		}
	}
}
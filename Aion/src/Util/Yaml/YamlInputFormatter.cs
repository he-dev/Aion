using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using YamlDotNet.Serialization;

namespace Aion.Util.Yaml;

public class YamlInputFormatter : InputFormatter
{
    public YamlInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/yaml"));
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/yaml"));
    }

    protected override bool CanReadType(Type type) => true;

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        using var reader = new StreamReader(context.HttpContext.Request.Body);
        var content = await reader.ReadToEndAsync();
        var deserializer = new DeserializerBuilder().Build();
        var result = deserializer.Deserialize(content, context.ModelType);
        return await InputFormatterResult.SuccessAsync(result);
    }
}
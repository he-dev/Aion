using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using YamlDotNet.Serialization;

namespace AionApi.Util.Yaml;

public class YamlOutputFormatter : TextOutputFormatter
{
    private readonly ISerializer _serializer =
        new SerializerBuilder()
            .WithTypeConverter(new YamlDateTimeOffsetConverter())
            .Build();

    public YamlOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-yaml"));
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/yaml"));
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanWriteType(Type? type) => true;

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var yaml = _serializer.Serialize(context.Object);
        await context.HttpContext.Response.WriteAsync(yaml, selectedEncoding);
    }
}
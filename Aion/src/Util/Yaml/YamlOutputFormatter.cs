using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using YamlDotNet.Serialization;

namespace Aion.Util.Yaml;

public class YamlOutputFormatter : TextOutputFormatter
{
    public YamlOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/yaml")); // utf-8
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/yaml")); // non utf-8
        SupportedEncodings.Add(Encoding.UTF8);
        //SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanWriteType(Type? type) => true;

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var serializer =
            new SerializerBuilder()
                .WithTypeConverter(new YamlDateTimeOffsetConverter())
                .Build();

        //var yaml = serializer.Serialize(context.Object);
        //await context.HttpContext.Response.WriteAsync(yaml, selectedEncoding);

        await using var writer = context.WriterFactory(context.HttpContext.Response.Body, selectedEncoding);
        serializer.Serialize(writer, context.Object);
        await writer.FlushAsync();
    }
}
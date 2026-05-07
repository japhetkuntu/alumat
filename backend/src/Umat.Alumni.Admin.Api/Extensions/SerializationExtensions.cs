using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Umat.Alumni.Admin.Api.Extensions;

public static class SerializationExtensions
{
    public static string Serialize<T>(this T @object, JsonSerializerSettings? settings = null)
        where T : notnull
    {
        settings ??= new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
        return JsonConvert.SerializeObject(@object, settings);
    }
}

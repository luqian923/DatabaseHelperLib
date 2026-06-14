using Newtonsoft.Json;
using SqlSugar;

namespace LQ.DatabaseHelper;

public class CustomSerializeService : ISerializeService
{
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore, // ignore default values
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };

    // ignore default values

    public string SerializeObject(object value)
    {
        return JsonConvert.SerializeObject(value, _jsonSettings);
    }

    public T DeserializeObject<T>(string value)
    {
        try
        {
            var clazz = JsonConvert.DeserializeObject<T>(value)!;
            return clazz;
        }
        catch
        {
            // try to create empty instance
            try
            {
                var inst = Activator.CreateInstance<T>();
                return inst;
            }
            catch
            {
                return default!;
            }
        }
    }

    public string SugarSerializeObject(object value)
    {
        return JsonConvert.SerializeObject(value, _jsonSettings);
    }
}
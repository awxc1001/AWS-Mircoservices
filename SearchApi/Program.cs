using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SearchApi.Models;
using Nest;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();


app.MapGet("/search", async (string? city, int? rating) =>
{
    // 1. 直接用常量或配置，别用 SetEnvironmentVariable
    var host = "https://hotels-988240.es.australia-southeast1.gcp.elastic-cloud.com";
    var username = "elastic";
    var password = "Ex9DjebSS5ZszR6fVQZhDgXH";
    var indexName = "event";

    var conSettings = new ConnectionSettings(new Uri(host))
        .BasicAuthentication(username, password)
        .DefaultIndex(indexName)
        .DefaultMappingFor<Hotel>(m => m.IdProperty(h => h.HotelId));
    var client = new ElasticClient(conSettings);

    var mustQueries = new List<Func<QueryContainerDescriptor<Hotel>, QueryContainer>>();
    if (!string.IsNullOrEmpty(city))
        mustQueries.Add(m => m.Prefix(p => p.Field(f => f.CityName).Value(city).CaseInsensitive()));
    if (rating.HasValue)
        mustQueries.Add(m => m.Range(r => r.Field(f => f.Rating).GreaterThanOrEquals(rating.Value)));

    var response = await client.SearchAsync<Hotel>(s => s
        .Query(q => q.Bool(b => b.Must(mustQueries)))
        .Size(20)
    );

     // 返回查到的酒店列表
    return response.Hits.Select(x => x.Source).ToList();

});



app.Run();

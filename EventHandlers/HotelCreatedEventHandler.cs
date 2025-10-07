using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;
using Nest;
using System.Text.Json;
using EventHandlers.models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace EventHandlers;

public class HotelCreatedEventHandler
{

    public async Task Handler(SNSEvent snsEvent)
    {
        var dbCClient = new AmazonDynamoDBClient();
        var table = Table.LoadTable(dbCClient, "hotel-created-event-ids");



        var host = Environment.GetEnvironmentVariable("host");
        var userName = Environment.GetEnvironmentVariable("userName");
        var password = Environment.GetEnvironmentVariable("password");
        var indexName = Environment.GetEnvironmentVariable("indexName");

        Console.WriteLine($"Elasticsearch Config: host={host}, username={userName}, indexName={indexName}");

        var conSettings = new ConnectionSettings(new Uri(host))
            .BasicAuthentication(userName, password)
            .DefaultIndex(indexName);
        conSettings.DefaultMappingFor<Hotel>(m => m.IdProperty(h => h.HotelId));

        var elasticClient = new ElasticClient(conSettings);

        try
        {
            if (!(await elasticClient.Indices.ExistsAsync(indexName)).Exists)
            {
                Console.WriteLine($"Creating index: {indexName}");
                await elasticClient.Indices.CreateAsync(indexName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating/checking index: {ex.Message}");
        }

        foreach (var eventRecord in snsEvent.Records)
        {
            var eventId = eventRecord.Sns.MessageId;
            var foundItem = await table.GetItemAsync(eventId);
            if (foundItem == null)
            {
                await table.PutItemAsync(new Document
                {
                    ["eventid"] = eventId
                });

                // Process the event
                Console.WriteLine($"Processing event {eventId}");
            }
            else
            {
                Console.WriteLine($"Skipping duplicate event {eventId}");
            }
            var hotel = JsonSerializer.Deserialize<Hotel>(eventRecord.Sns.Message);
            Console.WriteLine($"Deserializing hotel: {hotel?.Name} (ID: {hotel?.HotelId})");
            
            try
            {
                var indexResponse = await elasticClient.IndexDocumentAsync(hotel);
                Console.WriteLine($"Elasticsearch index result: Success={indexResponse.IsValid}, Index={indexResponse.Index}, Id={indexResponse.Id}");
                if (!indexResponse.IsValid)
                {
                    Console.WriteLine($"Elasticsearch error: {indexResponse.OriginalException?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error indexing to Elasticsearch: {ex.Message}");
            }
        }
    }
}

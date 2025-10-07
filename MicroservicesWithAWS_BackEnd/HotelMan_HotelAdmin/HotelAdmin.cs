using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Internal;
using Amazon.SimpleNotificationService.Model;
using AutoMapper;
using HotelMan_HotelAdmin.Models;
using HttpMultipartParser;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace HotelMan_HotelAdmin;

public class HotelAdmin
{
    public async Task<APIGatewayProxyResponse> ListHotels(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200,
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "OPTIONS,GET");
        response.Headers.Add("Content-Type", "application/json");

        try
        {
            var token = new JwtSecurityToken(request.QueryStringParameters["token"]);
            var userId = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize(new { message = "Invalid token" });
                return response;
            }

            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            var dbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
            using var dbContext = new DynamoDBContext(dbClient);

            var hotels = await dbContext.QueryAsync<Hotel>(userId).GetRemainingAsync();

            response.Body = JsonSerializer.Serialize(hotels);
        }
        catch (Exception e)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.Body = JsonSerializer.Serialize(new { message = "Failed to retrieve hotels", error = e.Message });
        }

        return response;
    }


    public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200,
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "OPTIONS,POST");

        var bodyContent = request.IsBase64Encoded ?
            Convert.FromBase64String(request.Body) :
            Encoding.UTF8.GetBytes(request.Body);

        using var memStream = new MemoryStream(bodyContent);
        var formData = MultipartFormDataParser.Parse(memStream);

        var hotelName = formData.GetParameterValue("hotelName");
        var hotelRating = formData.GetParameterValue("hotelRating");
        var hotelCity = formData.GetParameterValue("hotelCity");
        var hotelPrice = formData.GetParameterValue("hotelPrice");

        var file = formData.Files.FirstOrDefault();
        var fileName = file?.FileName;

        await using var fileContentStream = new MemoryStream();
        await file.Data.CopyToAsync(fileContentStream);
        // 复制完成后，流的位置在末尾，需要重置到开始位置
        fileContentStream.Position = 0;

        var userId = formData.GetParameterValue("userId");
        var idToken = formData.GetParameterValue("idToken");

        var token = new JwtSecurityToken(idToken);
        var group = token.Claims.FirstOrDefault(c => c.Type == "cognito:groups");
        if (group == null || group.Value != "Admin")
        {
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.Body = JsonSerializer.Serialize(new { message = "User is not authorized, Must be Admin group" });
            return response;
        }

        var region = Environment.GetEnvironmentVariable("AWS_REGION");
        var bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");

        var client = new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));
        var dbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));

        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = fileContentStream,
                AutoCloseStream = true
            });

            var hotel = new Hotel
            {
                UserId = userId,
                HotelId = Guid.NewGuid().ToString(),
                Name = hotelName,
                Rating = int.Parse(hotelRating),
                CityName = hotelCity,
                Price = int.Parse(hotelPrice),
                FileName = fileName
            };

            using var dbContext = new DynamoDBContext(dbClient);
            await dbContext.SaveAsync(hotel);

            //SNS topic publish
            var mapperConfig = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Hotel, HotelCreatedEvent>()
                    .ForMember(d => d.CreationTime, o => o.MapFrom(_ => DateTime.UtcNow)); // 建议用 UtcNow
                }, NullLoggerFactory.Instance); // ← 关键：传入 ILoggerFactory

            var mapper = mapperConfig.CreateMapper();
            var hotelCreatedEvent = mapper.Map<HotelCreatedEvent>(hotel);

            var snsClient = new AmazonSimpleNotificationServiceClient();
            var publishResponse = await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = Environment.GetEnvironmentVariable("snsTopicArn"),
                Message = JsonSerializer.Serialize(hotelCreatedEvent),
                Subject = "New Hotel Created"
            });

            

            response.Body = JsonSerializer.Serialize(new { message = "Hotel uploaded successfully", hotelId = hotel.HotelId });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.Body = JsonSerializer.Serialize(new { message = "Upload failed", error = e.Message });
        }
        return response;
    }
}

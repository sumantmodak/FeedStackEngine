using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FeedStackEngine.Functions.Functions;

public class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;

    public HealthFunction(ILogger<HealthFunction> logger)
    {
        _logger = logger;
    }

    [Function("Health")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check endpoint called");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync("{\"status\":\"ok\"}");

        return response;
    }
}

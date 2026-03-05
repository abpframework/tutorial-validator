using Validator.Core.Models.Assertions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Steps;
using Validator.Executor.Agent;
using Xunit;

namespace Validator.Core.Tests.Execution;

public class AgentResponseParserTests
{
    [Fact]
    public void ParseResponse_Fails_WhenHttpStatusDoesNotMatchExpectation()
    {
        var step = new ExpectationStep
        {
            StepId = 14,
            Assertions =
            [
                new HttpAssertion
                {
                    Url = "https://localhost:44364/Books",
                    Method = "GET",
                    ExpectsStatus = 200
                }
            ]
        };

        var tracker = new FunctionCallTracker();
        tracker.AddTrackedCall(new TrackedFunctionCall
        {
            PluginName = "Http",
            FunctionName = "Get",
            Url = "https://localhost:44364/Books",
            RawResult = """
                        HTTP Response:
                          Status: 404 NotFound
                        
                        Response Body:
                        Not Found
                        """,
            WasSuccessful = true
        });

        var (status, _, errorMessage) = AgentResponseParser.ParseResponse("ignored", step, tracker, DeveloperPersona.Senior);

        Assert.Equal(StepExecutionStatus.Failed, status);
        Assert.NotNull(errorMessage);
        Assert.Contains("expected status 200", errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("got 404", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseResponse_Passes_WhenHttpStatusMatchesExpectation()
    {
        var step = new ExpectationStep
        {
            StepId = 14,
            Assertions =
            [
                new HttpAssertion
                {
                    Url = "https://localhost:44364/Books",
                    Method = "GET",
                    ExpectsStatus = 200
                }
            ]
        };

        var tracker = new FunctionCallTracker();
        tracker.AddTrackedCall(new TrackedFunctionCall
        {
            PluginName = "Http",
            FunctionName = "Get",
            Url = "https://localhost:44364/Books",
            RawResult = """
                        HTTP Response:
                          Status: 200 OK
                        
                        Response Body:
                        Books page
                        """,
            WasSuccessful = true
        });

        var (status, details, errorMessage) = AgentResponseParser.ParseResponse("ignored", step, tracker, DeveloperPersona.Senior);

        Assert.Equal(StepExecutionStatus.Success, status);
        Assert.Contains("expectations met", details, StringComparison.OrdinalIgnoreCase);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ParseResponse_Fails_WhenExpectedContentIsMissing()
    {
        var step = new ExpectationStep
        {
            StepId = 14,
            Assertions =
            [
                new HttpAssertion
                {
                    Url = "https://localhost:44364/Books",
                    Method = "GET",
                    ExpectsStatus = 200,
                    ExpectsContent = "Book Store"
                }
            ]
        };

        var tracker = new FunctionCallTracker();
        tracker.AddTrackedCall(new TrackedFunctionCall
        {
            PluginName = "Http",
            FunctionName = "Get",
            Url = "https://localhost:44364/Books",
            RawResult = """
                        HTTP Response:
                          Status: 200 OK
                        
                        Response Body:
                        Books page loaded
                        """,
            WasSuccessful = true
        });

        var (status, _, errorMessage) = AgentResponseParser.ParseResponse("ignored", step, tracker, DeveloperPersona.Senior);

        Assert.Equal(StepExecutionStatus.Failed, status);
        Assert.NotNull(errorMessage);
        Assert.Contains("expected response content", errorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Aspire.TestProject;
using MySqlConnector;
using Polly;

public static class MySqlExtensions
{
    public static void MapMySqlApi(this WebApplication app)
    {
        app.MapGet("/mysql/verify", VerifyMySqlAsync);
    }

    private static async Task<IResult> VerifyMySqlAsync(MySqlConnection connection)
    {
        StringBuilder errorMessageBuilder = new();
        try
        {
            ResiliencePipeline pipeline = ResilienceUtils.GetDefaultResiliencePipelineBuilder<MySqlException>(args =>
            {
                errorMessageBuilder.AppendLine($"{Environment.NewLine}Service retry #{args.AttemptNumber} due to {args.Outcome.Exception}");
                return ValueTask.CompletedTask;
            }).Build();

            await pipeline.ExecuteAsync(async token => await connection.OpenAsync(token));

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT 1";
            var results = await command.ExecuteReaderAsync();

            return results.HasRows ? Results.Ok("Success!") : Results.Problem("Failed");
        }
        catch (Exception e)
        {
            return Results.Problem($"Error: {e}{Environment.NewLine}** Previous retries: {errorMessageBuilder}");
        }
    }
}

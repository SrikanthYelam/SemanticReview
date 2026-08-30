using AI.CodeReview.Application;
using AI.CodeReview.Application.Analysis;
using AI.CodeReview.Application.Diffing;
using AI.CodeReview.Infrastructure;
using AI.CodeReview.Infrastructure.Diffing;
using AI.CodeReview.Infrastructure.Git;
using AI.CodeReview.Infrastructure.Roslyn;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Code Review Assistant",
        Version = "v1",
        Description = "Accepts a unified C# git diff and returns AI-assisted + deterministic (Roslyn) code review findings."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IDiffParser, UnifiedDiffParser>();
builder.Services.AddSingleton<IStaticCodeAnalyzer, RoslynStaticAnalyzer>();
builder.Services.AddSemanticKernelServices(builder.Configuration);
builder.Services.AddGitDiffFetching();
builder.Services.AddScoped<ICodeReviewService, CodeReviewOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception handler: never leak exception details/stack traces to clients.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        if (feature is not null)
        {
            logger.LogError(feature.Error, "Unhandled exception processing {Path}", feature.Path);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

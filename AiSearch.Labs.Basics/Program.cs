using AiSearch.Labs.Basics.Components;
using AiSearch.Labs.Basics.Configuration;
using AiSearch.Labs.Basics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection(AzureStorageOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<AzureOpenAiSettings>(builder.Configuration.GetSection(AzureOpenAiSettings.SectionName));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<BlobStorageService>();
builder.Services.AddSingleton<AzureSearchAdminService>();
builder.Services.AddSingleton<AzureAiSearchService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
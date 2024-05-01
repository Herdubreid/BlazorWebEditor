using Fluxor;
using Luthetus.Common.RazorLib.BackgroundTasks.Models;
using Luthetus.Common.RazorLib.Installations.Models;
using TestServer.Components;
using Luthetus.TextEditor.RazorLib.Installations.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var hostingInformation = new LuthetusHostingInformation(
    LuthetusHostingKind.ServerSide,
    new BackgroundTaskService());

builder.Services
    .AddLuthetusTextEditor(hostingInformation)
    .AddFluxor(options => options.ScanAssemblies(
        typeof(LuthetusCommonConfig).Assembly,
        typeof(LuthetusTextEditorConfig).Assembly));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;
using RepairDesk.Core;
using RepairDesk.Data;

namespace RepairDesk.Api;

public static class ApiHost
{
    public static WebApplication Create(DatabaseSettings settings, string webRoot, string url = "http://127.0.0.1:0")
    {
        var address = new Uri(url);
        if (address.Scheme != "http" || address.Host != "127.0.0.1") throw new ArgumentException("API разрешено слушать только 127.0.0.1.", nameof(url));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot, ContentRootPath = AppContext.BaseDirectory, Args = [] });
        builder.WebHost.UseUrls(url).ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024);
        builder.Services.AddSingleton(settings);
        builder.Services.AddScoped<Repository>();
        builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "RepairDesk.Studio";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limit => { limit.PermitLimit = 12; limit.Window = TimeSpan.FromMinutes(1); limit.QueueLimit = 0; }).RejectionStatusCode = 429);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    var origin = context.Request.Headers.Origin.ToString();
                    if (context.Request.Headers["X-RepairDesk-Client"] != "studio" || origin.Length > 0 && origin != $"{context.Request.Scheme}://{context.Request.Host}")
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new { title = "Источник запроса не разрешён." });
                        return;
                    }
                }
            }
            try { await next(context); }
            catch (BusinessException error) { await Problem(context, 409, error.Message); }
            catch (BadHttpRequestException) { await Problem(context, 400, "Проверьте формат и обязательные поля запроса."); }
            catch (MySqlException error) when (error.Number is 1062 or 1451 or 1452) { await Problem(context, 409, "Запись уже существует или используется в связанных документах."); }
            catch (Exception error)
            {
                app.Logger.LogError(error, "Request {Trace} failed", context.TraceIdentifier);
                await Problem(context, 500, $"Не удалось выполнить действие. Код обращения: {context.TraceIdentifier}");
            }
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/api/health", () => Results.Ok(new { name = "RepairDesk Studio", version = "2.0", storage = "MySQL / MariaDB" }));
        app.MapPost("/api/auth/login", async (LoginRequest request, Repository repository, HttpContext context) =>
        {
            Staff user;
            try { user = await repository.LoginAsync(request.Login, request.Password); }
            catch (BusinessException) { return Results.Problem(statusCode: 401, title: "Неверный логин или пароль."); }
            var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Name), new(ClaimTypes.Role, user.Role)], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(user);
        }).RequireRateLimiting("login");
        var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter(async (invocation, next) =>
        {
            var id = invocation.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(id, out var staffId)) return Results.Unauthorized();
            try { await invocation.HttpContext.RequestServices.GetRequiredService<Repository>().ResumeSessionAsync(staffId); }
            catch (BusinessException) { await invocation.HttpContext.SignOutAsync(); return Results.Unauthorized(); }
            return await next(invocation);
        });
        api.MapGet("/auth/me", (Repository repository) => repository.Actor);
        api.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(); return Results.NoContent(); });
        api.MapPut("/auth/password", async (PasswordRequest request, Repository repository) => { await repository.ChangePasswordAsync(request.OldPassword, request.NewPassword); return Results.NoContent(); });
        api.MapGet("/reference", () => new { statuses = OrderRules.Statuses, roles = OrderRules.Roles, tests = DiagnosticTests.Items });
        api.MapGet("/staff", (Repository repository) => repository.StaffAsync());
        api.MapPost("/staff", async (StaffRequest request, Repository repository) => { await repository.AddStaffAsync(request.Name, request.Login, request.Password, request.Role); return Results.NoContent(); });
        api.MapGet("/clients", (Repository repository) => repository.ClientsAsync());
        api.MapGet("/clients/{id:int}", async (int id, Repository repository) => (await repository.ClientsAsync()).FirstOrDefault(client => client.Id == id) is { } client ? Results.Ok(client) : Results.NotFound());
        api.MapPost("/clients", async (ClientRequest request, Repository repository) => { var id = await repository.SaveClientAsync(null, request.Name, request.Phone, request.Email, request.Notes); return Results.Created($"/api/clients/{id}", new { id }); });
        api.MapPut("/clients/{id:int}", async (int id, ClientRequest request, Repository repository) => { await repository.SaveClientAsync(id, request.Name, request.Phone, request.Email, request.Notes); return Results.NoContent(); });
        api.MapDelete("/clients/{id:int}", async (int id, Repository repository) => { await repository.DeleteClientAsync(id); return Results.NoContent(); });
        api.MapGet("/parts", (Repository repository) => repository.PartsAsync());
        api.MapPost("/parts", async (Part request, Repository repository) => { await repository.SavePartAsync(request, true); return Results.StatusCode(201); });
        api.MapPut("/parts/{id:int}", async (int id, Part request, Repository repository) => { await repository.SavePartAsync(request with { Id = id }, false); return Results.NoContent(); });
        api.MapDelete("/parts/{id:int}", async (int id, Repository repository) => { await repository.DeletePartAsync(id); return Results.NoContent(); });
        api.MapPost("/parts/{id:int}/movements", async (int id, StockRequest request, Repository repository) => { await repository.AdjustStockAsync(id, request.Quantity, request.Reason); return Results.NoContent(); });
        api.MapGet("/stock-movements", (Repository repository) => repository.StockMovesAsync());
        api.MapGet("/services", (Repository repository) => repository.ServicesAsync());
        api.MapPost("/services", async (ServiceRequest request, Repository repository) => { var id = await repository.SaveServiceAsync(null, request.Name, request.Price); return Results.Created($"/api/services/{id}", new { id }); });
        api.MapPut("/services/{id:int}", async (int id, ServiceRequest request, Repository repository) => { await repository.SaveServiceAsync(id, request.Name, request.Price); return Results.NoContent(); });
        api.MapDelete("/services/{id:int}", async (int id, Repository repository) => { await repository.DeleteServiceAsync(id); return Results.NoContent(); });
        MapOrders(api);
        MapBuilds(api);
        return app;
    }

    private static void MapOrders(RouteGroupBuilder api)
    {
        api.MapGet("/orders", (Repository repository) => repository.OrdersAsync());
        api.MapPost("/orders", async (NewOrder request, Repository repository) => { var id = await repository.CreateOrderAsync(request); return Results.Created($"/api/orders/{id}", new { id }); });
        api.MapGet("/orders/{id:int}", async (int id, Repository repository) =>
        {
            var order = await repository.OrderAsync(id);
            return Results.Ok(new { order, lines = await repository.LinesAsync(id), events = await repository.EventsAsync(id), estimates = await repository.EstimatesAsync(id), diagnostics = await repository.DiagnosticsAsync(id), nextStatuses = OrderRules.NextStatuses(order.Status) });
        });
        api.MapPut("/orders/{id:int}", async (int id, OrderUpdate request, Repository repository) => { var order = await Snapshot(repository, id, request.Version); await repository.UpdateOrderAsync(order, request.Diagnosis, request.DueAt, request.TechnicianId, request.Status); return Results.NoContent(); });
        api.MapPost("/orders/{id:int}/notes", async (int id, NoteRequest request, Repository repository) => { await repository.AddNoteAsync(await Snapshot(repository, id, request.Version), request.Message); return Results.NoContent(); });
        api.MapPost("/orders/{id:int}/payments", async (int id, PaymentRequest request, Repository repository) => { await repository.PayAsync(await Snapshot(repository, id, request.Version), request.Amount); return Results.NoContent(); });
        api.MapPost("/orders/{id:int}/lines", async (int id, LineRequest request, Repository repository) =>
        {
            if (request.PartId.HasValue == request.ServiceId.HasValue) throw new BusinessException("Выберите одну деталь или услугу.");
            var service = request.ServiceId is { } serviceId ? (await repository.ServicesAsync()).FirstOrDefault(item => item.Id == serviceId) : null;
            await repository.AddLineAsync(await Snapshot(repository, id, request.Version), request.PartId, service, request.Quantity);
            return Results.NoContent();
        });
        api.MapDelete("/orders/{id:int}/lines/{lineId:int}", async (int id, int lineId, int version, Repository repository) =>
        {
            var order = await Snapshot(repository, id, version);
            var line = (await repository.LinesAsync(id)).FirstOrDefault(item => item.Id == lineId) ?? throw new BusinessException("Строка не найдена.");
            await repository.RemoveLineAsync(order, line); return Results.NoContent();
        });
        api.MapPost("/orders/{id:int}/estimates", async (int id, EstimateRequest request, Repository repository) => { var estimateId = await repository.CreateEstimateAsync(await Snapshot(repository, id, request.Version), request.Note, request.Items); return Results.Created($"/api/orders/{id}/estimates/{estimateId}", new { id = estimateId }); });
        api.MapGet("/orders/{id:int}/estimates/{estimateId:int}", (int id, int estimateId, Repository repository) => repository.EstimateLinesAsync(id, estimateId));
        api.MapPut("/orders/{id:int}/estimates/{estimateId:int}", async (int id, int estimateId, DecisionRequest request, Repository repository) => { await repository.DecideEstimateAsync(await Snapshot(repository, id, request.Version), estimateId, request.Status, request.ApprovedBy); return Results.NoContent(); });
        api.MapPut("/orders/{id:int}/diagnostics/{key}", async (int id, string key, DiagnosticRequest request, Repository repository) => { await repository.SaveDiagnosticAsync(await Snapshot(repository, id, request.Version), key, request.Result, request.Note); return Results.NoContent(); });
    }

    private static async Task<RepairOrder> Snapshot(Repository repository, int id, int version)
    {
        var order = await repository.OrderAsync(id);
        if (order.Version != version) throw new BusinessException("Заказ изменён. Обновите его, чтобы не потерять чужие изменения.");
        return order;
    }

    private static void MapBuilds(RouteGroupBuilder api)
    {
        api.MapGet("/games", (Repository repository) => repository.GamesAsync());
        api.MapGet("/builds", (Repository repository) => repository.BuildsAsync());
        api.MapGet("/builds/{id:int}/parts", (int id, Repository repository) => repository.BuildPartIdsAsync(id));
        api.MapDelete("/builds/{id:int}", async (int id, Repository repository) => { await repository.DeleteBuildAsync(id); return Results.NoContent(); });
        api.MapPost("/builds", async (BuildRequest request, Repository repository) => { await repository.SaveBuildAsync(request.Name, request.ClientId, BuildEngine.Select(await repository.PartsAsync(), request.PartIds)); return Results.StatusCode(201); });
        api.MapPost("/builds/order", async (BuildRequest request, Repository repository) => { var id = await repository.CreateBuildOrderAsync(request.ClientId ?? 0, BuildEngine.Select(await repository.PartsAsync(), request.PartIds), request.Name); return Results.Created($"/api/orders/{id}", new { id }); });
        api.MapPost("/builds/evaluate", async (BuildRequest request, Repository repository) => await BuildEngine.EvaluateAsync(repository, request.PartIds, request.Width));
        api.MapPost("/builds/recommend", (BudgetRequest request, Repository repository) => BuildEngine.RecommendAsync(repository, request));
        api.MapPost("/builds/compare", async (CompareRequest request, Repository repository) => new { current = await BuildEngine.EvaluateAsync(repository, request.Current, request.Width), proposed = await BuildEngine.EvaluateAsync(repository, request.Proposed, request.Width) });
    }

    private static async Task Problem(HttpContext context, int status, string title)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { type = "about:blank", title, status, traceId = context.TraceIdentifier });
    }
}

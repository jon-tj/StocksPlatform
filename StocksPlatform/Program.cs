using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StocksPlatform.Data;
using StocksPlatform.Services;
using StocksPlatform.Services.Seeding;
using StocksPlatform.Services.CompanyNews;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<PollWeekService>();
builder.Services.AddScoped<FractionService>();
builder.Services.AddHttpClient<StocksPlatform.Services.PriceServices.E24PriceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<StocksPlatform.Services.PriceServices.E24PriceService>();
builder.Services.AddScoped<StocksPlatform.Services.PriceServices.IAssetPriceProvider>(
    sp => sp.GetRequiredService<StocksPlatform.Services.PriceServices.E24PriceService>());
builder.Services.AddScoped<AssetPriceService>();
builder.Services.AddHttpClient<StocksPlatform.Services.PriceServices.YahooPriceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<StocksPlatform.Services.PriceServices.YahooPriceService>();
builder.Services.AddHttpClient<StocksPlatform.Services.FundServices.SpareBank1FundService>();
builder.Services.AddHttpClient<StocksPlatform.Services.FundServices.HanEtfFundService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<StocksPlatform.Services.FundServices.IFundHoldingsProvider>(
    sp => sp.GetRequiredService<StocksPlatform.Services.FundServices.SpareBank1FundService>());
builder.Services.AddScoped<StocksPlatform.Services.FundServices.IFundHoldingsProvider>(
    sp => sp.GetRequiredService<StocksPlatform.Services.FundServices.HanEtfFundService>());
builder.Services.AddScoped<FundInstitutionalService>();
builder.Services.AddSingleton<StocksPlatform.Services.Analysis.OnnxPriceModelRegistry>();
builder.Services.AddScoped<StocksPlatform.Services.Analysis.PatternDeltaService>();
builder.Services.AddScoped<StocksPlatform.Services.Analysis.AnalysisService>();
builder.Services.AddHttpClient<PublicSentimentService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<OrderBookService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<OrderBookPollingService>();

// Company-specific news feeds
builder.Services.AddHttpClient("CompanyNews", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
});
builder.Services.AddSingleton<ICompanyNewsFeed, EquinorNewsFeed>();
builder.Services.AddSingleton<CompanyNewsFeedService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>();
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await CurrencyPairSeeder.SeedAsync(db);
    await NordnetTickerSeeder.SeedAsync(db);
    await SpFiveHundredSeeder.SeedAsync(db);
}

app.Run();


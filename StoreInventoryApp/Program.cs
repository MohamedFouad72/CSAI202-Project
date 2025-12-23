using StoreInventoryApp.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register DbHelper as a singleton or scoped service
builder.Services.AddScoped<DbHelper>();


builder.Services.AddHttpContextAccessor();

//[cite_start]// [cite: 298] Add Session Support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register Configuration for DbHelper
builder.Configuration.AddJsonFile("appsettings.json");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

//[cite_start]// [cite: 298] Enable Session Middleware
app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
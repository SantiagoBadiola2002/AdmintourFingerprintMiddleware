using AdmintourFingerprintMiddleware.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ---------------- SERVICIOS ----------------

// API + UI MVC
builder.Services.AddControllersWithViews();

// Servicio lector huella (hardware único)
builder.Services.AddSingleton<FingerprintService>();

// Cliente API externa
builder.Services.AddHttpClient<AdmintourApiClient>();

// CORS abierto
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- PIPELINE ----------------

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseCors();

app.UseAuthorization();

// ---------------- RUTAS ----------------

// Ruta raíz → UI
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Home", action = "Index" }
);

// API
app.MapControllers();

// UI MVC normal
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();

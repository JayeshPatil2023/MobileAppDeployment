using MobileAppDeployment.Services;
using MobileAppDeployment.Services.GitHub;
using MobileAppDeployment.Repositories;
using MobileAppDeployment.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository & Service registration
builder.Services.AddScoped<IAppDeploymentRepository, AppDeploymentRepository>();
builder.Services.AddScoped<IAppDeploymentService,AppDeploymentService>();
builder.Services.AddScoped<IAssetStorageService, AssetStorageService>();
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.Configure<RepoMergeOptions>(builder.Configuration.GetSection($"{GitHubOptions.SectionName}:RepoMerge"));
builder.Services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
builder.Services.AddSingleton<IRepoMergeJobStore, RepoMergeJobStore>();
builder.Services.AddSingleton<IRepoMergeService, RepoMergeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AppDeployment}/{action=Index}/{id?}");

app.Run();

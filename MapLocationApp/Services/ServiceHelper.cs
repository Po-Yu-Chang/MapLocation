using Microsoft.Extensions.DependencyInjection;

namespace MapLocationApp.Services
{
    public static class ServiceHelper
    {
        public static T GetService<T>() where T : class
        {
            return MauiProgram.Services.GetRequiredService<T>();
        }

        public static T? GetOptionalService<T>() where T : class
        {
            return MauiProgram.Services.GetService<T>();
        }
    }
}
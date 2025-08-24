using ArticleGenerator.Home.Endpoints;
using ArticleGenerator.Products.Endpoints;

namespace ArticleGenerator
{
    public static class Endpoints
    {
        public static void MapEndpoints(this WebApplication app)
        {
            var endpoints = app.MapGroup("");

            endpoints.MapHomeEndpoints();
            endpoints.MapProductsEndpoints();
        }

        private static void MapHomeEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("/")
                .WithTags("Home");

            endpoints.MapPublicGroup()
                .MapEndpoint<GetForm>();
        }

        private static void MapProductsEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("/products")
                .WithTags("Products");

            endpoints.MapPublicGroup()
                .MapEndpoint<UploadProducts>()
                .MapEndpoint<GetProducts>()
                .MapEndpoint<DownloadProducts>();
        }

        private static RouteGroupBuilder MapPublicGroup(this IEndpointRouteBuilder app, string? prefix = null)
        {
            return app.MapGroup(prefix ?? string.Empty)
                .AllowAnonymous();
        }

        private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app) where TEndpoint : IEndpoint
        {
            TEndpoint.Map(app);
            return app;
        }
    }
}

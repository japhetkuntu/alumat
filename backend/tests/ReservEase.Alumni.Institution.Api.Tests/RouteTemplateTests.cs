using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ReservEase.Alumni.Institution.Api.Controllers;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>
/// "action", "controller", "area" and "page" are reserved route parameter names: an endpoint whose template uses one of them
/// never matches a request, and nothing fails until someone calls it (the service tests do not go through routing).
/// </summary>
public class RouteTemplateTests
{
    private static readonly string[] Reserved = ["action", "controller", "area", "page", "handler"];

    public static IEnumerable<object[]> Endpoints() =>
        typeof(DefaultController).Assembly.GetTypes().Where(t => typeof(DefaultController).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => new object[] { $"{m.DeclaringType!.Name}.{m.Name}", a.Template ?? "" }));

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void No_endpoint_route_uses_a_reserved_parameter_name(string endpoint, string template)
    {
        var parameters = System.Text.RegularExpressions.Regex.Matches(template, @"\{\*?([A-Za-z0-9_]+)[^}]*\}").Select(m => m.Groups[1].Value);

        Assert.DoesNotContain(parameters, p => Reserved.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_ambassador_task_endpoint_is_routed_by_id_and_outcome()
    {
        var method = typeof(AmbassadorController).GetMethod(nameof(AmbassadorController.UpdateTask))!;

        Assert.Equal("tasks/{id}/{outcome}", method.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal(["id", "outcome"], method.GetParameters().Select(p => p.Name));
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using ApexPost.Api.DTOs;

namespace ApexPost.Api.Routing;

/// <summary>
/// Route constraint <c>{trackingNumber:trackingNumber}</c>: the segment must look like APX-YYYYMMDD.
/// A request for any other shape never reaches a controller and gets a 404 problem response.
/// The pattern is the same one the request DTOs validate, so the rule is defined once.
/// </summary>
public sealed class TrackingNumberRouteConstraint : IRouteConstraint
{
    public const string Name = "trackingNumber";

    private static readonly Regex Pattern = new(
        TrackingNumberFormat.Pattern,
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value) || value is null)
        {
            return false;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return !string.IsNullOrEmpty(text) && Pattern.IsMatch(text);
    }
}

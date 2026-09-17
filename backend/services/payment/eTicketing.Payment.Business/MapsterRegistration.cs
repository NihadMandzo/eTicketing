using System.Runtime.CompilerServices;
using Mapster;

namespace eTicketing.Payment.Business;

/// <summary>
/// Registers every <see cref="IRegister"/> mapping config in this assembly into Mapster's global
/// config as soon as the assembly loads — via <see cref="ModuleInitializerAttribute"/>, not a call
/// from API startup, so <c>.Adapt&lt;T&gt;()</c> works in unit tests that build services directly.
/// Identical to eTicketing.Catalog.Business/MapsterRegistration.cs; this service was the last one
/// without Mapster at all, which is why its response mapping was hand-written.
/// </summary>
internal static class MapsterRegistration
{
    [ModuleInitializer]
    internal static void Initialize() => TypeAdapterConfig.GlobalSettings.Scan(typeof(MapsterRegistration).Assembly);
}

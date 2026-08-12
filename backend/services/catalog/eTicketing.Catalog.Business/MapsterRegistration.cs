using System.Runtime.CompilerServices;
using Mapster;

namespace eTicketing.Catalog.Business;

/// <summary>
/// Registers every <see cref="IRegister"/> mapping config in this assembly (CategoryMappingConfig,
/// EventMappingConfig, ...) into Mapster's global config as soon as the assembly loads — via
/// <see cref="ModuleInitializerAttribute"/>, not a call from API startup. This way
/// <c>.Adapt&lt;T&gt;()</c> works correctly wherever this assembly is used, including unit
/// tests that build services directly without going through CatalogServiceCollectionExtensions.
/// Mirrors eTicketing.Identity.Business/MapsterRegistration.cs.
/// </summary>
internal static class MapsterRegistration
{
    [ModuleInitializer]
    internal static void Initialize() => TypeAdapterConfig.GlobalSettings.Scan(typeof(MapsterRegistration).Assembly);
}

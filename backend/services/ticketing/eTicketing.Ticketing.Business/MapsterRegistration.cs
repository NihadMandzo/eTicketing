using System.Runtime.CompilerServices;
using Mapster;

namespace eTicketing.Ticketing.Business;

/// <summary>
/// Registers every <see cref="IRegister"/> mapping config in this assembly (SectorMappingConfig, ...)
/// into Mapster's global config as soon as the assembly loads — via
/// <see cref="ModuleInitializerAttribute"/>, not a call from API startup. Mirrors
/// eTicketing.Catalog.Business/MapsterRegistration.cs.
/// </summary>
internal static class MapsterRegistration
{
    [ModuleInitializer]
    internal static void Initialize() => TypeAdapterConfig.GlobalSettings.Scan(typeof(MapsterRegistration).Assembly);
}

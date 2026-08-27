/// Same shape for create and update — mirrors the backend's UpsertGateDeviceRequest.
///
/// [allSectors] and [sectorIds] are mutually exclusive: when allSectors is true the list is sent
/// empty and the gate admits every sector of the product; otherwise the list must name at least
/// one. The backend rejects a request that sets neither, and additionally rejects any sector that
/// does not belong to [productId].
class GateDeviceUpsertRequest {
  final String productId;
  final String name;
  final bool allSectors;
  final List<String> sectorIds;
  final bool isActive;

  const GateDeviceUpsertRequest({
    required this.productId,
    required this.name,
    required this.allSectors,
    required this.sectorIds,
    this.isActive = true,
  });

  Map<String, dynamic> toJson() => {
        'ProductId': productId,
        'Name': name,
        'AllSectors': allSectors,
        'SectorIds': allSectors ? <String>[] : sectorIds,
        'IsActive': isActive,
      };
}

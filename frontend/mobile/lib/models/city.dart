import 'api_enum.dart';

/// Mirrors `eTicketing.Contracts.Persistence.City` — the fixed set of BiH cities a Product can be
/// located in. Same ordinal/name convention as TicketingMode above.
enum City { sarajevo, mostar, banjaLuka, tuzla, zenica, bihac, brcko, trebinje }

/// Declaration order must match the backend enum exactly — the wire value is the ordinal.
const cityNames = ['Sarajevo', 'Mostar', 'BanjaLuka', 'Tuzla', 'Zenica', 'Bihac', 'Brcko', 'Trebinje'];

City cityFromJson(dynamic value) => switch (enumNameFromJson(value, cityNames, 'Sarajevo')) {
      'Mostar' => City.mostar,
      'BanjaLuka' => City.banjaLuka,
      'Tuzla' => City.tuzla,
      'Zenica' => City.zenica,
      'Bihac' => City.bihac,
      'Brcko' => City.brcko,
      'Trebinje' => City.trebinje,
      _ => City.sarajevo,
    };

/// The wire name for a City value — what the backend expects back as a `city=` query param.
String cityToJson(City city) => cityNames[city.index];

/// Bosnian label for UI display (dropdowns, location rows).
String cityLabel(City city) => switch (city) {
      City.sarajevo => 'Sarajevo',
      City.mostar => 'Mostar',
      City.banjaLuka => 'Banja Luka',
      City.tuzla => 'Tuzla',
      City.zenica => 'Zenica',
      City.bihac => 'Bihać',
      City.brcko => 'Brčko',
      City.trebinje => 'Trebinje',
    };

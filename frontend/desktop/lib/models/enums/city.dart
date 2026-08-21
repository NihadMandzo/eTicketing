/// Mirrors the backend's `eTicketing.Contracts.Persistence.City` ordinals exactly (Sarajevo(0),
/// Mostar(1), BanjaLuka(2), Tuzla(3), Zenica(4), Bihac(5), Brcko(6), Trebinje(7) — see
/// .claude/rules/01-domain.md). Fixed set of BiH cities a Product can be located in; raw-numeric
/// JSON convention, same as TicketingMode/RoleType.
enum City {
  sarajevo(0),
  mostar(1),
  banjaLuka(2),
  tuzla(3),
  zenica(4),
  bihac(5),
  brcko(6),
  trebinje(7);

  final int value;
  const City(this.value);

  static City fromValue(int value) => City.values.firstWhere(
        (c) => c.value == value,
        orElse: () => City.sarajevo,
      );

  /// Bosnian label for UI display (dropdowns).
  String get label => switch (this) {
        City.sarajevo => 'Sarajevo',
        City.mostar => 'Mostar',
        City.banjaLuka => 'Banja Luka',
        City.tuzla => 'Tuzla',
        City.zenica => 'Zenica',
        City.bihac => 'Bihać',
        City.brcko => 'Brčko',
        City.trebinje => 'Trebinje',
      };
}

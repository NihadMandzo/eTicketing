class HoldSectorRequest {
  final int quantity;
  /// DailyEntry only — `yyyy-MM-dd`, must fall within the Sector's PeriodYear/PeriodMonth.
  final String? date;

  const HoldSectorRequest({required this.quantity, this.date});

  Map<String, dynamic> toJson() => {
        'quantity': quantity,
        if (date != null) 'date': date,
      };
}

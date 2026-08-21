import 'package:flutter/material.dart';

import '../../theme/app_colors.dart';

/// Shared layout for every card-grid screen in the app (Categories, Products,
/// Organizations, Users, organization detail's Products/Users tabs): a
/// scrollable content region (header, search/filters, count, card grid) with
/// the [pagination] bar pinned to the bottom of the viewport instead of
/// scrolling away with everything else — so the pager stays reachable no
/// matter how far down a long result list the user has scrolled.
///
/// Each screen still owns its own scroll padding/spacing for [content]; this
/// widget only owns the split between the scrollable region and the fixed
/// footer strip.
class PaginatedScreenBody extends StatelessWidget {
  final Widget content;
  final Widget pagination;
  final EdgeInsetsGeometry contentPadding;

  const PaginatedScreenBody({
    super.key,
    required this.content,
    required this.pagination,
    this.contentPadding = const EdgeInsets.all(24),
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Column(
      children: [
        Expanded(
          child: SingleChildScrollView(
            padding: contentPadding,
            child: content,
          ),
        ),
        Container(
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 24),
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
            border: Border(
              top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
            ),
          ),
          child: pagination,
        ),
      ],
    );
  }
}

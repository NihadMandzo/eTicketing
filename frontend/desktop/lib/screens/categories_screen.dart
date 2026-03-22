import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/category_response.dart';
import '../providers/category_provider.dart';
import '../main.dart';

class CategoriesScreen extends StatefulWidget {
  const CategoriesScreen({super.key});

  @override
  State<CategoriesScreen> createState() => _CategoriesScreenState();
}

class _CategoriesScreenState extends State<CategoriesScreen> {
  final _searchController = TextEditingController();
  final CategoryProvider _provider = CategoryProvider();
  
  List<CategoryResponse> _categories = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final result = await _provider.getAll(fromJson: CategoryResponse.fromJson);
      if (mounted) {
        setState(() {
          _categories = result.items;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isLoading = false);
        handleApiError(e);
      }
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ── Page Header ──────────────────────────────────────────────
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: const [
                    Text(
                      'Kategorije',
                      style: TextStyle(
                        fontSize: 30, // text-3xl
                        fontWeight: FontWeight.w700, // font-bold
                        color: Color(0xFF111827), // text-gray-900
                        height: 1.2,
                      ),
                    ),
                    SizedBox(height: 8), // mb-2
                    Text(
                      'Upravljajte kategorijama i klasifikacijama događaja',
                      style: TextStyle(
                        fontSize: 16,
                        color: Color(0xFF4B5563), // text-gray-600
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              // Gradient Button
              Container(
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF0D7C66), Color(0xFF0a6b57)],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFF0D7C66).withValues(alpha: 0.2),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onHover: (val) {}, // to handle hover scale slightly if preferred via AnimatedScale in more complex widget
                    onTap: () {
                      // onAddCategory
                    },
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(
                        children: const [
                          Icon(LucideIcons.plus, color: Colors.white, size: 20),
                          SizedBox(width: 8),
                          Text(
                            'Dodaj Kategoriju',
                            style: TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
          
          const SizedBox(height: 24), // space-y-6

          // ── Search and Filters ───────────────────────────────────────
          Row(
            children: [
              Expanded(
                child: Container(
                  height: 48,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    border: Border.all(color: const Color(0xFFD1D5DB)),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: TextField(
                    controller: _searchController,
                    decoration: const InputDecoration(
                      hintText: 'Pretražite kategorije...',
                      hintStyle: TextStyle(color: Color(0xFF6B7280)),
                      prefixIcon: Icon(LucideIcons.search, color: Color(0xFF9CA3AF), size: 20),
                      border: InputBorder.none,
                      contentPadding: EdgeInsets.symmetric(vertical: 14),
                    ),
                    style: const TextStyle(color: Color(0xFF111827)),
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Categories Grid ──────────────────────────────────────────
          Expanded(
            child: _isLoading 
              ? const Center(child: CircularProgressIndicator(color: Color(0xFF0D7C66)))
              : LayoutBuilder(
              builder: (context, constraints) {
                int crossAxisCount = 1;
                if (constraints.maxWidth >= 1280) { // xl
                  crossAxisCount = 4;
                } else if (constraints.maxWidth >= 1024) { // lg
                  crossAxisCount = 3;
                } else if (constraints.maxWidth >= 768) { // md
                  crossAxisCount = 2;
                }
                
                return GridView.builder(
                  gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: crossAxisCount,
                    crossAxisSpacing: 24,
                    mainAxisSpacing: 24,
                    childAspectRatio: 1.1, 
                  ),
                  itemCount: _categories.length,
                  itemBuilder: (context, index) {
                    final category = _categories[index];
                    return _CategoryCard(category: category);
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _CategoryCard extends StatefulWidget {
  final CategoryResponse category;

  const _CategoryCard({required this.category});

  @override
  State<_CategoryCard> createState() => _CategoryCardState();
}

class _CategoryCardState extends State<_CategoryCard> {
  bool _isHovering = false;

  void _onEnter(PointerEvent details) {
    setState(() => _isHovering = true);
  }

  void _onExit(PointerEvent details) {
    setState(() => _isHovering = false);
  }

  IconData _getIcon(String iconUrl) {
    switch (iconUrl.toLowerCase()) {
      case 'music': return LucideIcons.music;
      case 'trophy': return LucideIcons.trophy;
      case 'book_open': return LucideIcons.bookOpen;
      default: return LucideIcons.layoutGrid;
    }
  }
  
  Color _getColor(String iconUrl) {
    switch (iconUrl.toLowerCase()) {
      case 'music': return const Color(0xFF8B5CF6); // purple-500
      case 'trophy': return const Color(0xFFF59E0B); // amber-500
      case 'book_open': return const Color(0xFF3B82F6); // blue-500
      default: return const Color(0xFF0D7C66); // primary
    }
  }

  @override
  Widget build(BuildContext context) {
    final color = _getColor(widget.category.iconUrl);
    
    return MouseRegion(
      onEnter: _onEnter,
      onExit: _onExit,
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: () {
          // onViewCategory
        },
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: _isHovering ? const Color(0xFF0D7C66) : const Color(0xFFE5E7EB),
            ),
            boxShadow: _isHovering
                ? [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.1),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    )
                  ]
                : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // ── Top row: Icon and More menu ──
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.2), // color20
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Center(
                      child: Icon(_getIcon(widget.category.iconUrl), color: color, size: 24),
                    ),
                  ),
                  AnimatedOpacity(
                    opacity: _isHovering ? 1.0 : 0.0,
                    duration: const Duration(milliseconds: 200),
                    child: Material(
                      color: Colors.transparent,
                      child: IconButton(
                        icon: const Icon(LucideIcons.trash2, size: 20, color: Color(0xFF9CA3AF)),
                        onPressed: () {},
                        hoverColor: const Color(0xFFF3F4F6),
                        splashRadius: 24,
                        constraints: const BoxConstraints(),
                        padding: const EdgeInsets.all(8),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              
              // ── Title ──
              Text(
                widget.category.name,
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 8),
              
              // ── Description ──
              Expanded(
                child: Text(
                  widget.category.description,
                  style: const TextStyle(
                    fontSize: 14,
                    color: Color(0xFF4B5563),
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              
              // ── Footer ──
              Container(
                margin: const EdgeInsets.only(top: 16),
                padding: const EdgeInsets.only(top: 16),
                decoration: const BoxDecoration(
                  border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: const [
                        Text(
                          '0', // category.events
                          style: TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF111827),
                            height: 1.0,
                          ),
                        ),
                        SizedBox(width: 8),
                        Text(
                          'događaja',
                          style: TextStyle(
                            fontSize: 14,
                            color: Color(0xFF4B5563),
                          ),
                        ),
                      ],
                    ),
                    AnimatedOpacity(
                      opacity: _isHovering ? 1.0 : 0.0,
                      duration: const Duration(milliseconds: 200),
                      child: Row(
                        children: [
                          Material(
                            color: Colors.transparent,
                            child: IconButton(
                              icon: const Icon(LucideIcons.pencil, size: 16, color: Color(0xFF4B5563)),
                              onPressed: () {},
                              hoverColor: const Color(0xFFF3F4F6),
                              splashRadius: 20,
                              constraints: const BoxConstraints(),
                              padding: const EdgeInsets.all(8),
                            ),
                          ),
                          Material(
                            color: Colors.transparent,
                            child: IconButton(
                              icon: const Icon(LucideIcons.trash2, size: 16, color: Color(0xFF4B5563)),
                              onPressed: () {},
                              hoverColor: const Color(0xFFF3F4F6),
                              splashRadius: 20,
                              constraints: const BoxConstraints(),
                              padding: const EdgeInsets.all(8),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

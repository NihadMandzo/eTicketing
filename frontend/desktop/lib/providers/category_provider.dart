import '../models/responses/category_response.dart';
import '../models/responses/paged_result.dart';
import '../models/search_objects/base_search_object.dart';
import 'base_provider.dart';

class CategoryProvider extends BaseProvider<CategoryResponse> {
  CategoryProvider() : super('Categories');

}

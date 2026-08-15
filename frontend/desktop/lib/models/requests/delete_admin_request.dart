/// Body for DELETE /admins/{id}. Reason/RecipientEmail are only required by
/// the backend when the target is an OrganizationAdmin (see
/// AdminService.DeleteAsync) — always send this, even with both fields
/// null, since the route no longer accepts a bodyless DELETE.
class DeleteAdminRequest {
  final String? reason;
  final String? recipientEmail;

  const DeleteAdminRequest({this.reason, this.recipientEmail});

  Map<String, dynamic> toJson() => {
        if (reason != null && reason!.isNotEmpty) 'Reason': reason,
        if (recipientEmail != null && recipientEmail!.isNotEmpty) 'RecipientEmail': recipientEmail,
      };
}

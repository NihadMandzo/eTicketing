namespace eTicketing.Model.Responses;

public class OrganizationDetailResponse : OrganizationResponse
{
    public List<UserResponse> Administrators { get; set; } = new();
}

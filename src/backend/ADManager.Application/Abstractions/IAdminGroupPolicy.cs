namespace ADManager.Application.Abstractions;

public interface IAdminGroupPolicy
{
    bool IsAdmin(IReadOnlyList<string> groups);
    bool IsViewer(IReadOnlyList<string> groups);
}

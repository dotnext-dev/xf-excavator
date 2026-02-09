using System.Collections.Generic;
using System.Threading.Tasks;
using MigrationToolkit.Shared.Models;

namespace MigrationToolkit.Shared
{
    public interface ISpyService
    {
        Task<List<AbstractControl>> GetTreeAsync(int depth = 50);
        Task<ScreenSnapshot> SaveSnapshotAsync(string name, string phase);
        Task<string[]> ListSnapshotsAsync();
        Task<ScreenSnapshot?> GetSnapshotAsync(string fileName);
        Task<ActionResult> DoActionAsync(ActionCommand command);
        Task<NavigationInfo> GetNavigationAsync();
    }
}

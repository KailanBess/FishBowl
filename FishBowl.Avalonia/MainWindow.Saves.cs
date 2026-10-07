using System.Threading.Tasks;

namespace EmulatorHub
{
    public partial class MainWindow
    {
        // Library toolbar "Save timeline": the selected game's saves and snapshot history (SaveHistoryDialog).
        private Task ShowLibrarySaveTimeline() { return ShowSaveHistory(SelectedLibraryGame()); }
    }
}

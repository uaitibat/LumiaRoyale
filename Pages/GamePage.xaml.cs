using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using LumiaRoyale.Game;

namespace LumiaRoyale.Pages
{
    public sealed partial class GamePage : Page
    {
        private readonly ArenaGame _game;

        public GamePage()
        {
            InitializeComponent();

            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;

            _game = new ArenaGame(SwapPanel);
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Escape && _game != null)
                _game.Exit();
        }
    }
}

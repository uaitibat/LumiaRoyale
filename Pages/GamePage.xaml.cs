using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using LumiaRoyale.Game;
using MonoGame.Framework;

namespace LumiaRoyale.Pages
{
    public sealed partial class GamePage : Page
    {
        private readonly ArenaGame _game;

        public GamePage()
        {
            InitializeComponent();
            var launchArguments = string.Empty;
            _game = XamlGame<ArenaGame>.Create(launchArguments, Window.Current.CoreWindow, SwapPanel);
        }
    }
}

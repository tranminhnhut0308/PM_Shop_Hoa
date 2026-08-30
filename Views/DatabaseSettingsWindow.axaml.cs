using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class DatabaseSettingsWindow : Window
{
    public DatabaseSettingsWindow(Window? owner = null)
    {
        InitializeComponent();

        DataContext = new DatabaseSettingsViewModel();

        if (owner != null)
        {
            Owner = owner;
        }
    }
}
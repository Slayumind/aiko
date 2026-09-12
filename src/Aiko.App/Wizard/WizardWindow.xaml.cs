using System.Windows;
using System.Windows.Input;

namespace Aiko.App;

public partial class WizardWindow : Window
{
    public WizardWindow()
    {
        InitializeComponent();

        Panel.Finished += Close;
        Panel.DragHandle.MouseLeftButtonDown += OnDragHandlePressed;
    }

    private void OnDragHandlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

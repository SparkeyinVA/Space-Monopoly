namespace SpaceMonopoly.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.AddMessageFilter(new ButtonDebounce());
        Application.Run(new MainForm());
    }
}

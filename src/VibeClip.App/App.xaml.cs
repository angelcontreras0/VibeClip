using System.Threading;

namespace VibeClip.App;
public partial class App : Application
{
    private Mutex? instance;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, @"Local\VibeClip.Desktop", out ownsInstance);
        if (!ownsInstance) { MessageBox.Show("VibeClip ya está abierta. Busca su icono junto al reloj. Cierra la versión anterior antes de abrir otra.", "VibeClip"); Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsInstance) instance?.ReleaseMutex(); instance?.Dispose(); Branding.Icon.Dispose(); base.OnExit(e);
    }
}

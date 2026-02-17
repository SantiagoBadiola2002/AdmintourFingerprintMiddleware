using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AdmintourFingerprintMiddleware.Services

{
    public class TrayService
    {
        private Thread _thread;

        public void Start()
        {
            _thread = new Thread(RunTray);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        private void RunTray()
        {
            var icon = new NotifyIcon
            {
                Icon = new Icon("wwwroot/img/icon.ico"),
                Visible = true,
                Text = "Middleware Huellas"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir panel", null, (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:5000",
                    UseShellExecute = true
                });
            });

            menu.Items.Add("Salir", null, (s, e) =>
            {
                icon.Visible = false;
                Application.Exit();
                Environment.Exit(0);
            });

            icon.ContextMenuStrip = menu;

            Application.Run();
        }
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AdmintourFingerprintMiddleware.Services
{
    public class TrayService
    {
        private Thread? _thread;
        private const string UrlBase = "http://localhost:5000";

        public void Start()
        {
            _thread = new Thread(RunTray);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        private void RunTray()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Intentar cargar el icono, si falla usa uno genérico del sistema
            Icon appIcon;
            try
            {
                appIcon = new Icon("wwwroot/img/icon.ico");
            }
            catch
            {
                appIcon = SystemIcons.Application;
            }

            var notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "Admintour Fingerprint Service"
            };

            // Crear el Menú Contextual
            var contextMenu = new ContextMenuStrip();

            // 1. Opción Login
            contextMenu.Items.Add("Login", null, (s, e) =>
                AbrirUrl($"{UrlBase}/AdmintourHuellas/Login"));

            // 2. Opción Registrarse
            contextMenu.Items.Add("Registrarse", null, (s, e) =>
                AbrirUrl($"{UrlBase}/AdmintourHuellas/Enrolar"));

            // Separador
            contextMenu.Items.Add(new ToolStripSeparator());

            // 3. Opción Salir
            contextMenu.Items.Add("Salir", null, (s, e) =>
            {
                notifyIcon.Visible = false;
                Application.Exit();
                Environment.Exit(0);
            });

            notifyIcon.ContextMenuStrip = contextMenu;

            // Al hacer doble click en el icono, abre el Login por defecto
            notifyIcon.DoubleClick += (s, e) => AbrirUrl($"{UrlBase}/AdmintourHuellas/Login");

            // Mantener el hilo de UI vivo
            Application.Run();
        }

        private void AbrirUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"No se pudo abrir la URL: {ex.Message}");
            }
        }
    }
}
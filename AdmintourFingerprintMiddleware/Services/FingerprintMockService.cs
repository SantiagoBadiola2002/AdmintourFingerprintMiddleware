using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AdmintourFingerprintMiddleware.Models;
using libzkfpcsharp;

namespace AdmintourFingerprintMiddleware.Services
{

    public class FingerprintMockService
    {
        private readonly ILogger<FingerprintMockService> _logger;
        private IntPtr _deviceHandle = IntPtr.Zero;

        public FingerprintMockService(ILogger<FingerprintMockService> logger)
        {
            _logger = logger;
        }

        public bool Inicializar()
        {
            _logger.LogInformation("MockService: Inicializando SDK...");
            int ret = zkfp2.Init();
            bool result = ret == 0;
            _logger.LogInformation("MockService: SDK inicializado, resultado = {Result}", result);
            return result;
        }

        public int ObtenerCantidadDispositivos()
        {
            _logger.LogInformation("MockService: Obteniendo cantidad de dispositivos...");
            int count = zkfp2.GetDeviceCount();
            _logger.LogInformation("MockService: Cantidad de dispositivos detectados = {Count}", count);
            return count;
        }

        public bool AbrirDispositivo(int index = 0)
        {
            _logger.LogInformation("MockService: Intentando abrir dispositivo en índice {Index}", index);
            _deviceHandle = zkfp2.OpenDevice(index);
            bool result = _deviceHandle != IntPtr.Zero;
            _logger.LogInformation("MockService: Dispositivo abierto = {Result}", result);
            return result;
        }

        public void CerrarDispositivo()
        {
            _logger.LogInformation("MockService: Cerrando dispositivo...");
            if (_deviceHandle != IntPtr.Zero)
            {
                zkfp2.CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                _logger.LogInformation("MockService: Dispositivo cerrado.");
            }
            else
            {
                _logger.LogWarning("MockService: No había dispositivo abierto para cerrar.");
            }
        }

        public (int Width, int Height) ObtenerTamanioImagen()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            int size = 4;
            byte[] buffer = new byte[size];

            int ret = zkfp2.GetParameters(_deviceHandle, 1, buffer, ref size);
            if (ret != 0) throw new Exception("Error leyendo parámetro ancho");
            int width = BitConverter.ToInt32(buffer, 0);

            size = 4;
            buffer = new byte[size];
            ret = zkfp2.GetParameters(_deviceHandle, 2, buffer, ref size);
            if (ret != 0) throw new Exception("Error leyendo parámetro alto");
            int height = BitConverter.ToInt32(buffer, 0);

            return (width, height);
        }

        public (string Vendor, string Product, string Serial) GetDeviceStrings()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            int size = 256;
            byte[] buffer = new byte[size];

            int ret = zkfp2.GetParameters(_deviceHandle, 1101, buffer, ref size);
            if (ret != 0) throw new Exception("Error leyendo Vendor");
            string vendor = System.Text.Encoding.ASCII.GetString(buffer, 0, size).TrimEnd('\0');

            size = 256;
            buffer = new byte[size];
            ret = zkfp2.GetParameters(_deviceHandle, 1102, buffer, ref size);
            if (ret != 0) throw new Exception("Error leyendo Product");
            string product = System.Text.Encoding.ASCII.GetString(buffer, 0, size).TrimEnd('\0');

            size = 256;
            buffer = new byte[size];
            ret = zkfp2.GetParameters(_deviceHandle, 1103, buffer, ref size);
            if (ret != 0) throw new Exception("Error leyendo Serial");
            string serial = System.Text.Encoding.ASCII.GetString(buffer, 0, size).TrimEnd('\0');

            return (vendor, product, serial);
        }

        public DeviceInfo GetDeviceInfo()
        {
            var (width, height) = ObtenerTamanioImagen();
            var (vendor, product, serial) = GetDeviceStrings();

            return new DeviceInfo
            {
                Width = width,
                Height = height,
                Vendor = vendor,
                Product = product,
                Serial = serial
            };
        }

        public byte[] ConvertirBase64ATemplate(string base64Template)
        {
            return zkfp2.Base64ToBlob(base64Template);
        }

        public byte[] CapturarHuellaEsperando()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            var (width, height) = ObtenerTamanioImagen();

            byte[] imgBuffer = new byte[width * height];
            byte[] templateBuffer = new byte[2048];

            _logger.LogInformation("MockService: Esperando huella del usuario...");

            while (true)
            {
                int templateSize = templateBuffer.Length;
                int ret = zkfp2.AcquireFingerprint(_deviceHandle, imgBuffer, templateBuffer, ref templateSize);

                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    if (templateSize > 0)
                    {
                        byte[] finalTemplate = new byte[templateSize];
                        Array.Copy(templateBuffer, finalTemplate, templateSize);

                        _logger.LogInformation("MockService: Huella capturada correctamente, tamaño del template = {Size}", templateSize);
                        return finalTemplate; // Devuelve template binario
                    }
                    else
                    {
                        _logger.LogWarning("MockService: AcquireFingerprint devolvió OK pero templateSize=0. Reintentando...");
                    }
                }
                else if (ret == zkfp.ZKFP_ERR_BUSY || ret == zkfp.ZKFP_ERR_CAPTURE)
                {
                    // Errores temporales, seguimos esperando
                }
                else
                {
                    _logger.LogError("MockService: Error capturando huella, código = {Ret}", ret);
                    throw new Exception($"Error capturando huella, código = {ret}");
                }

                Thread.Sleep(200);
            }
        }



        public bool InicializarYAbrir(int index = 0)
        {
            if (!Inicializar())
                return false;

            int dispositivos = ObtenerCantidadDispositivos();
            if (dispositivos <= 0)
                return false;

            return AbrirDispositivo(index);
        }

        public string CapturarHuella3Veces()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            const int REGISTER_FINGER_COUNT = 3;
            byte[][] RegTmps = new byte[REGISTER_FINGER_COUNT][];
            int[] sizes = new int[REGISTER_FINGER_COUNT];

            _logger.LogInformation("MockService: Usuario debe colocar la huella 3 veces para generar un template confiable.");

            // Capturar 3 veces
            for (int i = 0; i < REGISTER_FINGER_COUNT; i++)
            {
                _logger.LogInformation("MockService: Captura {Index} de 3...", i + 1);
                byte[] template = CapturarHuellaEsperando(); // Devuelve byte[] binario
                RegTmps[i] = new byte[template.Length];
                Array.Copy(template, RegTmps[i], template.Length);
                sizes[i] = template.Length;

                _logger.LogInformation("MockService: Captura {Index} completada, tamaño = {Size}", i + 1, template.Length);
            }

            // Crear DB temporal para fusionar
            IntPtr tmpDB = zkfp2.DBInit();
            if (tmpDB == IntPtr.Zero)
                throw new Exception("No se pudo inicializar DB temporal para fusionar templates.");

            byte[] mergedTemplate = new byte[2048];
            int mergedSize = mergedTemplate.Length;

            int ret = zkfp2.DBMerge(tmpDB, RegTmps[0], RegTmps[1], RegTmps[2], mergedTemplate, ref mergedSize);
            zkfp2.DBFree(tmpDB);

            if (ret != zkfp.ZKFP_ERR_OK)
                throw new Exception($"Error fusionando templates, código = {ret}");

            // Copiar solo los bytes válidos según mergedSize
            byte[] finalTemplate = new byte[mergedSize];
            Array.Copy(mergedTemplate, finalTemplate, mergedSize);

            // Convertir a Base64
            string base64Final = Convert.ToBase64String(finalTemplate);
            _logger.LogInformation("MockService: Template final generado correctamente, tamaño = {Size}", mergedSize);

            return base64Final;
        }





    }

}

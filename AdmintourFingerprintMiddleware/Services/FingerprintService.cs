using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using libzkfpcsharp;

namespace AdmintourFingerprintMiddleware.Services
{
    public class FingerprintService
    {
        private readonly ILogger<FingerprintService> _logger;

        private IntPtr _manejadorDispositivo = IntPtr.Zero;
        private IntPtr _manejadorBD = IntPtr.Zero;

        private int _fpWidth;
        private int _fpHeight;

        private Thread _hiloCaptura;
        private bool _cancelarCaptura;
        private byte[] _ultimaHuella;
        private int _ultimoTam;

        public FingerprintService(ILogger<FingerprintService> logger)
        {
            _logger = logger;
        }

        public bool Inicializar()
        {
            int ret = zkfp2.Init();
            if (ret == zkfp.ZKFP_ERR_OK)
                _logger.LogInformation("Service: Dispositivo inicializado correctamente");
            else
                _logger.LogError("Service: Error al inicializar el dispositivo, código {Codigo}", ret);

            return ret == zkfp.ZKFP_ERR_OK;
        }

        public int ObtenerCantidadDispositivos()
        {
            int count = zkfp2.GetDeviceCount();
            _logger.LogInformation("Service: Cantidad de dispositivos detectados: {Count}", count);
            return count;
        }

        public bool AbrirDispositivo(int indice = 0)
        {
            _manejadorDispositivo = zkfp2.OpenDevice(indice);
            if (_manejadorDispositivo == IntPtr.Zero)
            {
                _logger.LogError("Service: Error al abrir el dispositivo");
                return false;
            }

            _manejadorBD = zkfp2.DBInit();
            if (_manejadorBD == IntPtr.Zero)
            {
                _logger.LogError("Service: Error al inicializar la base de datos");
                return false;
            }

            byte[] param = new byte[4];
            int size = 4;
            zkfp2.GetParameters(_manejadorDispositivo, 1, param, ref size);
            zkfp2.ByteArray2Int(param, ref _fpWidth);

            size = 4;
            zkfp2.GetParameters(_manejadorDispositivo, 2, param, ref size);
            zkfp2.ByteArray2Int(param, ref _fpHeight);

            _logger.LogInformation("Service: Dispositivo abierto correctamente. Tamaño huella: {Width}x{Height}", _fpWidth, _fpHeight);
            return true;
        }

        public void CerrarDispositivo()
        {
            _cancelarCaptura = true;
            _hiloCaptura?.Join();

            if (_manejadorDispositivo != IntPtr.Zero)
            {
                zkfp2.CloseDevice(_manejadorDispositivo);
                _manejadorDispositivo = IntPtr.Zero;
                _logger.LogInformation("Service: Dispositivo cerrado");
            }

            if (_manejadorBD != IntPtr.Zero)
            {
                zkfp2.DBFree(_manejadorBD);
                _manejadorBD = IntPtr.Zero;
                _logger.LogInformation("Service: Base de datos liberada");
            }
        }

        // ----------- Captura segura -----------
        public void IniciarCapturaContinua()
        {
            _ultimaHuella = null;
            _ultimoTam = 0;
            _cancelarCaptura = false;

            _hiloCaptura = new Thread(() =>
            {
                byte[] bufferImagen = new byte[_fpWidth * _fpHeight];
                byte[] bufferTemplate = new byte[2048];
                int tamTemplate;

                int intento = 0;
                const int intentosMax = 10;
                const int delayMs = 500;

                while (!_cancelarCaptura && intento < intentosMax)
                {
                    tamTemplate = bufferTemplate.Length;
                    int ret = zkfp2.AcquireFingerprint(_manejadorDispositivo, bufferImagen, bufferTemplate, ref tamTemplate);
                    if (ret == zkfp.ZKFP_ERR_OK)
                    {
                        _ultimaHuella = (byte[])bufferTemplate.Clone();
                        _ultimoTam = tamTemplate;
                        break;
                    }
                    intento++;
                    _logger.LogWarning("Service: Intento {Intento} fallido para capturar huella, código {Codigo}", intento, ret);
                    Thread.Sleep(delayMs);
                }
            });
            _hiloCaptura.IsBackground = true;
            _hiloCaptura.Start();
        }

        public string ObtenerHuellaCapturada(int timeoutSegundos = 30)
        {
            if (_manejadorDispositivo == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            byte[] bufferImagen = new byte[_fpWidth * _fpHeight];
            byte[] bufferTemplate = new byte[2048];
            int tamTemplate = bufferTemplate.Length;

            int ret;
            var startTime = DateTime.Now;

            do
            {
                ret = zkfp2.AcquireFingerprint(_manejadorDispositivo, bufferImagen, bufferTemplate, ref tamTemplate);

                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    _logger.LogInformation("Service: Huella capturada correctamente");
                    return zkfp2.BlobToBase64(bufferTemplate, tamTemplate);
                }

                Thread.Sleep(200); // evita que se llene la CPU
            }
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSegundos);

            _logger.LogError("Service: No se pudo capturar la huella después de {Timeout} segundos", timeoutSegundos);
            throw new Exception($"No se pudo capturar la huella después de {timeoutSegundos} segundos");
        }


        public string CapturarHuella()
        {
            if (_manejadorDispositivo == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            IniciarCapturaContinua();
            return ObtenerHuellaCapturada();
        }
        // -------------------------------------

        public bool HuellaYaRegistrada(string huellaBase64, out int fid)
        {
            fid = 0;

            if (string.IsNullOrEmpty(huellaBase64))
                throw new ArgumentException("El template de huella es null o vacío.");

            if (_manejadorBD == IntPtr.Zero)
                throw new InvalidOperationException("BD no inicializada");

            byte[] template = zkfp2.Base64ToBlob(huellaBase64);
            int score = 0;
            int ret = zkfp2.DBIdentify(_manejadorBD, template, ref fid, ref score);

            return ret == zkfp.ZKFP_ERR_OK;
        }


        public string RegistrarHuellaPasoAPaso(List<string> templatesBase64)
        {
            if (_manejadorBD == IntPtr.Zero)
                throw new InvalidOperationException("BD no inicializada");

            if (templatesBase64.Count != 3)
                throw new ArgumentException("Se requieren 3 huellas para unificar.");

            byte[][] listaTmp = new byte[3][];
            for (int i = 0; i < 3; i++)
                listaTmp[i] = zkfp2.Base64ToBlob(templatesBase64[i]);

            byte[] huellaFinal = new byte[2048];
            int tamFinal = huellaFinal.Length;

            int mergeRet = zkfp2.DBMerge(_manejadorBD, listaTmp[0], listaTmp[1], listaTmp[2], huellaFinal, ref tamFinal);
            if (mergeRet != zkfp.ZKFP_ERR_OK)
                throw new Exception("Error al unificar huellas, código: " + mergeRet);

            _logger.LogInformation("Service: Huellas unificadas correctamente");
            return zkfp2.BlobToBase64(huellaFinal, tamFinal);
        }

        public bool VerificarHuella(string huellaCapturadaBase64, string huellaGuardadaBase64)
        {
            if (_manejadorBD == IntPtr.Zero)
                throw new InvalidOperationException("BD no inicializada");

            byte[] huellaCapturada = zkfp2.Base64ToBlob(huellaCapturadaBase64);
            byte[] huellaGuardada = zkfp2.Base64ToBlob(huellaGuardadaBase64);

            int score = zkfp2.DBMatch(_manejadorBD, huellaCapturada, huellaGuardada);
            return score > 0;
        }
    }
}

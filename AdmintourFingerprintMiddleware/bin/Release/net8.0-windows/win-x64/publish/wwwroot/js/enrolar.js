async function registrar() {
    const btnRegistrar = document.getElementById('btnRegistrar');
    const btnRetry = document.getElementById('btnRetry');
    const loader = document.getElementById('loader');
    const estado = document.getElementById('estado');
    const instrucciones = document.getElementById('instrucciones');

    // 1. Reset de UI y Estado Visual
    btnRegistrar.style.display = 'none';
    btnRetry.style.display = 'none';
    loader.style.display = 'block';
    estado.innerText = "Esperando huellas (coloque el dedo 3 veces)...";
    estado.className = "estado procesando";

    // CAMBIO AQUÍ: Ahora pide el dedo inmediatamente
    estado.innerText = "Coloque su dedo en el lector...";
    estado.className = "estado procesando";

    try {
        // 2. Llamada al endpoint de enrolamiento
        const response = await fetch('/api/huellas/capturar-y-enviar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });

        const result = await response.json();

        if (response.ok) {
            // CASO ÉXITO
            loader.style.display = 'none';
            estado.innerText = "¡Registro Exitoso!";
            estado.className = "estado ok";
            instrucciones.innerText = "Huella guardada correctamente. Reiniciando...";

            setTimeout(() => {
                window.location.href = window.location.pathname + "?t=" + new Date().getTime();
            }, 2500);

        } else {
            // CASO ERROR CONTROLADO (Incluye Timeout 408)
            // Si es 408, el mensaje será "Tiempo de espera agotado durante la captura."
            manejarError(result.mensaje || "Error al registrar huella.");
        }

    } catch (error) {
        // CASO ERROR CRÍTICO (Middleware desconectado)
        console.error("Error crítico:", error);
        manejarError("No se pudo establecer comunicación con el lector.");
    }
}

function manejarError(mensaje) {
    const loader = document.getElementById('loader');
    const estado = document.getElementById('estado');
    const btnRetry = document.getElementById('btnRetry');
    const instrucciones = document.getElementById('instrucciones');

    loader.style.display = 'none';
    estado.innerText = mensaje; // Quitamos el prefijo "Error: " para que sea más limpio
    estado.className = "estado error";

    // Cambiamos instrucciones para dar feedback al usuario
    instrucciones.innerText = "El proceso se detuvo. Asegúrese de colocar el dedo cuando el lector encienda su luz.";

    btnRetry.style.display = 'inline-block';
}
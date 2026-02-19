async function loginHuella() {
    const btn = document.querySelector('.primary');
    const divResultado = document.getElementById('resultado');
    const loader = document.querySelector('.loader');

    // 1. Estado Visual Inicial
    btn.disabled = true;
    loader.style.display = "block";
    divResultado.innerText = "Coloque su dedo en el lector...";
    divResultado.className = "estado procesando";

    try {
        // 2. Llamada al Controller
        const response = await fetch('/api/huellas/validar-en-lista-admintour', {
            method: 'POST'
        });

        const data = await response.json();

        // 3. Evaluar la respuesta del servidor
        if (response.ok && data.ok) {
            // CASO ÉXITO: Huella encontrada
            divResultado.className = "estado ok";
            divResultado.innerHTML = `
                ¡Bienvenido!<br>
                <small>${data.nombre} ${data.apellido}</small><br>
                <span style="font-size: 0.7em; color: #666;">La página se recargará en 10 segundos...</span>
            `;

            // RECARGA AUTOMÁTICA DESPUÉS DE 10 SEGUNDOS
            setTimeout(() => {
                console.log("Recargando página por éxito...");
                window.location.reload();
            }, 10000); // 10000 ms = 10 segundos

        }
        else if (response.status === 408) {
            // CASO ESPECÍFICO: Timeout (Tiempo agotado)
            divResultado.className = "estado error";
            divResultado.innerText = data.mensaje || "Tiempo agotado. Intente de nuevo.";
        }
        else {
            // CASO ERROR: No coincide, lista vacía, etc.
            divResultado.className = "estado error";
            divResultado.innerText = data.mensaje || "Acceso denegado o error en validación.";
        }

    } catch (error) {
        divResultado.className = "estado error";
        divResultado.innerText = "No se pudo conectar con el servicio de huellas.";
        console.error("Error en login:", error);
    } finally {
        // Solo quitamos el loader y habilitamos el botón si NO fue éxito 
        // para evitar que el usuario intente clickear mientras espera la recarga.
        const exito = divResultado.classList.contains('ok');
        if (!exito) {
            btn.disabled = false;
            loader.style.display = "none";
        } else {
            // Si fue éxito, solo ocultamos el loader pero dejamos el botón deshabilitado
            loader.style.display = "none";
        }
    }
}
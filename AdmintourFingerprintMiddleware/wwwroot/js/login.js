async function loginHuella() {
    const btn = document.querySelector('.primary');
    const divResultado = document.getElementById('resultado');
    const loader = document.querySelector('.loader');

    btn.disabled = true;
    loader.style.display = "block";
    divResultado.innerText = "Coloque su dedo en el lector...";
    divResultado.className = "estado procesando";

    try {
        // 1. Validar la huella
        const response = await fetch('/api/huellas/validar-en-lista-admintour', { method: 'POST' });
        const data = await response.json();

        if (response.ok && data.ok) {
            divResultado.innerText = "Huella reconocida. Obteniendo acceso...";

            // 2. Obtener la URL de login con los códigos +2026
            const loginResponse = await fetch('/api/huellas/login-admintour', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    hotelCodigo: data.hotelCodigo.toString(),
                    usuarioId: data.hotelHuellaUsuarioId.toString()
                })
            });

            const loginData = await loginResponse.json();

            if (loginResponse.ok && loginData.ok) {
                // ÉXITO: Redirigir al link generado
                divResultado.className = "estado ok";
                divResultado.innerHTML = `¡Bienvenido! <br> Redirigiendo...`;


                if (loginData.url) {
                    window.location.href = loginData.url;
                } else {
                    console.error("No se recibió una URL válida del servidor");
                }

            } else {
                throw new Error(loginData.mensaje || "Error al procesar el acceso.");
            }
        } else {
            // Manejo de errores de validación (408 o denegado)
            divResultado.className = "estado error";
            divResultado.innerText = data.mensaje || "Acceso denegado.";
        }
    } catch (error) {
        divResultado.className = "estado error";
        divResultado.innerText = error.message || "Error de conexión.";
    } finally {
        // Si no hubo redirección (hubo error), reactivamos el botón
        if (!divResultado.classList.contains('ok')) {
            btn.disabled = false;
            loader.style.display = "none";
        }
    }
}
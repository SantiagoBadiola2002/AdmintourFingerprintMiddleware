async function call(method, url, data = null) {
    try {
        setResult("Procesando...");

        const res = await fetch(url, {
            method: method,
            headers: { "Content-Type": "application/json" },
            body: data ? JSON.stringify(data) : null
        });

        const txt = await res.text();
        setResult(txt);
    }
    catch (e) {
        setResult(e);
    }
}

function setResult(txt) {
    document.getElementById("resultado").innerText = txt;
}

async function comparar() {
    const template = document.getElementById("template").value;
    await call("POST", "/api/huellas/compararDosHuellas", template);
}

async function capturarYEnviar() {
    await call("POST", "/api/huellas/capturar-y-enviar");
}

async function cargarHotel() {

    try {
        const res = await fetch("/api/huellas/hotel");
        const data = await res.json();

        document.getElementById("hotelLabel").textContent =
            "Hotel código: " + data.hotelCodigo;

    } catch {
        document.getElementById("hotelLabel").textContent =
            "Hotel desconocido";
    }
}

window.addEventListener("DOMContentLoaded", cargarHotel);

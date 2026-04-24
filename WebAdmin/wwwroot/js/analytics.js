window.analyticsMap = null;
window.analyticsMarkers = [];

window.loadHeatmap = function (points) {
    console.log("loadHeatmap called", points);

    const mapElement = document.getElementById("map");
    if (!mapElement) {
        console.error("Không tìm th?y div #map");
        return;
    }
    if (typeof L === "undefined") {
        console.error("Leaflet ch?a ???c load");
        return;
    }
    if (!points || points.length === 0) {
        console.error("Không có d? li?u heatmap");
        return;
    }

    const validPoints = points.filter(p => p.latitude && p.longitude);
    console.log("validPoints", validPoints);

    if (validPoints.length === 0) return;

    if (window.analyticsMap) {
        window.analyticsMap.remove();
        window.analyticsMap = null;
    }

    window.analyticsMap = L.map("map").setView(
        [validPoints[0].latitude, validPoints[0].longitude],
        16
    );

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "&copy; OpenStreetMap contributors"
    }).addTo(window.analyticsMap);

    validPoints.forEach(p => {
        L.circleMarker([p.latitude, p.longitude], {
            radius: 7,
            color: "#0d6efd",
            fillColor: "#0d6efd",
            fillOpacity: 0.5,
            weight: 1
        }).addTo(window.analyticsMap);
    });

    setTimeout(() => {
        window.analyticsMap.invalidateSize();
    }, 300);
};
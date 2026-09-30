// Leaflet map used by the home module to pick a house location.
// init() draws the map, drops a draggable marker, and calls back into Blazor
// (OnMapPicked) whenever the marker moves or the map is clicked.
window.nestifyMapPicker = (function () {
    const maps = {};

    function init(elementId, lat, lng, dotNetRef) {
        destroy(elementId);

        const map = L.map(elementId).setView([lat, lng], 15);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const marker = L.marker([lat, lng], { draggable: true }).addTo(map);

        function report(position) {
            dotNetRef.invokeMethodAsync('OnMapPicked', position.lat, position.lng);
        }

        marker.on('dragend', () => report(marker.getLatLng()));
        map.on('click', (e) => {
            marker.setLatLng(e.latlng);
            report(e.latlng);
        });

        // The modal animates in, so the container size settles a frame later.
        setTimeout(() => map.invalidateSize(), 120);

        maps[elementId] = map;
    }

    function destroy(elementId) {
        if (maps[elementId]) {
            maps[elementId].remove();
            delete maps[elementId];
        }
    }

    return { init, destroy };
})();

let map;
let dotNetReference;
let googleMapsPromise;
let markers = [];
let destinationMarker;
let infoWindow;
let mapUnavailableMessage;

function ensureGoogleMaps(apiKey) {
    if (!apiKey) {
        return Promise.reject(new Error("Google Maps API key is missing."));
    }

    if (window.google?.maps) {
        return Promise.resolve(window.google.maps);
    }

    if (!googleMapsPromise) {
        googleMapsPromise = new Promise((resolve, reject) => {
            const callbackName = `smartParkingMapsReady_${Date.now()}`;
            window[callbackName] = () => {
                delete window[callbackName];
                resolve(window.google.maps);
            };

            const script = document.createElement("script");
            script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places&callback=${callbackName}`;
            script.async = true;
            script.defer = true;
            script.onerror = () => {
                delete window[callbackName];
                reject(new Error("Google Maps SDK failed to load."));
            };
            document.head.appendChild(script);
        });
    }

    return googleMapsPromise;
}

function showMapUnavailable(message) {
    const mapElement = document.getElementById("parking-map");
    if (!mapElement) {
        return;
    }

    mapElement.innerHTML = "";
    mapUnavailableMessage = document.createElement("div");
    mapUnavailableMessage.className = "map-unavailable-message";
    mapUnavailableMessage.textContent = message;
    mapElement.appendChild(mapUnavailableMessage);
}

export async function initMap(dotNetRef, apiKey) {
    dotNetReference = dotNetRef;

    try {
        await ensureGoogleMaps(apiKey);
    } catch (error) {
        showMapUnavailable("Map unavailable — check your connection.");
        throw error;
    }

    const mapElement = document.getElementById("parking-map");
    const input = document.getElementById("destination-search");
    if (!mapElement || !input) {
        return;
    }

    map = new google.maps.Map(mapElement, {
        center: { lat: 1.3521, lng: 103.8198 },
        zoom: 12,
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: false
    });

    infoWindow = new google.maps.InfoWindow();

    const autocomplete = new google.maps.places.Autocomplete(input, {
        componentRestrictions: { country: "sg" },
        fields: ["formatted_address", "geometry", "name"]
    });

    autocomplete.addListener("place_changed", async () => {
        const place = autocomplete.getPlace();
        if (!place?.geometry?.location) {
            await dotNetReference.invokeMethodAsync("OnPlaceSearchFailed");
            return;
        }

        const lat = place.geometry.location.lat();
        const lng = place.geometry.location.lng();
        const label = place.formatted_address || place.name || input.value;
        await setDestination(lat, lng, label);
        await dotNetReference.invokeMethodAsync("OnPlaceSelected", lat, lng, label);
    });
}

async function setDestination(lat, lng, label) {
    if (!map) {
        return;
    }

    const position = { lat, lng };
    if (!destinationMarker) {
        destinationMarker = new google.maps.Marker({
            position,
            map,
            icon: {
                path: google.maps.SymbolPath.CIRCLE,
                scale: 8,
                fillColor: "#0f6b4f",
                fillOpacity: 1,
                strokeColor: "#ffffff",
                strokeWeight: 2
            },
            title: label || "Destination"
        });
    } else {
        destinationMarker.setPosition(position);
        destinationMarker.setTitle(label || "Destination");
    }

    map.panTo(position);
    map.setZoom(16);
}

export async function searchPlace(query) {
    if (!map || !query) {
        return false;
    }

    const service = new google.maps.places.PlacesService(map);
    return new Promise((resolve) => {
        service.textSearch(
            {
                query,
                region: "sg"
            },
            async (results, status) => {
                if (status !== google.maps.places.PlacesServiceStatus.OK || !results?.length || !results[0].geometry?.location) {
                    await dotNetReference.invokeMethodAsync("OnPlaceSearchFailed");
                    resolve(false);
                    return;
                }

                const result = results[0];
                const lat = result.geometry.location.lat();
                const lng = result.geometry.location.lng();
                const label = result.formatted_address || result.name || query;
                document.getElementById("destination-search").value = label;
                await setDestination(lat, lng, label);
                await dotNetReference.invokeMethodAsync("OnPlaceSelected", lat, lng, label);
                resolve(true);
            });
    });
}

export function updateMarkers(carparks) {
    if (!map || !window.google?.maps) {
        return;
    }

    for (const marker of markers) {
        marker.setMap(null);
    }
    markers = [];

    for (const carpark of carparks || []) {
        const marker = new google.maps.Marker({
            map,
            position: { lat: carpark.latitude, lng: carpark.longitude },
            title: `${carpark.carparkNo} · ${carpark.address}`
        });

        marker.addListener("click", async () => {
            infoWindow.setContent(`<strong>${carpark.carparkNo}</strong><br/>${carpark.address}`);
            infoWindow.open({ map, anchor: marker });
            await dotNetReference.invokeMethodAsync("OnCarparkSelected", carpark.carparkNo);
        });

        markers.push(marker);
    }
}

export function setMapCenter(lat, lng) {
    if (!map) {
        return;
    }

    map.panTo({ lat, lng });
}
